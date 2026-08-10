using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly uint[] GzipCrc32Table = BuildGzipCrc32Table();

    private sealed class GzipModule : PyModule
    {
        public static readonly GzipModule Instance = new();

        private static readonly string[] Members = ["open", "compress", "decompress", "BadGzipFile"];

        private GzipModule() : base("gzip")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "open" => GzipOpenCallable.Instance,
                "compress" => GzipCompressCallable.Instance,
                "decompress" => new BuiltinCallable(LythonKnownCallableSignatures.GzipDecompress, Decompress),
                "BadGzipFile" => new ExceptionTypeValue("BadGzipFile"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static object Decompress(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments[0] is not PyBytes data)
            {
                throw new LythonRuntimeException("TypeError", "gzip.decompress(data) requires a bytes-like object", span);
            }

            return DecompressPayload(data.Memory, span, context);
        }

        internal static PyBytes DecompressPayload(
            ReadOnlyMemory<byte> compressed,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (compressed.IsEmpty)
            {
                return CreateBytes([], context, span);
            }

            try
            {
                var output = new GovernedByteBuilder(context.MemoryGovernor, span);
                var buffer = new byte[8192];
                var position = 0;
                var memberCount = 0;
                try
                {
                    // RFC 1952 permits concatenated members and zero padding between them.
                    // Keep their output governed and private until every trailer is validated.
                    while (position < compressed.Length)
                    {
                        while (position < compressed.Length && compressed.Span[position] == 0)
                        {
                            position++;
                        }

                        if (position == compressed.Length)
                        {
                            break;
                        }

                        position = ParseGzipHeader(compressed.Span, position, span);
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

                                output.Append(buffer.AsSpan(0, count));
                                memberCrc = UpdateGzipCrc32(memberCrc, buffer.AsSpan(0, count));
                                memberLength = unchecked(memberLength + (uint)count);
                            }

                            position = cursor.BytePosition;
                        }

                        if (compressed.Length - position < 8)
                        {
                            throw BadGzip("Compressed file ended before the end-of-stream marker was reached", span);
                        }

                        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Span.Slice(position, 4));
                        var expectedLength = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Span.Slice(position + 4, 4));
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

                    var decompressed = output.ToArrayAndRelease();
                    return CreateBytes(decompressed, context, span);
                }
                finally
                {
                    output.Release();
                }
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

    private enum GzipOperation
    {
        Read,
        Write,
        Append,
    }

    private enum GzipContentKind
    {
        Binary,
        Text,
    }

    private readonly record struct GzipOpenOptions(
        string Path,
        string Mode,
        GzipOperation Operation,
        GzipContentKind ContentKind,
        int CompressionLevel,
        TextEncodingMode Encoding,
        TextErrorMode Errors,
        TextNewlineMode Newline);

    private sealed class GzipOpenCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public static readonly GzipOpenCallable Instance = new();

        private GzipOpenCallable()
        {
        }

        public string Name => "gzip.open";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var options = ParseGzipOpenArguments(arguments, span, context);
            return OpenGzipHandle(options, context, span);
        }

        public async ValueTask<object> InvokeAsync(
            CallArgumentValue[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var options = ParseGzipOpenArguments(arguments, span, context);
            return await OpenGzipHandleAsync(options, context, span).ConfigureAwait(false);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private static GzipOpenOptions ParseGzipOpenArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArgumentsWithPresence(
            arguments,
            span,
            LythonKnownCallableSignatures.GzipOpen,
            PythonCallableKind.Builtin);
        var values = bound.Values;

        var path = PathOps.Normalize(
            CoercePathLike(values[0], context, span, "gzip.open()").AsString(),
            context.Host.Cwd);
        var mode = IsAssigned(1)
            ? PyStringOps.TryAsString(values[1], out var modeText)
                ? modeText.AsString()
                : throw new LythonRuntimeException("TypeError", "gzip.open(..., mode=...) expects a string", span)
            : "rb";
        var (operation, contentKind) = mode switch
        {
            "r" or "rb" => (GzipOperation.Read, GzipContentKind.Binary),
            "rt" => (GzipOperation.Read, GzipContentKind.Text),
            "w" or "wb" => (GzipOperation.Write, GzipContentKind.Binary),
            "wt" => (GzipOperation.Write, GzipContentKind.Text),
            "a" or "ab" => (GzipOperation.Append, GzipContentKind.Binary),
            "at" => (GzipOperation.Append, GzipContentKind.Text),
            _ when mode.Contains('+', StringComparison.Ordinal) => throw new LythonRuntimeException("NotImplementedError", "gzip.open() does not support random-access updating modes", span),
            _ when mode.StartsWith('x') => throw new LythonRuntimeException("NotImplementedError", "gzip.open() does not support exclusive-creation modes", span),
            _ => throw new LythonRuntimeException("ValueError", $"Invalid mode: '{mode}'", span),
        };
        var compressionLevel = IsAssigned(2) ? ParseCompressionLevel(values[2], span) : 9;

        if (contentKind == GzipContentKind.Binary)
        {
            if ((IsAssigned(3) && values[3] is not PyNone) ||
                (IsAssigned(4) && values[4] is not PyNone) ||
                (IsAssigned(5) && values[5] is not PyNone))
            {
                throw new LythonRuntimeException("ValueError", "Argument 'encoding', 'errors', or 'newline' not supported in binary mode", span);
            }

            return new GzipOpenOptions(
                path,
                mode,
                operation,
                GzipContentKind.Binary,
                compressionLevel,
                TextEncodingMode.Utf8,
                TextErrorMode.Strict,
                TextNewlineMode.TranslateUniversal);
        }

        var encoding = IsAssigned(3)
            ? ParseTextEncoding(values[3], "gzip.open()", span)
            : TextEncodingMode.Utf8;
        var errors = IsAssigned(4)
            ? ParseTextErrors(values[4], "gzip.open()", span)
            : TextErrorMode.Strict;
        var newline = IsAssigned(5)
            ? ParseTextNewline(values[5], "gzip.open()", span)
            : TextNewlineMode.TranslateUniversal;
        return new GzipOpenOptions(path, mode, operation, GzipContentKind.Text, compressionLevel, encoding, errors, newline);

        bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
    }

    private static object OpenGzipHandle(GzipOpenOptions options, ExecutionContext context, LythonSourceSpan span)
    {
        if (options.Operation == GzipOperation.Read)
        {
            using var compressed = ReadGovernedHostBytes(options.Path, context, span);
            var decompressed = GzipModule.DecompressPayload(compressed.Memory, span, context);
            return GzipFileHandle.ForRead(options, decompressed, context, span);
        }

        if (options.Operation == GzipOperation.Append)
        {
            context.RegisterHostCall(span);
            var stat = context.HostStat(options.Path, span);
            if (stat is { Exists: true, IsFile: true })
            {
                using var compressed = ReadGovernedHostBytes(options.Path, context, span);
                return GzipFileHandle.ForWrite(options, compressed.Memory.ToArray(), context);
            }

            return GzipFileHandle.ForWrite(options, [], context);
        }

        return GzipFileHandle.ForWrite(options, [], context);
    }

    private static async ValueTask<object> OpenGzipHandleAsync(
        GzipOpenOptions options,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (options.Operation == GzipOperation.Read)
        {
            using var compressed = await ReadGovernedHostBytesAsync(options.Path, context, span).ConfigureAwait(false);
            var decompressed = GzipModule.DecompressPayload(compressed.Memory, span, context);
            return GzipFileHandle.ForRead(options, decompressed, context, span);
        }

        if (options.Operation == GzipOperation.Append)
        {
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(options.Path, span).ConfigureAwait(false);
            if (stat is { Exists: true, IsFile: true })
            {
                using var compressed = await ReadGovernedHostBytesAsync(options.Path, context, span).ConfigureAwait(false);
                return GzipFileHandle.ForWrite(options, compressed.Memory.ToArray(), context);
            }

            return GzipFileHandle.ForWrite(options, [], context);
        }

        return GzipFileHandle.ForWrite(options, [], context);
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
                if (argument.IsPositional)
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

                switch (argument.KeywordName)
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
                        throw new LythonRuntimeException("TypeError", $"gzip.compress() got multiple values for argument '{argument.KeywordName}'", span);
                    default:
                        throw new LythonRuntimeException("TypeError", $"gzip.compress() got an unexpected keyword argument '{argument.KeywordName}'", span);
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

            var compressionLevel = levelAssigned ? ParseCompressionLevel(level.RequireNotNull(), span) : 9;
            var modificationTime = mtimeAssigned ? ParseModificationTime(mtime.RequireNotNull(), span) : 0u;
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
        LythonSourceSpan? span)
    {
        var maximumLength = checked((long)data.Length + (data.Length >> 12) + (data.Length >> 14) + (data.Length >> 25) + 31L);
        if (maximumLength > int.MaxValue)
        {
            throw RuntimeErrors.Memory("gzip compressed output is too large", span);
        }

        var output = new GovernedByteBuilder(
            context.MemoryGovernor,
            span,
            capacity: 0,
            maxLengthBytes: (int)maximumLength,
            maxLengthOwner: "gzip compressed output");
        try
        {
            using var outputStream = new GzipBufferWriteStream(output);
            Span<byte> header = stackalloc byte[10];
            header[0] = 0x1f;
            header[1] = 0x8b;
            header[2] = 8;
            header[3] = 0;
            BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], modificationTime);
            header[8] = compressionLevel switch { 1 => 4, 9 => 2, _ => 0 };
            header[9] = 255;
            outputStream.Write(header);

            var options = new ZLibCompressionOptions { CompressionLevel = compressionLevel };
            using (var deflate = new DeflateStream(outputStream, options, leaveOpen: true))
            {
                deflate.Write(data);
            }

            Span<byte> trailer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(trailer[..4], ComputeGzipCrc32(data, context, span));
            BinaryPrimitives.WriteUInt32LittleEndian(trailer[4..], unchecked((uint)data.Length));
            outputStream.Write(trailer);
            return output.ToArrayAndRelease();
        }
        finally
        {
            output.Release();
        }
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

    private static uint ComputeGzipCrc32(ReadOnlySpan<byte> data, ExecutionContext context, LythonSourceSpan? span)
    {
        var crc = uint.MaxValue;
        const int budgetChunkLength = 4096;
        for (var offset = 0; offset < data.Length; offset += budgetChunkLength)
        {
            context.CheckExecutionBudget(span);
            var length = Math.Min(budgetChunkLength, data.Length - offset);
            crc = UpdateGzipCrc32(crc, data.Slice(offset, length));
        }

        return ~crc;
    }

    private static uint UpdateGzipCrc32(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            crc = GzipCrc32Table[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildGzipCrc32Table()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var value = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value >> 1) ^ (0xedb88320u & unchecked((uint)-(int)(value & 1)));
            }

            table[index] = value;
        }

        return table;
    }

    private static int ParseGzipHeader(ReadOnlySpan<byte> data, int position, LythonSourceSpan span)
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

            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position, 2));
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

    private static int SkipGzipZeroTerminatedField(ReadOnlySpan<byte> data, int position, LythonSourceSpan span)
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

    private sealed class GzipBufferWriteStream : Stream
    {
        private readonly GovernedByteBuilder _output;

        public GzipBufferWriteStream(GovernedByteBuilder output)
        {
            _output = output;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _output.Length;
        public override long Position { get => _output.Length; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
            => _output.Append(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer) => _output.Append(buffer);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class GzipByteCursorStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _data;

        public GzipByteCursorStream(ReadOnlyMemory<byte> data, int position)
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

            buffer[offset] = _data.Span[BytePosition++];
            return 1;
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0 || BytePosition >= _data.Length)
            {
                return 0;
            }

            buffer[0] = _data.Span[BytePosition++];
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static LythonRuntimeException BadGzip(string message, LythonSourceSpan span)
        => BadGzip(message, span, null);

    private static LythonRuntimeException BadGzip(string message, LythonSourceSpan span, Exception? inner)
        => new("BadGzipFile", message, span, inner);
}
