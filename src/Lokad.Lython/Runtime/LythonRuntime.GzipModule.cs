using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class GzipModule : PyModule
    {
        public static readonly GzipModule Instance = new();
        private static readonly ExceptionTypeValue BadGzipType = new(ModuleException("gzip", "BadGzipFile"));

        private static readonly string[] Members = ["open", "GzipFile", "compress", "decompress", "BadGzipFile"];

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
                "GzipFile" => GzipFileClass.Instance,
                "compress" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.GzipCompress,
                    (args, span, context) => CompressAsync(args, span, context, false).GetAwaiter().GetResult(),
                    (args, span, context) => CompressAsync(args, span, context, true)),
                "decompress" => BuiltinCallable.Create(LythonKnownCallableSignatures.GzipDecompress, Decompress),
                "BadGzipFile" => BadGzipType,
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
                using var scratch = context.MemoryGovernor.ReserveTemporary(ZlibModule.InflationScratchBytes, span);
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
                            if ((position & 1023) == 0) context.CheckExecution(span);
                            position++;
                        }

                        if (position == compressed.Length)
                        {
                            break;
                        }

                        position = ParseGzipHeader(compressed.Span, position, context, span);
                        var memberCrc = uint.MaxValue;
                        uint memberLength = 0;
                        using (var cursor = new GzipByteCursorStream(compressed, position, context, span))
                        {
                            using var deflate = new DeflateStream(cursor, CompressionMode.Decompress, leaveOpen: true);
                            while (true)
                            {
                                context.CheckExecution(span);
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
                                memberCrc = Crc32.Update(memberCrc, buffer.AsSpan(0, count));
                                memberLength = unchecked(memberLength + (uint)count);
                            }

                            position = cursor.BytePosition;
                        }

                        if (compressed.Length - position < 8)
                        {
                            throw GzipEnd(span);
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

                    return output.ToPyBytesAndRelease();
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

    private sealed class GzipOpenCallable : BoundArgumentsCallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        internal static readonly GzipOpenCallable Instance = new();
        private GzipOpenCallable() : base(LythonKnownCallableSignatures.GzipOpen, PythonCallableKind.Builtin) { }
        public string Name => "gzip.open";
        protected override bool PreservePresence => true;
        protected override object InvokeBound(object[] args, LythonSourceSpan span, ExecutionContext context)
            => throw new InvalidOperationException("gzip.open requires presence");
        protected override object InvokeBoundWithPresence(BoundCallArguments args, LythonSourceSpan span, ExecutionContext context)
            => OpenAsync(args, span, context, false).GetAwaiter().GetResult();
        protected override ValueTask<object> InvokeBoundWithPresenceAsync(BoundCallArguments args, LythonSourceSpan span, ExecutionContext context)
            => OpenAsync(args, span, context, true);

        private static async ValueTask<object> OpenAsync(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var args = bound.Values;
            var requestedMode = !bound.Assigned[1] ? PyString.FromString("rb") : PyStringOps.TryAsString(args[1], out var text)
                ? text : throw RuntimeErrors.Type("gzip.open mode must be str", span);
            using var modeStorage = context.MemoryGovernor.ReserveTemporary(128L + 6L * requestedMode.Utf8Bytes.Length, span);
            var mode = requestedMode.AsString();
            if (mode.Contains('t') && mode.Contains('b')) throw RuntimeErrors.Value("Invalid mode: " + mode, span);
            var binaryMode = mode.Replace("t", "", StringComparison.Ordinal);
            _ = GzipFileOperation(binaryMode, span, ownsStream: true);
            var isText = mode.Contains('t');
            if (!isText && (args[3] is not PyNone || args[4] is not PyNone || args[5] is not PyNone))
                throw RuntimeErrors.Value("Argument 'encoding', 'errors', or 'newline' not supported in binary mode", span);
            var encoding = isText ? ParseTextEncoding(args[3], "gzip.open", span) : TextEncodingMode.Utf8;
            var errors = isText ? ParseTextErrors(args[4], "gzip.open", span) : TextErrorMode.Strict;
            var newline = isText ? ParseTextNewline(args[5], "gzip.open", span) : TextNewlineMode.TranslateUniversal;
            // Both public factories share the existing native constructor and
            // execution writer owner. No copy of a compressed prefix is needed.
            var nativeArguments = new object[] { args[0], PyString.FromString(binaryMode),
                bound.Assigned[2] ? args[2] : new BigInteger(9), PyNone.Instance, PyNone.Instance };
            var assigned = new ArgumentPresence(5);
            assigned[0] = assigned[1] = assigned[2] = true;
            using var facade = context.MemoryGovernor.ReserveTemporary(isText ? GzipTextFile.ShellBytes + ExecutionContext.ChunkedTextFileReadState.ShellBytes : 0, span);
            var binary = (GzipFileObject)await GzipFileClass.CreateAsync(new(nativeArguments, assigned), span, context, asynchronous).ConfigureAwait(false);
            if (!isText) return binary;
            try { return new GzipTextFile(binary, encoding, errors, newline, requestedMode, context, span); }
            catch { await binary.CloseFromRuntimeAsync(context, span, asynchronous).ConfigureAwait(false); throw; }
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    private static async ValueTask<object> CompressAsync(BoundCallArguments bound, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        if (bound.Values[0] is not PyBytes bytes) throw RuntimeErrors.Type("gzip.compress requires bytes", span);
        var level = bound.Assigned[1] ? await CoerceIoIntegerAsync(bound.Values[1], span, context, asynchronous).ConfigureAwait(false) : 9;
        if (level < int.MinValue || level > int.MaxValue) throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span);
        if (level is < -1 or > 9) throw RuntimeErrors.Value("Bad compression level", span);
        var time = await GzipTimeAsync(bound.Values[2], context, span, asynchronous).ConfigureAwait(false);
        if (time < 0 || time > uint.MaxValue) throw new LythonRuntimeException("OverflowError", "gzip mtime must be between 0 and 4294967295", span);
        return CompressGzip(bytes.Bytes, (int)level, (uint)time, context.Services, span);
    }

    private static async ValueTask<BigInteger> GzipTimeAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        if (value is PyNone)
        {
            context.CheckExecution(span);
            return new BigInteger(context.Host.UtcNow.ToUnixTimeSeconds());
        }
        return (BigInteger)(asynchronous ? await IntAsync([value], span, context).ConfigureAwait(false) : Int([value], span, context));
    }

    private static LythonRuntimeException GzipEnd(LythonSourceSpan span)
        => new("EOFError", "Compressed file ended before the end-of-stream marker was reached", span);

    private static PyBytes CompressGzip(
        ReadOnlySpan<byte> data,
        int compressionLevel,
        uint modificationTime,
        ExecutionServices context,
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
            capacity: 0);
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
            using (var scratch = context.MemoryGovernor.ReserveTemporary(ZlibModule.CompressionScratchBytes, span))
            using (var deflate = new DeflateStream(outputStream, options, leaveOpen: true))
            {
                for (var offset = 0; offset < data.Length;)
                {
                    context.CheckExecution(span);
                    var count = Math.Min(65536, data.Length - offset);
                    deflate.Write(data.Slice(offset, count));
                    offset += count;
                }
            }
            // The CLR stream emits no DEFLATE block when it receives no input.
            // A complete empty member still needs a final block before its trailer.
            if (data.IsEmpty) WriteEmptyDeflate(outputStream, compressionLevel);

            Span<byte> trailer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(trailer[..4], Crc32.Compute(data, context, span));
            BinaryPrimitives.WriteUInt32LittleEndian(trailer[4..], unchecked((uint)data.Length));
            outputStream.Write(trailer);
            return output.ToPyBytesAndRelease();
        }
        finally
        {
            output.Release();
        }
    }

    private static void WriteEmptyDeflate(Stream output, int compressionLevel)
    {
        ReadOnlySpan<byte> emptyStored = [1, 0, 0, 255, 255];
        ReadOnlySpan<byte> emptyFixed = [3, 0];
        output.Write(compressionLevel == 0 ? emptyStored : emptyFixed);
    }

    private static int ParseGzipHeader(ReadOnlySpan<byte> data, int position, ExecutionContext context, LythonSourceSpan span)
    {
        if (data.Length - position < 2 || data[position] != 0x1f || data[position + 1] != 0x8b)
            throw BadGzip("Not a gzipped file", span);
        if (data.Length - position < 10) throw GzipEnd(span);
        if (data[position + 2] != 8) throw BadGzip("Unknown compression method", span);

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
                throw GzipEnd(span);
            }

            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position, 2));
            position += 2;
            if (data.Length - position < extraLength)
            {
                throw GzipEnd(span);
            }

            position += extraLength;
        }

        if ((flags & 0x08) != 0)
        {
            position = SkipGzipZeroTerminatedField(data, position, context, span);
        }

        if ((flags & 0x10) != 0)
        {
            position = SkipGzipZeroTerminatedField(data, position, context, span);
        }

        if ((flags & 0x02) != 0)
        {
            if (data.Length - position < 2)
            {
                throw GzipEnd(span);
            }

            position += 2;
        }

        return position;
    }

    private static int SkipGzipZeroTerminatedField(ReadOnlySpan<byte> data, int position, ExecutionContext context, LythonSourceSpan span)
    {
        while (position < data.Length)
        {
            if ((position & 1023) == 0) context.CheckExecution(span);
            if (data[position++] == 0)
            {
                return position;
            }
        }

        throw GzipEnd(span);
    }

    internal sealed class GzipBufferWriteStream : Stream
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
        private readonly ExecutionContext? _context;
        private readonly LythonSourceSpan? _span;

        public GzipByteCursorStream(ReadOnlyMemory<byte> data, int position,
            ExecutionContext? context = null, LythonSourceSpan? span = null)
        {
            _data = data;
            _context = context;
            _span = span;
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

            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0 || BytePosition >= _data.Length)
            {
                return 0;
            }

            if ((BytePosition & 255) == 0) _context?.CheckExecution(_span);
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
        => new(ModuleException("gzip", "BadGzipFile"), message, span, inner, null);
}
