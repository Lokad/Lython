using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Zip;

/// <summary>
/// Governed one-shot member reads shared by <c>read</c>, <c>testzip</c>, and
/// sequential member handles. Inflates STORED/DEFLATED data under work budgets,
/// enforces declared lengths and CRCs before bytes are exposed, rejects truncated
/// streams even when lengths and CRCs match, and applies
/// the encryption/method policy. Returns an uncharged owned array; callers
/// transfer ownership into governed values (or drop it for validation-only
/// reads). Data checksums enforce here, never in the directory reader.
/// </summary>
internal static class ZipMemberReader
{
    private const ushort StoredMethod = 0;
    private const ushort DeflatedMethod = 8;
    private const ushort EncryptedFlag = 0x0001;
    private const ushort PatchedDataFlag = 0x0020;
    private const ushort StrongEncryptionFlag = 0x0040;

    public static byte[] ReadMemberBytes(
        ReadOnlyMemory<byte> archiveBytes,
        string name,
        ushort method,
        ushort flags,
        uint expectedCrc,
        ulong compressedSize,
        ulong uncompressedSize,
        ulong dataOffset,
        ulong dataEndLimit,
        object? password,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        if (method != StoredMethod && method != DeflatedMethod)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                "That compression method is not supported",
                span);
        }

        if ((flags & PatchedDataFlag) != 0)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                "compressed patched data (flag bit 5)",
                span);
        }

        if ((flags & StrongEncryptionFlag) != 0)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                "strong encryption (flag bit 6)",
                span);
        }

        if ((flags & EncryptedFlag) != 0)
        {
            if (password is null or PyNone)
            {
                throw new LythonRuntimeException(
                    "RuntimeError",
                    $"File '{name}' is encrypted, password required for extraction",
                    span);
            }

            if (password is not PyBytes)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    $"pwd: expected bytes, got {PythonTypeName(password)}",
                    span);
            }

            throw new LythonRuntimeException(
                "RuntimeError",
                $"Bad password for file '{name}'",
                span);
        }

        var bytes = archiveBytes.Span;
        if (dataOffset > (ulong)bytes.Length)
        {
            throw BadZipFile($"Member '{name}' starts beyond the end of data.", span);
        }

        if (compressedSize > (ulong)bytes.Length || dataOffset + compressedSize > (ulong)bytes.Length)
        {
            throw BadZipFile($"Member '{name}' extends beyond the end of data.", span);
        }

        if (dataOffset + compressedSize > dataEndLimit)
        {
            throw BadZipFile($"Overlapped entries: '{name}' (possible zip bomb).", span);
        }

        if (method == StoredMethod)
        {
            // Reserve the copy before allocating, and checksum in governed chunks
            // so huge members stay interruptible instead of one unbudgeted pass.
            var length = (int)compressedSize;
            using (context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(length), span))
            {
                var stored = bytes.Slice((int)dataOffset, length).ToArray();
                if ((ulong)stored.Length != uncompressedSize)
                {
                    throw BadZipFile($"Corrupt member '{name}': length mismatch.", span);
                }

                if (Crc32.Compute(stored, context, span) != expectedCrc)
                {
                    throw BadZipFile($"Bad CRC-32 for file '{name}'.", span);
                }

                return stored;
            }
        }

        return InflateMember(bytes, (int)dataOffset, (int)compressedSize, uncompressedSize, expectedCrc, name, context, span);

    }

    private static byte[] InflateMember(
        ReadOnlySpan<byte> bytes,
        int dataOffset,
        int compressedSize,
        ulong uncompressedSize,
        uint expectedCrc,
        string name,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        // Strict single-pass inflation: the decoder itself enforces the declared
        // size, the CRC, and end-of-stream finality, so truncated payloads fail
        // even when their expanded bytes and CRC would otherwise match.
        return StrictDeflateInflater.Inflate(
            bytes.Slice(dataOffset, compressedSize),
            name,
            uncompressedSize,
            expectedCrc,
            context,
            span);
    }


    private static LythonRuntimeException BadZipFile(string message, LythonSourceSpan? span)
        => new(LythonRuntime.ModuleException("zipfile", "BadZipFile"), message, span);

    internal static string PythonTypeName(object? value)
        => value switch
        {
            BigInteger => "int",
            int => "int",
            bool => "bool",
            PyString => "str",
            PyBytes => "bytes",
            PyTuple => "tuple",
            PyList => "list",
            PyDict => "dict",
            PyNone => "NoneType",
            null => "NoneType",
            _ => value.GetType().Name,
        };
}
