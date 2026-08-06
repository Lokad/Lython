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

        private static readonly string[] Members = ["open", "compress", "decompress", "BadGzipFile"];

        private GzipModule() : base("gzip")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "open" => GzipOpenCallable.Instance,
                "compress" => GzipCompressCallable.Instance,
                "decompress" => new BuiltinCallable(LythonKnownCallableSignatures.GzipDecompress, Decompress),
                "BadGzipFile" => new ExceptionTypeValue("BadGzipFile"),
                _ => null!,
            };

            return value is not null;
        }

        public static object Decompress(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

    private readonly record struct GzipOpenOptions(
        string Path,
        string Mode,
        string Operation,
        bool Text,
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
        var values = new object?[6];
        var assigned = new bool[6];
        var names = new[] { "filename", "mode", "compresslevel", "encoding", "errors", "newline" };
        var positional = 0;
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positional >= values.Length)
                {
                    throw new LythonRuntimeException("TypeError", "gzip.open() accepts at most six positional arguments", span);
                }

                values[positional] = argument.Value;
                assigned[positional] = true;
                positional++;
                continue;
            }

            var index = Array.IndexOf(names, argument.Name);
            if (index < 0)
            {
                throw new LythonRuntimeException("TypeError", $"gzip.open() got an unexpected keyword argument '{argument.Name}'", span);
            }

            if (assigned[index])
            {
                throw new LythonRuntimeException("TypeError", $"gzip.open() got multiple values for argument '{argument.Name}'", span);
            }

            values[index] = argument.Value;
            assigned[index] = true;
        }

        if (!assigned[0])
        {
            throw new LythonRuntimeException("TypeError", "gzip.open() missing required argument 'filename'", span);
        }

        var path = PathOps.Normalize(
            CoercePathLike(values[0]!, context, span, "gzip.open()").AsString(),
            context.Host.Cwd);
        var mode = assigned[1]
            ? PyStringOps.TryAsString(values[1]!, out var modeText)
                ? modeText.AsString()
                : throw new LythonRuntimeException("TypeError", "gzip.open(..., mode=...) expects a string", span)
            : "rb";
        var (operation, text) = mode switch
        {
            "r" or "rb" => ("r", false),
            "rt" => ("r", true),
            "w" or "wb" => ("w", false),
            "wt" => ("w", true),
            "a" or "ab" => ("a", false),
            "at" => ("a", true),
            _ when mode.Contains('+', StringComparison.Ordinal) => throw new LythonRuntimeException("NotImplementedError", "gzip.open() does not support random-access updating modes", span),
            _ when mode.StartsWith('x') => throw new LythonRuntimeException("NotImplementedError", "gzip.open() does not support exclusive-creation modes", span),
            _ => throw new LythonRuntimeException("ValueError", $"Invalid mode: '{mode}'", span),
        };
        var compressionLevel = assigned[2] ? ParseCompressionLevel(values[2]!, span) : 9;

        if (!text)
        {
            if ((assigned[3] && values[3] is not PyNone) ||
                (assigned[4] && values[4] is not PyNone) ||
                (assigned[5] && values[5] is not PyNone))
            {
                throw new LythonRuntimeException("ValueError", "Argument 'encoding', 'errors', or 'newline' not supported in binary mode", span);
            }

            return new GzipOpenOptions(
                path,
                mode,
                operation,
                Text: false,
                compressionLevel,
                TextEncodingMode.Utf8,
                TextErrorMode.Strict,
                TextNewlineMode.TranslateUniversal);
        }

        var encoding = assigned[3]
            ? ParseTextEncoding(values[3]!, "gzip.open()", span)
            : TextEncodingMode.Utf8;
        var errors = assigned[4]
            ? ParseTextErrors(values[4]!, "gzip.open()", span)
            : TextErrorMode.Strict;
        var newline = assigned[5]
            ? ParseTextNewline(values[5]!, "gzip.open()", span)
            : TextNewlineMode.TranslateUniversal;
        return new GzipOpenOptions(path, mode, operation, Text: true, compressionLevel, encoding, errors, newline);
    }

    private static object OpenGzipHandle(GzipOpenOptions options, ExecutionContext context, LythonSourceSpan span)
    {
        if (options.Operation == "r")
        {
            var compressed = ReadGovernedHostBytes(options.Path, context, span);
            var decompressed = (PyBytes)GzipModule.Decompress(
                [new PyBytes(compressed.ToArray())],
                span,
                context);
            return GzipFileHandle.ForRead(options, decompressed, context, span);
        }

        if (options.Operation == "a")
        {
            context.RegisterHostCall(span);
            var stat = context.HostStat(options.Path, span);
            var prefix = stat is { Exists: true, IsFile: true }
                ? ReadGovernedHostBytes(options.Path, context, span).ToArray()
                : [];
            return GzipFileHandle.ForWrite(options, prefix, context);
        }

        return GzipFileHandle.ForWrite(options, [], context);
    }

    private static async ValueTask<object> OpenGzipHandleAsync(
        GzipOpenOptions options,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (options.Operation == "r")
        {
            var compressed = await ReadGovernedHostBytesAsync(options.Path, context, span).ConfigureAwait(false);
            var decompressed = (PyBytes)GzipModule.Decompress(
                [new PyBytes(compressed.ToArray())],
                span,
                context);
            return GzipFileHandle.ForRead(options, decompressed, context, span);
        }

        if (options.Operation == "a")
        {
            context.RegisterHostCall(span);
            var stat = await context.HostStatAsync(options.Path, span).ConfigureAwait(false);
            var prefix = stat is { Exists: true, IsFile: true }
                ? (await ReadGovernedHostBytesAsync(options.Path, context, span).ConfigureAwait(false)).ToArray()
                : [];
            return GzipFileHandle.ForWrite(options, prefix, context);
        }

        return GzipFileHandle.ForWrite(options, [], context);
    }

    private sealed class GzipFileHandle : IPyDynamicAttributes, IPyAsyncContextManager, IPyIteratorValue, IPyRenderableValue
    {
        private readonly GzipOpenOptions _options;
        private readonly ExecutionContext _context;
        private readonly PyBytes? _binaryRead;
        private readonly PyString? _textRead;
        private readonly byte[] _compressedPrefix;
        private byte[] _writeBuffer = [];
        private int _readCursor;
        private bool _dirty;
        private bool _validationFailed;
        private bool _textBomWritten;

        private GzipFileHandle(
            GzipOpenOptions options,
            ExecutionContext context,
            PyBytes? binaryRead,
            PyString? textRead,
            byte[] compressedPrefix,
            bool dirty)
        {
            _options = options;
            _context = context;
            _binaryRead = binaryRead;
            _textRead = textRead;
            _compressedPrefix = compressedPrefix;
            _dirty = dirty;
        }

        public bool IsClosed { get; private set; }

        public static GzipFileHandle ForRead(
            GzipOpenOptions options,
            PyBytes decompressed,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (!options.Text)
            {
                return new GzipFileHandle(options, context, decompressed, null, [], dirty: false);
            }

            var text = DecodeText(
                decompressed.ToArray(),
                options.Encoding,
                context,
                span,
                options.Errors,
                options.Newline);
            context.ObserveString(text, span);
            return new GzipFileHandle(options, context, null, text, [], dirty: false);
        }

        public static GzipFileHandle ForWrite(GzipOpenOptions options, byte[] prefix, ExecutionContext context)
        {
            if (prefix.Length > 0)
            {
                context.MemoryGovernor.Reserve(PyBytes.EstimateApproximateBytes(prefix.Length), null);
                context.MemoryGovernor.Commit(PyBytes.EstimateApproximateBytes(prefix.Length));
            }

            return new GzipFileHandle(options, context, null, null, prefix, dirty: true);
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "name" => PyString.FromString(_options.Path),
                "mode" => PyString.FromString(_options.Mode),
                "encoding" when _options.Text => PyString.FromString(EncodingName),
                "errors" when _options.Text => PyString.FromString(ErrorsName),
                "__enter__" => new BoundCallable((arguments, span, _) =>
                {
                    RequireNoArguments(arguments, "gzip file __enter__()", span);
                    EnsureOpen(span);
                    return this;
                }, "gzip file.__enter__", []),
                "__exit__" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "gzip file __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    if (arguments[0] is PyNone)
                    {
                        Close(span);
                    }
                    else
                    {
                        AbortClose();
                    }
                    return false;
                },
                async (arguments, span, _) =>
                {
                    if (arguments.Length != 3)
                    {
                        throw new LythonRuntimeException("TypeError", "gzip file __exit__(exc_type, exc, tb) expects three arguments", span);
                    }

                    if (arguments[0] is PyNone)
                    {
                        await CloseAsync(span).ConfigureAwait(false);
                    }
                    else
                    {
                        AbortClose();
                    }
                    return false;
                }),
                "close" => new BoundCallable((arguments, span, _) =>
                {
                    RequireNoArguments(arguments, "gzip file.close()", span);
                    Close(span);
                    return PyNone.Instance;
                },
                async (arguments, span, _) =>
                {
                    RequireNoArguments(arguments, "gzip file.close()", span);
                    await CloseAsync(span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "flush" => new BoundCallable((arguments, span, _) =>
                {
                    RequireNoArguments(arguments, "gzip file.flush()", span);
                    Flush(span);
                    return PyNone.Instance;
                },
                async (arguments, span, _) =>
                {
                    RequireNoArguments(arguments, "gzip file.flush()", span);
                    await FlushAsync(span).ConfigureAwait(false);
                    return PyNone.Instance;
                }),
                "readable" => NoArgumentMethod("gzip file.readable", (_, span) => { EnsureOpen(span); return _options.Operation == "r"; }),
                "writable" => NoArgumentMethod("gzip file.writable", (_, span) => { EnsureOpen(span); return _options.Operation is "w" or "a"; }),
                "seekable" => NoArgumentMethod("gzip file.seekable", (_, span) => { EnsureOpen(span); return false; }),
                "tell" => NoArgumentMethod("gzip file.tell", (_, span) => { EnsureOpen(span); return new BigInteger(_options.Operation == "r" ? _readCursor : _writeBuffer.Length); }),
                "seek" => new BoundCallable((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "gzip file seek/random access is unsupported by Lython.", span), "gzip file.seek", ["offset", "whence"], 1),
                "read" => new BoundCallable((arguments, span, _) => Read(ParseOptionalSize(arguments, "gzip file.read([size])", span), span), "gzip file.read", ["size"], 0),
                "readline" => new BoundCallable((arguments, span, _) => ReadLine(ParseOptionalSize(arguments, "gzip file.readline([size])", span), span), "gzip file.readline", ["size"], 0),
                "readlines" => new BoundCallable((arguments, span, _) => ReadLines(ParseOptionalSize(arguments, "gzip file.readlines([hint])", span), span), "gzip file.readlines", ["hint"], 0),
                "write" => new BoundCallable((arguments, span, _) => Write(arguments, span), "gzip file.write", ["data"]),
                "writelines" => new BoundCallable((arguments, span, _) => WriteLines(arguments, span), "gzip file.writelines", ["lines"]),
                _ => null!,
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public object Enter() => this;

        object IPyContextManager.Enter() => Enter();

        ValueTask<object> IPyAsyncContextManager.EnterAsync() => ValueTask.FromResult<object>(Enter());

        bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionType is PyNone)
            {
                Close(null);
            }
            else
            {
                AbortClose();
            }
            return false;
        }

        async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionType is PyNone)
            {
                await CloseAsync(null).ConfigureAwait(false);
            }
            else
            {
                AbortClose();
            }
            return false;
        }

        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value))
            {
                yield return value;
            }
        }

        public bool TryMoveNext(out object value)
        {
            var line = ReadLine(-1, null);
            if (IsEmptyReadValue(line))
            {
                value = PyNone.Instance;
                return false;
            }

            value = line;
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<gzip file '{_options.Path}' mode '{_options.Mode}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Read(int size, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            if (_options.Text)
            {
                var text = _textRead!;
                if (_readCursor >= text.Utf8Bytes.Length)
                {
                    return PyString.Empty;
                }

                var end = size < 0
                    ? text.Utf8Bytes.Length
                    : text.GetByteIndexForRuneBoundary(text.ByteIndexToRuneIndex(_readCursor) + size);
                var result = text.SliceByByteRange(_readCursor, end);
                _readCursor = end;
                return result;
            }

            var bytes = _binaryRead!;
            if (_readCursor >= bytes.Length)
            {
                return CreateBytes([], _context, span);
            }

            var binaryEnd = size < 0 ? bytes.Length : Math.Min(bytes.Length, _readCursor + size);
            var binary = bytes.Bytes.Slice(_readCursor, binaryEnd - _readCursor).ToArray();
            _readCursor = binaryEnd;
            return CreateBytes(binary, _context, span);
        }

        private object ReadLine(int size, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            if (_options.Text)
            {
                var text = _textRead!;
                var source = text.Utf8Bytes.Span;
                if (_readCursor >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = FindTextLineEnd(source, _readCursor);
                if (size >= 0)
                {
                    end = Math.Min(end, text.GetByteIndexForRuneBoundary(text.ByteIndexToRuneIndex(_readCursor) + size));
                }

                var line = text.SliceByByteRange(_readCursor, end);
                _readCursor = end;
                return line;
            }

            var bytes = _binaryRead!;
            if (_readCursor >= bytes.Length)
            {
                return CreateBytes([], _context, span);
            }

            var binaryEnd = _readCursor;
            while (binaryEnd < bytes.Length && bytes.Bytes[binaryEnd++] != (byte)'\n')
            {
            }

            if (size >= 0)
            {
                binaryEnd = Math.Min(binaryEnd, _readCursor + size);
            }

            var lineBytes = bytes.Bytes.Slice(_readCursor, binaryEnd - _readCursor).ToArray();
            _readCursor = binaryEnd;
            return CreateBytes(lineBytes, _context, span);
        }

        private object ReadLines(int hint, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            var lines = new List<object>();
            var total = 0;
            while (true)
            {
                var line = ReadLine(-1, span);
                if (IsEmptyReadValue(line))
                {
                    break;
                }

                lines.Add(line);
                total += line is PyString text ? text.Utf8Bytes.Length : ((PyBytes)line).Length;
                if (hint > 0 && total > hint)
                {
                    break;
                }
            }

            return new PyList(lines, _context.MemoryGovernor, span);
        }

        private object Write(object[] arguments, LythonSourceSpan span)
        {
            EnsureWritable(span);
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "gzip file.write(data) expects one argument", span);
            }

            if (_options.Text)
            {
                if (!PyStringOps.TryAsString(arguments[0], out var text))
                {
                    _validationFailed = true;
                    throw new LythonRuntimeException("TypeError", "write() argument must be str, not bytes", span);
                }

                byte[] encoded;
                try
                {
                    var writeEncoding = _options.Encoding == TextEncodingMode.Utf8Bom
                        ? TextEncodingMode.Utf8
                        : _options.Encoding;
                    encoded = EncodeText(text, writeEncoding, _options.Errors, _options.Newline, _context, span);
                    if (_options.Encoding == TextEncodingMode.Utf8Bom && !_textBomWritten)
                    {
                        encoded = [0xef, 0xbb, 0xbf, .. encoded];
                    }
                }
                catch
                {
                    _validationFailed = true;
                    throw;
                }
                AppendWriteBytes(encoded, span);
                _textBomWritten = _options.Encoding == TextEncodingMode.Utf8Bom || _textBomWritten;
                return new BigInteger(text.Length);
            }

            if (arguments[0] is not PyBytes bytes)
            {
                _validationFailed = true;
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required", span);
            }

            AppendWriteBytes(bytes.Bytes, span);
            return new BigInteger(bytes.Length);
        }

        private object WriteLines(object[] arguments, LythonSourceSpan span)
        {
            EnsureWritable(span);
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "gzip file.writelines(lines) expects one iterable", span);
            }

            foreach (var item in ToSequence(arguments[0], span))
            {
                _ = Write([item], span);
            }

            return PyNone.Instance;
        }

        private void AppendWriteBytes(ReadOnlySpan<byte> bytes, LythonSourceSpan span)
        {
            var newLength = checked(_writeBuffer.Length + bytes.Length);
            _context.MemoryGovernor.Reserve(bytes.Length, span);
            _context.MemoryGovernor.Commit(bytes.Length);
            var combined = new byte[newLength];
            _writeBuffer.CopyTo(combined, 0);
            bytes.CopyTo(combined.AsSpan(_writeBuffer.Length));
            _writeBuffer = combined;
            _dirty = true;
        }

        private void Flush(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == "r" || !_dirty || _validationFailed)
            {
                return;
            }

            var payload = CreateCompressedWritePayload(span);
            _context.RegisterHostCall(span);
            _context.WriteHostBytes(_options.Path, payload, span);
            _dirty = false;
        }

        private async ValueTask FlushAsync(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == "r" || !_dirty || _validationFailed)
            {
                return;
            }

            var payload = CreateCompressedWritePayload(span);
            _context.RegisterHostCall(span);
            await _context.WriteHostBytesAsync(_options.Path, payload, span).ConfigureAwait(false);
            _dirty = false;
        }

        private byte[] CreateCompressedWritePayload(LythonSourceSpan? span)
        {
            var compressed = CompressGzip(_writeBuffer, _options.CompressionLevel, 0, _context, span);
            if (_options.Operation != "a" || _compressedPrefix.Length == 0)
            {
                return compressed;
            }

            var length = checked(_compressedPrefix.Length + compressed.Length);
            _context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
            var payload = new byte[length];
            _compressedPrefix.CopyTo(payload, 0);
            compressed.CopyTo(payload, _compressedPrefix.Length);
            return payload;
        }

        private void Close(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            Flush(span);
            IsClosed = true;
        }

        private async ValueTask CloseAsync(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            await FlushAsync(span).ConfigureAwait(false);
            IsClosed = true;
        }

        private void AbortClose()
        {
            IsClosed = true;
            _dirty = false;
            _context.MemoryGovernor.Release(_writeBuffer.Length);
            _writeBuffer = [];
        }

        private int FindTextLineEnd(ReadOnlySpan<byte> source, int start)
        {
            for (var i = start; i < source.Length; i++)
            {
                if (source[i] == (byte)'\n' &&
                    _options.Newline is TextNewlineMode.TranslateUniversal or TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed)
                {
                    return i + 1;
                }

                if (source[i] != (byte)'\r')
                {
                    continue;
                }

                if (_options.Newline == TextNewlineMode.PreserveCarriageReturn)
                {
                    return i + 1;
                }

                if (_options.Newline == TextNewlineMode.PreserveCarriageReturnLineFeed)
                {
                    if (i + 1 < source.Length && source[i + 1] == (byte)'\n')
                    {
                        return i + 2;
                    }

                    continue;
                }

                if (_options.Newline == TextNewlineMode.PreserveUniversal)
                {
                    return i + 1 < source.Length && source[i + 1] == (byte)'\n' ? i + 2 : i + 1;
                }
            }

            return source.Length;
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }
        }

        private void EnsureReadable(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation != "r")
            {
                throw new LythonRuntimeException("ValueError", "write-only gzip file", span);
            }
        }

        private void EnsureWritable(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == "r")
            {
                throw new LythonRuntimeException("ValueError", "read-only gzip file", span);
            }
        }

        private BoundCallable NoArgumentMethod(string name, Func<object[], LythonSourceSpan, object> implementation)
            => new((arguments, span, _) =>
            {
                RequireNoArguments(arguments, name + "()", span);
                return implementation(arguments, span);
            }, name, []);

        private static void RequireNoArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects no arguments", span);
            }
        }

        private static int ParseOptionalSize(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                return -1;
            }

            if (arguments.Length != 1 || arguments[0] is not BigInteger integer || integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects an integer", span);
            }

            return (int)integer;
        }

        private static bool IsEmptyReadValue(object value)
            => value is PyString text ? text.Length == 0 : value is PyBytes bytes && bytes.Length == 0;

        private string EncodingName => _options.Encoding switch
        {
            TextEncodingMode.Utf8Bom => "utf-8-sig",
            TextEncodingMode.Latin1 => "iso8859-1",
            _ => "utf-8",
        };

        private string ErrorsName => _options.Errors switch
        {
            TextErrorMode.Ignore => "ignore",
            TextErrorMode.Replace => "replace",
            TextErrorMode.BackslashReplace => "backslashreplace",
            _ => "strict",
        };
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
        LythonSourceSpan? span)
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

    private static uint ComputeGzipCrc32(ReadOnlySpan<byte> data, ExecutionContext context, LythonSourceSpan? span)
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
