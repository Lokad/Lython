using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class GzipFileHandle : IPyDynamicAttributes, IPyAsyncContextManager, IPyIteratorValue, IPyRenderableValue
    {
        private readonly GzipOpenOptions _options;
        private readonly ExecutionContext _context;
        private readonly PyBytes? _binaryRead;
        private readonly PyString? _textRead;
        private byte[] _compressedPrefix;
        private readonly GovernedByteBuilder _writeBuffer;
        private long _compressedPrefixCharge;
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
            _writeBuffer = new GovernedByteBuilder(context.MemoryGovernor);
            _dirty = dirty;
        }

        public bool IsClosed { get; private set; }

        public PyString ReadRemainingTextForLexer(LythonSourceSpan span)
        {
            EnsureReadable(span);
            if (_options.ContentKind != GzipContentKind.Text)
            {
                throw new LythonRuntimeException("TypeError", "shlex.shlex input must be a readable text handle, not a binary gzip handle.", span);
            }

            return (PyString)Read(-1, span);
        }

        public void ClosePushedLexerSource(LythonSourceSpan span) => Close(span);

        public static GzipFileHandle ForRead(
            GzipOpenOptions options,
            PyBytes decompressed,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (options.ContentKind != GzipContentKind.Text)
            {
                return new GzipFileHandle(options, context, decompressed, null, [], dirty: false);
            }

            var text = DecodeText(
                decompressed.Memory,
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
            // Append retains the original compressed members verbatim. Flush creates and
            // validates the new member before a single host replacement publishes either.
            if (prefix.Length > 0)
            {
                context.MemoryGovernor.Reserve(PyBytes.EstimateApproximateBytes(prefix.Length), null);
                context.MemoryGovernor.Commit(PyBytes.EstimateApproximateBytes(prefix.Length));
            }

            var handle = new GzipFileHandle(options, context, null, null, prefix, dirty: true)
            {
                _compressedPrefixCharge = prefix.Length > 0 ? PyBytes.EstimateApproximateBytes(prefix.Length) : 0,
            };
            return handle;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "name" => PyString.FromString(_options.Path),
                "mode" => PyString.FromString(_options.Mode),
                "encoding" when _options.ContentKind == GzipContentKind.Text => PyString.FromString(EncodingName),
                "errors" when _options.ContentKind == GzipContentKind.Text => PyString.FromString(ErrorsName),
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
                "readable" => NoArgumentMethod("gzip file.readable", (_, span) => { EnsureOpen(span); return _options.Operation == GzipOperation.Read; }),
                "writable" => NoArgumentMethod("gzip file.writable", (_, span) => { EnsureOpen(span); return _options.Operation is GzipOperation.Write or GzipOperation.Append; }),
                "seekable" => NoArgumentMethod("gzip file.seekable", (_, span) => { EnsureOpen(span); return false; }),
                "tell" => NoArgumentMethod("gzip file.tell", (_, span) => { EnsureOpen(span); return new BigInteger(_options.Operation == GzipOperation.Read ? _readCursor : _writeBuffer.Length); }),
                "seek" => new BoundCallable((_, span, _) => throw new LythonRuntimeException("NotImplementedError", "gzip file seek/random access is unsupported by Lython.", span), "gzip file.seek", ["offset", "whence"], 1),
                "read" => new BoundCallable((arguments, span, _) => Read(ParseOptionalSize(arguments, "gzip file.read([size])", span), span), "gzip file.read", ["size"], 0),
                "readline" => new BoundCallable((arguments, span, _) => ReadLine(ParseOptionalSize(arguments, "gzip file.readline([size])", span), span), "gzip file.readline", ["size"], 0),
                "readlines" => new BoundCallable((arguments, span, _) => ReadLines(ParseOptionalSize(arguments, "gzip file.readlines([hint])", span), span), "gzip file.readlines", ["hint"], 0),
                "write" => new BoundCallable((arguments, span, _) => Write(arguments, span), "gzip file.write", ["data"]),
                "writelines" => new BoundCallable((arguments, span, _) => WriteLines(arguments, span), "gzip file.writelines", ["lines"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
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

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
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
            if (_options.ContentKind == GzipContentKind.Text)
            {
                var text = _textRead.RequireNotNull();
                if (_readCursor >= text.Utf8Bytes.Length)
                {
                    return PyString.Empty;
                }

                var end = size < 0
                    ? text.Utf8Bytes.Length
                    : text.GetByteIndexAfterRunes(_readCursor, size);
                var result = text.SliceByByteRange(_readCursor, end);
                _readCursor = end;
                return result;
            }

            var bytes = _binaryRead.RequireNotNull();
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
            if (_options.ContentKind == GzipContentKind.Text)
            {
                var text = _textRead.RequireNotNull();
                var source = text.Utf8Bytes.Span;
                if (_readCursor >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = FindTextLineEnd(source, _readCursor);
                if (size >= 0)
                {
                    end = Math.Min(end, text.GetByteIndexAfterRunes(_readCursor, size));
                }

                var line = text.SliceByByteRange(_readCursor, end);
                _readCursor = end;
                return line;
            }

            var bytes = _binaryRead.RequireNotNull();
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

            if (_options.ContentKind == GzipContentKind.Text)
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
            _writeBuffer.Append(bytes);
            _dirty = true;
        }

        private void Flush(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == GzipOperation.Read || !_dirty || _validationFailed)
            {
                return;
            }

            var payload = CreateCompressedWritePayload(span);
            try
            {
                _context.RegisterHostCall(span);
                _context.WriteHostBytes(_options.Path, payload.Bytes, span);
                _dirty = false;
            }
            finally
            {
                _context.MemoryGovernor.Release(payload.MemoryCharge);
            }
        }

        private async ValueTask FlushAsync(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == GzipOperation.Read || !_dirty || _validationFailed)
            {
                return;
            }

            var payload = CreateCompressedWritePayload(span);
            try
            {
                _context.RegisterHostCall(span);
                await _context.WriteHostBytesAsync(_options.Path, payload.Bytes, span).ConfigureAwait(false);
                _dirty = false;
            }
            finally
            {
                _context.MemoryGovernor.Release(payload.MemoryCharge);
            }
        }

        private GzipWritePayload CreateCompressedWritePayload(LythonSourceSpan? span)
        {
            // Construct and account for the complete payload before either flush path
            // crosses the host boundary, so local failures cannot partially update a file.
            var compressed = CompressGzip(_writeBuffer.WrittenSpan, _options.CompressionLevel, 0, _context, span);
            var compressedCharge = PyBytes.EstimateApproximateBytes(compressed.Length);
            _context.MemoryGovernor.Reserve(compressedCharge, span);
            _context.MemoryGovernor.Commit(compressedCharge);
            if (_options.Operation != GzipOperation.Append || _compressedPrefix.Length == 0)
            {
                return new GzipWritePayload(compressed, compressedCharge);
            }

            var length = checked(_compressedPrefix.Length + compressed.Length);
            var payloadCharge = PyBytes.EstimateApproximateBytes(length);
            try
            {
                _context.MemoryGovernor.Reserve(payloadCharge, span);
                var payload = new byte[length];
                _compressedPrefix.CopyTo(payload, 0);
                compressed.CopyTo(payload, _compressedPrefix.Length);
                _context.MemoryGovernor.Release(compressedCharge);
                _context.MemoryGovernor.Commit(payloadCharge);
                return new GzipWritePayload(payload, payloadCharge);
            }
            catch
            {
                _context.MemoryGovernor.Release(compressedCharge);
                throw;
            }
        }

        private void Close(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            Flush(span);
            IsClosed = true;
            ReleaseWriteBuffers();
        }

        private async ValueTask CloseAsync(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                return;
            }

            await FlushAsync(span).ConfigureAwait(false);
            IsClosed = true;
            ReleaseWriteBuffers();
        }

        private void AbortClose()
        {
            IsClosed = true;
            _dirty = false;
            ReleaseWriteBuffers();
        }

        private void ReleaseWriteBuffers()
        {
            _writeBuffer.Release();
            _context.MemoryGovernor.Release(_compressedPrefixCharge);
            _compressedPrefixCharge = 0;
            _compressedPrefix = [];
        }

        private readonly record struct GzipWritePayload(byte[] Bytes, long MemoryCharge);

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
            if (_options.Operation != GzipOperation.Read)
            {
                throw new LythonRuntimeException("ValueError", "write-only gzip file", span);
            }
        }

        private void EnsureWritable(LythonSourceSpan? span)
        {
            EnsureOpen(span);
            if (_options.Operation == GzipOperation.Read)
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

}
