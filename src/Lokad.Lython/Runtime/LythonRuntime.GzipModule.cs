using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class GzipModule : PyModule
    {
        public static readonly GzipModule Instance = new();

        private static readonly string[] Members = ["compress", "decompress", "BadGzipFile"];

        private GzipModule() : base("gzip")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "compress" => GzipCompressCallable.Instance,
                "decompress" => new BuiltinCallable(LythonKnownCallableSignatures.GzipDecompress, Decompress),
                "BadGzipFile" => new ExceptionTypeValue("BadGzipFile"),
                _ => null!,
            };

            return value is not null;
        }

        private static object Decompress(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments[0] is not PyBytes data)
            {
                throw new LythonRuntimeException("TypeError", "gzip.decompress(data) requires a bytes-like object", span);
            }

            if (data.Length == 0)
            {
                return CreateBytes([], context, span);
            }

            try
            {
                var compressed = data.ToArray();
                using var output = new MemoryStream();
                var buffer = new byte[8192];
                var position = 0;
                var memberCount = 0;
                while (position < compressed.Length)
                {
                    while (position < compressed.Length && compressed[position] == 0)
                    {
                        position++;
                    }

                    if (position == compressed.Length)
                    {
                        break;
                    }

                    position = ParseGzipHeader(compressed, position, span);
                    var memberCrc = uint.MaxValue;
                    uint memberLength = 0;
                    using (var cursor = new GzipByteCursorStream(compressed, position))
                    {
                        using var deflate = new DeflateStream(cursor, CompressionMode.Decompress, leaveOpen: true);
                        while (true)
                        {
                            context.CheckExecutionBudget(span);
                            var count = deflate.Read(buffer, 0, buffer.Length);
                            if (count == 0)
                            {
                                break;
                            }

                            var newLength = checked(output.Length + count);
                            if (newLength > int.MaxValue)
                            {
                                throw RuntimeErrors.Memory("gzip decompressed output is too large", span);
                            }

                            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes((int)newLength), span);
                            output.Write(buffer, 0, count);
                            memberCrc = UpdateGzipCrc32(memberCrc, buffer.AsSpan(0, count));
                            memberLength = unchecked(memberLength + (uint)count);
                        }

                        position = cursor.BytePosition;
                    }

                    if (compressed.Length - position < 8)
                    {
                        throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
                    }

                    var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(position, 4));
                    var expectedLength = BinaryPrimitives.ReadUInt32LittleEndian(compressed.AsSpan(position + 4, 4));
                    if (~memberCrc != expectedCrc)
                    {
                        throw BadGzip("CRC check failed", span);
                    }

                    if (memberLength != expectedLength)
                    {
                        throw BadGzip("Incorrect length of data produced", span);
                    }

                    position += 8;
                    memberCount++;
                }

                if (memberCount == 0)
                {
                    throw BadGzip("Not a gzipped file", span);
                }

                var decompressed = output.ToArray();
                context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(decompressed.Length), span);
                return CreateBytes(decompressed, context, span);
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (InvalidDataException ex)
            {
                throw BadGzip(ex.Message, span, ex);
            }
            catch (IOException ex)
            {
                throw BadGzip(ex.Message, span, ex);
            }
        }
    }

    private sealed class GzipCompressCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public static readonly GzipCompressCallable Instance = new();

        private GzipCompressCallable()
        {
        }

        public string Name => "gzip.compress";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            object? data = null;
            object? level = null;
            object? mtime = null;
            var dataAssigned = false;
            var levelAssigned = false;
            var mtimeAssigned = false;
            var positionalCount = 0;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    switch (positionalCount++)
                    {
                        case 0 when !dataAssigned:
                            data = argument.Value;
                            dataAssigned = true;
                            break;
                        case 1 when !levelAssigned:
                            level = argument.Value;
                            levelAssigned = true;
                            break;
                        default:
                            throw new LythonRuntimeException("TypeError", "gzip.compress() accepts at most two positional arguments", span);
                    }

                    continue;
                }

                switch (argument.Name)
                {
                    case "data" when !dataAssigned:
                        data = argument.Value;
                        dataAssigned = true;
                        break;
                    case "compresslevel" when !levelAssigned:
                        level = argument.Value;
                        levelAssigned = true;
                        break;
                    case "mtime" when !mtimeAssigned:
                        mtime = argument.Value;
                        mtimeAssigned = true;
                        break;
                    case "data":
                    case "compresslevel":
                    case "mtime":
                        throw new LythonRuntimeException("TypeError", $"gzip.compress() got multiple values for argument '{argument.Name}'", span);
                    default:
                        throw new LythonRuntimeException("TypeError", $"gzip.compress() got an unexpected keyword argument '{argument.Name}'", span);
                }
            }

            if (!dataAssigned)
            {
                throw new LythonRuntimeException("TypeError", "gzip.compress() missing required argument 'data'", span);
            }

            if (data is not PyBytes bytes)
            {
                throw new LythonRuntimeException("TypeError", "gzip.compress(data) requires a bytes-like object", span);
            }

            var compressionLevel = levelAssigned ? ParseCompressionLevel(level!, span) : 9;
            var modificationTime = mtimeAssigned ? ParseModificationTime(mtime!, span) : 0u;
            return CreateBytes(CompressGzip(bytes.Bytes, compressionLevel, modificationTime, context, span), context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private static byte[] CompressGzip(
        ReadOnlySpan<byte> data,
        int compressionLevel,
        uint modificationTime,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var maximumLength = checked((long)data.Length + (data.Length >> 12) + (data.Length >> 14) + (data.Length >> 25) + 31L);
        if (maximumLength > int.MaxValue)
        {
            throw RuntimeErrors.Memory("gzip compressed output is too large", span);
        }

        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes((int)maximumLength), span);
        using var output = new MemoryStream();
        Span<byte> header = stackalloc byte[10];
        header[0] = 0x1f;
        header[1] = 0x8b;
        header[2] = 8;
        header[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], modificationTime);
        header[8] = compressionLevel switch { 1 => 4, 9 => 2, _ => 0 };
        header[9] = 255;
        output.Write(header);

        var options = new ZLibCompressionOptions { CompressionLevel = compressionLevel };
        using (var deflate = new DeflateStream(output, options, leaveOpen: true))
        {
            deflate.Write(data);
        }

        Span<byte> trailer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[..4], ComputeGzipCrc32(data, context, span));
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[4..], unchecked((uint)data.Length));
        output.Write(trailer);
        var compressed = output.ToArray();
        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(compressed.Length), span);
        return compressed;
    }

    private static int ParseCompressionLevel(object value, LythonSourceSpan span)
    {
        BigInteger integer = value switch
        {
            bool boolean => boolean ? BigInteger.One : BigInteger.Zero,
            BigInteger bigInteger => bigInteger,
            _ => throw new LythonRuntimeException("TypeError", "gzip compression level must be an integer", span),
        };

        if (integer < -1 || integer > 9)
        {
            throw new LythonRuntimeException("ValueError", "Bad compression level", span);
        }

        return (int)integer;
    }

    private static uint ParseModificationTime(object value, LythonSourceSpan span)
    {
        if (value is PyNone)
        {
            return 0;
        }

        var numeric = value switch
        {
            bool boolean => boolean ? 1d : 0d,
            BigInteger integer => (double)integer,
            double floating => floating,
            _ => throw new LythonRuntimeException("TypeError", "gzip mtime must be a real number or None", span),
        };
        if (!double.IsFinite(numeric) || numeric < 0 || numeric > uint.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "gzip mtime must be between 0 and 4294967295", span);
        }

        return (uint)numeric;
    }

    private static uint ComputeGzipCrc32(ReadOnlySpan<byte> data, ExecutionContext context, LythonSourceSpan span)
    {
        var crc = uint.MaxValue;
        for (var i = 0; i < data.Length; i++)
        {
            if ((i & 0xfff) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            crc = UpdateGzipCrc32(crc, data.Slice(i, 1));
        }

        return ~crc;
    }

    private static uint UpdateGzipCrc32(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ (0xedb88320u & unchecked((uint)-(int)(crc & 1)));
            }
        }

        return crc;
    }

    private static int ParseGzipHeader(byte[] data, int position, LythonSourceSpan span)
    {
        if (data.Length - position < 10)
        {
            throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
        }

        if (data[position] != 0x1f || data[position + 1] != 0x8b)
        {
            throw BadGzip("Not a gzipped file", span);
        }

        if (data[position + 2] != 8)
        {
            throw BadGzip("Unknown compression method", span);
        }

        var flags = data[position + 3];
        if ((flags & 0xe0) != 0)
        {
            throw BadGzip("Reserved flags are set", span);
        }

        position += 10;
        if ((flags & 0x04) != 0)
        {
            if (data.Length - position < 2)
            {
                throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
            }

            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position, 2));
            position += 2;
            if (data.Length - position < extraLength)
            {
                throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
            }

            position += extraLength;
        }

        if ((flags & 0x08) != 0)
        {
            position = SkipGzipZeroTerminatedField(data, position, span);
        }

        if ((flags & 0x10) != 0)
        {
            position = SkipGzipZeroTerminatedField(data, position, span);
        }

        if ((flags & 0x02) != 0)
        {
            if (data.Length - position < 2)
            {
                throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
            }

            position += 2;
        }

        return position;
    }

    private static int SkipGzipZeroTerminatedField(byte[] data, int position, LythonSourceSpan span)
    {
        while (position < data.Length)
        {
            if (data[position++] == 0)
            {
                return position;
            }
        }

        throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
    }

    private sealed class GzipByteCursorStream : Stream
    {
        private readonly byte[] _data;

        public GzipByteCursorStream(byte[] data, int position)
        {
            _data = data;
            BytePosition = position;
        }

        public int BytePosition { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => BytePosition; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0 || BytePosition >= _data.Length)
            {
                return 0;
            }

            buffer[offset] = _data[BytePosition++];
            return 1;
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0 || BytePosition >= _data.Length)
            {
                return 0;
            }

            buffer[0] = _data[BytePosition++];
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static LythonRuntimeException BadGzip(string message, LythonSourceSpan span, Exception? inner = null)
        => new("BadGzipFile", message, span, inner);
}
