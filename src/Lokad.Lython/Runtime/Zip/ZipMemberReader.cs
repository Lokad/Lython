using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Zip;

/// <summary>
/// Governed one-shot member reads shared by <c>read</c>, <c>testzip</c>, and
/// sequential member handles. Inflates STORED/DEFLATED data under work budgets,
/// enforces declared lengths and CRCs before bytes are exposed, and applies
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
    private const int InflateChunkBytes = 8192;

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
            var stored = bytes.Slice((int)dataOffset, (int)compressedSize).ToArray();
            if ((ulong)stored.Length != uncompressedSize)
            {
                throw BadZipFile($"Corrupt member '{name}': length mismatch.", span);
            }

            ValidateCrc(stored, expectedCrc, name, span);
            return stored;
        }

        return InflateMember(bytes, (int)dataOffset, (int)compressedSize, uncompressedSize, expectedCrc, name, context, span);

        static void ValidateCrc(ReadOnlySpan<byte> data, uint expectedCrc, string name, LythonSourceSpan? span)
        {
            if (~Crc32.Update(uint.MaxValue, data) != expectedCrc)
            {
                throw BadZipFile($"Bad CRC-32 for file '{name}'.", span);
            }
        }
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
        if (uncompressedSize > int.MaxValue)
        {
            throw RuntimeErrors.Memory($"zip member '{name}' is too large to materialize", span);
        }

        var output = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            using var source = new MemoryStream(bytes.Slice(dataOffset, compressedSize).ToArray(), writable: false);
            using var deflate = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: false);
            var buffer = new byte[InflateChunkBytes];
            var crc = uint.MaxValue;
            ulong total = 0;
            while (true)
            {
                context.CheckExecutionBudget(span);
                int count;
                try
                {
                    count = deflate.Read(buffer, 0, buffer.Length);
                }
                catch (InvalidDataException exception)
                {
                    throw BadZipFile($"Corrupt member '{name}': invalid deflated data ({exception.Message}).", span);
                }

                if (count == 0)
                {
                    break;
                }

                total = checked(total + (ulong)count);
                if (total > uncompressedSize)
                {
                    throw BadZipFile($"Corrupt member '{name}': length mismatch.", span);
                }

                crc = Crc32.Update(crc, buffer.AsSpan(0, count));
                output.Append(buffer.AsSpan(0, count));
            }

            if (total != uncompressedSize)
            {
                throw BadZipFile($"Corrupt member '{name}': length mismatch.", span);
            }

            if (~crc != expectedCrc)
            {
                throw BadZipFile($"Bad CRC-32 for file '{name}'.", span);
            }

            return output.ToArrayAndRelease();
        }
        catch
        {
            output.Release();
            throw;
        }
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
