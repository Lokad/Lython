using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileHandle : IPyDynamicAttributes, IPyAsyncContextManager, IPyIteratorValue, IPyRenderableValue
    {
        private interface IGzipContent { }

        private sealed record BinaryReadContent(PyBytes Value) : IGzipContent;

        private sealed record TextReadContent(PyString Value) : IGzipContent;

        private sealed class WritableContent : IGzipContent
        {
            public static readonly WritableContent Instance = new();

            private WritableContent() { }
        }

        private readonly GzipOpenOptions _options;
        private readonly ExecutionContext _context;
        private readonly IGzipContent _content;
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
            IGzipContent content,
            byte[] compressedPrefix,
            bool dirty)
        {
            _options = options;
            _context = context;
            _content = content;
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
                return new GzipFileHandle(options, context, new BinaryReadContent(decompressed), [], dirty: false);
            }

            var text = DecodeText(
                decompressed.Memory,
                options.Encoding,
                context,
                span,
                options.Errors,
                options.Newline);
            context.ObserveString(text, span);
            return new GzipFileHandle(options, context, new TextReadContent(text), [], dirty: false);
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

            var handle = new GzipFileHandle(options, context, WritableContent.Instance, prefix, dirty: true)
            {
                _compressedPrefixCharge = prefix.Length > 0 ? PyBytes.EstimateApproximateBytes(prefix.Length) : 0,
            };
            return handle;
        }

        private object Read(int size, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            if (_content is TextReadContent textContent)
            {
                var text = textContent.Value;
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

            var bytes = ((BinaryReadContent)_content).Value;
            if (_readCursor >= bytes.Length)
            {
                return CreateBytes([], _context, span);
            }

            var remaining = bytes.Length - _readCursor;
            var take = size < 0 ? remaining : Math.Min(remaining, size);
            var binaryEnd = _readCursor + take;
            var length = binaryEnd - _readCursor;
            // Cover the transient copy as well as the owned result that
            // CreateBytes charges; the reservation is released only after
            // ownership transfers, with no allocation in between.
            using (_context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(length), span))
            {
                var binary = bytes.Bytes.Slice(_readCursor, length).ToArray();
                _readCursor = binaryEnd;
                return CreateBytes(binary, _context, span);
            }
        }

        private object ReadLine(int size, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            if (_content is TextReadContent textContent)
            {
                var text = textContent.Value;
                var source = text.Utf8Bytes.Span;
                if (_readCursor >= source.Length)
                {
                    return PyString.Empty;
                }

                var end = TextLineScanning.FindLineEndByte(source, _readCursor, _options.Newline);
                if (size >= 0)
                {
                    end = Math.Min(end, text.GetByteIndexAfterRunes(_readCursor, size));
                }

                var line = text.SliceByByteRange(_readCursor, end);
                _readCursor = end;
                return line;
            }

            var bytes = ((BinaryReadContent)_content).Value;
            if (_readCursor >= bytes.Length)
            {
                return CreateBytes([], _context, span);
            }

            // Plain line-feed scanning, shared with text readers and ZIP members.
            var binaryEnd = TextLineScanning.FindLineEndByte(bytes.Bytes, _readCursor, TextNewlineMode.PreserveLineFeed);

            if (size >= 0)
            {
                var remainingBytes = bytes.Length - _readCursor;
                var take = Math.Min(size, remainingBytes);
                var maxEnd = _readCursor + take;
                binaryEnd = Math.Min(binaryEnd, maxEnd);
            }

            var lineLength = binaryEnd - _readCursor;
            using (_context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(lineLength), span))
            {
                var lineBytes = bytes.Bytes.Slice(_readCursor, lineLength).ToArray();
                _readCursor = binaryEnd;
                return CreateBytes(lineBytes, _context, span);
            }
        }

        private object ReadLines(int hint, LythonSourceSpan? span)
        {
            EnsureReadable(span);
            // Accumulate directly in the governed result so growth is charged
            // as it happens; no second copy is needed on return.
            var lines = new PyList([], _context.MemoryGovernor, span);
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
                _context.ObserveCollectionCount(lines.Count, span);
                if ((lines.Count & 63) == 0)
                {
                    _context.CheckExecutionBudget(span);
                }

                if (hint > 0 && total > hint)
                {
                    break;
                }
            }

            return lines;
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

        private object WriteLines(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureWritable(span);
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "gzip file.writelines(lines) expects one iterable", span);
            }

            foreach (var item in ToSequence(arguments[0], span, context))
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

        /// <summary>
        /// Finalizes staged writes: validation failures must
        /// go through <see cref="AbortClose"/> instead; unrelated body errors still
        /// publish valid writes while propagating; host or budget failures propagate
        /// with the handle left open and staged output retained for retry. Idempotent.
        /// </summary>
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

        /// <summary>Asynchronous <see cref="Close"/> with identical finalization states.</summary>
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

        /// <summary>
        /// Discards staged output after a validation failure without publishing.
        /// Only validation failures abort; host or budget failures keep the staged
        /// output for an explicit retry instead.
        /// </summary>
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

        private static int ParseOptionalSize(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length == 0 || arguments[0] is null or PyNone)
            {
                return -1;
            }

            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects an integer", span);
            }

            var candidate = arguments[0];
            if (candidate is PyInstance indexInstance)
            {
                candidate = CoerceGzipIndexProtocol(indexInstance, context, span, owner);
            }

            if (!Numbers.PyNumberOps.TryAsInteger(candidate, out var integer) || integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects an integer", span);
            }

            return (int)integer;
        }

        private static object CoerceGzipIndexProtocol(PyInstance instance, ExecutionContext context, LythonSourceSpan span, string owner)
        {
            if (!instance.TryGetAttribute("__index__", context, span, out var member) || member is not ICallable callable)
            {
                return instance;
            }

            var converted = callable.Invoke([], span, context);
            if (!Numbers.PyNumberOps.TryAsInteger(converted, out _))
            {
                throw new LythonRuntimeException("TypeError", "__index__ returned non-int", span);
            }

            return converted;
        }

        private static bool IsEmptyReadValue(object value)
            => value is PyString text ? text.Length == 0 : value is PyBytes bytes && bytes.Length == 0;

        private string EncodingName => _options.Encoding switch
        {
            TextEncodingMode.Utf8Bom => "utf-8-sig",
            TextEncodingMode.Latin1 => "iso8859-1",
            _ => "utf-8",
        };

        private string ErrorsName => TextErrorName(_options.Errors);
    }

}
