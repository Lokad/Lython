using System.Collections;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PopenInputStream : IPyDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue
    {
        private readonly PyPopen _owner;
        private readonly ExecutionContext _context;
        private readonly GovernedByteBuilder _buffer;

        public PopenInputStream(PyPopen owner, ExecutionContext context)
        {
            _owner = owner;
            _context = context;
            _buffer = new GovernedByteBuilder(
                context.MemoryGovernor,
                allocationSpan: null,
                capacity: 0);
        }

        public bool IsClosed { get; private set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "encoding" when _owner.IsText => PyString.FromString(_owner.EncodingName),
                "errors" when _owner.IsText => PyString.FromString(_owner.ErrorsName),
                "write" => BoundCallable.Create((arguments, span, _) => WriteBound(arguments, span), "Popen.stdin.write", ["s"]),
                "writelines" => BoundCallable.Create(WriteLines, WriteLinesAsync, "Popen.stdin.writelines", ["lines"]),
                "flush" => BoundCallable.Create((arguments, span, _) => Flush(arguments, span), "Popen.stdin.flush", []),
                "close" => BoundCallable.Create((arguments, span, _) => CloseBound(arguments, span), "Popen.stdin.close", []),
                "writable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, writable: true), "Popen.stdin.writable", []),
                "readable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.readable", []),
                "seekable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.seekable", []),
                "isatty" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public BigInteger Write(object value, LythonSourceSpan span)
        {
            EnsureWritable(span);
            _context.CheckExecution(span);
            if (_owner.IsText && value is PyString text)
            {
                if (_buffer.WrittenMemory.Length == 0 && _owner.EncodingName == "utf-8-sig") _buffer.Append("\uFEFF"u8);
                _buffer.Append(text); return new BigInteger(text.Length);
            }
            if (!_owner.IsText && value is PyBytes bytes) { _buffer.Append(bytes.Bytes); return new BigInteger(bytes.Length); }
            throw RuntimeErrors.Type($"Popen.stdin.write() expects {(_owner.IsText ? "str" : "bytes")} in this content mode.", span);
        }

        public ReadOnlyMemory<byte> SnapshotAndClose(LythonSourceSpan? span)
        {
            if (!IsClosed)
            {
                IsClosed = true;
            }

            return _buffer.WrittenMemory;
        }

        public void Close() => IsClosed = true;

        public void FlushValue(LythonSourceSpan span) => EnsureWritable(span);

        public object Enter()
        {
            EnsureWritable(null);
            return this;
        }

        public ValueTask<object> EnterAsync() => ValueTask.FromResult(Enter());

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            Close();
            return false;
        }

        public ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
            => ValueTask.FromResult(Exit(exceptionType, exceptionValue, traceback));

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<Popen stdin>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object WriteBound(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1) throw RuntimeErrors.Type("Popen.stdin.write() expects one value.", span);
            return Write(arguments[0], span);
        }

        private object WriteLines(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => WriteLinesCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private ValueTask<object> WriteLinesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => WriteLinesCoreAsync(arguments, span, context, true);

        private async ValueTask<object> WriteLinesCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 1) throw RuntimeErrors.Type("Popen.stdin.writelines() expects one iterable.", span);
            if (asynchronous)
                await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false)) _ = Write(item, span);
            else foreach (var item in ToSequence(arguments[0], span, context)) _ = Write(item, span);
            return PyNone.Instance;
        }

        private object Flush(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.stdin.flush()", span);
            EnsureWritable(span);
            return PyNone.Instance;
        }

        private object CloseBound(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.stdin.close()", span);
            Close();
            return PyNone.Instance;
        }

        private object StreamPredicate(object[] arguments, LythonSourceSpan span, bool writable)
        {
            RequirePopenNoArguments(arguments, "Popen stdin stream predicate", span);
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }

            return writable;
        }

        private void EnsureWritable(LythonSourceSpan? span)
        {
            if (IsClosed || _owner.IsCompleted)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }
        }
    }

    private sealed class PopenOutputStream : IPyDynamicAttributes, IPyAsyncContextManager, IPyAsyncIteratorValue, IPyRenderableValue
    {
        private readonly PyPopen _owner;
        private readonly bool _isStandardError;
        private readonly ExecutionContext _context;
        private int _cursorByte;
        private bool _claimedByPipeline;

        public PopenOutputStream(PyPopen owner, bool isStandardError, ExecutionContext context)
        {
            _owner = owner;
            _isStandardError = isStandardError;
            _context = context;
        }

        public bool IsClosed { get; private set; }

        public void AttachAsPipelineInput(ExecutionContext context, LythonSourceSpan span)
        {
            if (!ReferenceEquals(context, _context))
            {
                throw new LythonRuntimeException("ValueError", "Popen pipeline endpoints must belong to the same execution.", span);
            }

            if (_isStandardError)
            {
                throw new LythonRuntimeException("ValueError", "Popen(..., stdin=...) accepts a prior stdout pipe, not stderr.", span);
            }

            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "Cannot use a closed Popen stdout pipe as stdin.", span);
            }

            if (_claimedByPipeline)
            {
                throw new LythonRuntimeException("ValueError", "Popen stdout pipe is already connected to another process.", span);
            }

            if (_cursorByte != 0)
            {
                throw new LythonRuntimeException("ValueError", "Cannot connect a Popen stdout pipe after reading from it.", span);
            }

            _claimedByPipeline = true;
        }

        public ReadOnlyMemory<byte> TakePipelinePayload(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = _owner.CompleteForPipeline(timeoutRequest, span, completionPath);
                MarkPipelineConsumed(payload.Length);
                return payload;
            }
            catch
            {
                IsClosed = true;
                throw;
            }
        }

        public async ValueTask<ReadOnlyMemory<byte>> TakePipelinePayloadAsync(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = await _owner
                    .CompleteForPipelineAsync(timeoutRequest, span, completionPath)
                    .ConfigureAwait(false);
                MarkPipelineConsumed(payload.Length);
                return payload;
            }
            catch
            {
                IsClosed = true;
                throw;
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "encoding" when _owner.IsText => PyString.FromString(_owner.EncodingName),
                "errors" when _owner.IsText => PyString.FromString(_owner.ErrorsName),
                "read" => BoundCallable.Create(
                    (arguments, span, _) => Read(arguments, span),
                    async (arguments, span, _) => await ReadAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.read",
                    ["size"],
                    0),
                "readline" => BoundCallable.Create(
                    (arguments, span, _) => ReadLine(arguments, span),
                    async (arguments, span, _) => await ReadLineAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readline",
                    ["size"],
                    0),
                "readlines" => BoundCallable.Create(
                    (arguments, span, _) => ReadLines(arguments, span),
                    async (arguments, span, _) => await ReadLinesAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readlines",
                    ["hint"],
                    0),
                "close" => BoundCallable.Create((arguments, span, _) => CloseBound(arguments, span), "Popen pipe.close", []),
                "readable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, readable: true), "Popen pipe.readable", []),
                "writable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.writable", []),
                "seekable" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.seekable", []),
                "isatty" => BoundCallable.Create((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            EnsureOpen(PopenSyntheticSpan);
            var line = ReadLineCore(_owner.GetOutput(_isStandardError, PopenSyntheticSpan), -1, PopenSyntheticSpan);
            if (ContentBytes(line).Length == 0)
            {
                value = PyNone.Instance;
                return false;
            }

            value = line;
            return true;
        }

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            EnsureOpen(PopenSyntheticSpan);
            var content = await _owner.GetOutputAsync(_isStandardError, PopenSyntheticSpan).ConfigureAwait(false);
            var line = ReadLineCore(content, -1, PopenSyntheticSpan);
            return ContentBytes(line).Length == 0 ? PyIterationResult.End : PyIterationResult.Yield(line);
        }

        public object Enter()
        {
            EnsureOpen(null);
            return this;
        }

        public ValueTask<object> EnterAsync() => ValueTask.FromResult(Enter());

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            Close();
            return false;
        }

        public ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
            => ValueTask.FromResult(Exit(exceptionType, exceptionValue, traceback));

        public void Close() => IsClosed = true;

        public object ReadRemainingAfterCompletion(LythonSourceSpan span)
        {
            EnsureOpen(span);
            return ReadCore(_owner.GetOutput(_isStandardError, span), -1, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(_isStandardError ? "<Popen stderr>" : "<Popen stdout>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Read(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.read", span);
            EnsureOpen(span);
            return ReadCore(_owner.GetOutput(_isStandardError, span), size, span);
        }

        private async ValueTask<object> ReadAsync(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.read", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadCore(content, size, span);
        }

        private object ReadLine(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.readline", span);
            EnsureOpen(span);
            return ReadLineCore(_owner.GetOutput(_isStandardError, span), size, span);
        }

        private async ValueTask<object> ReadLineAsync(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.readline", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadLineCore(content, size, span);
        }

        private object ReadLines(object[] arguments, LythonSourceSpan span)
        {
            var hint = ReadSize(arguments, "Popen pipe.readlines", span);
            EnsureOpen(span);
            return ReadLinesCore(_owner.GetOutput(_isStandardError, span), hint, span);
        }

        private async ValueTask<object> ReadLinesAsync(object[] arguments, LythonSourceSpan span)
        {
            var hint = ReadSize(arguments, "Popen pipe.readlines", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadLinesCore(content, hint, span);
        }

        private static ReadOnlyMemory<byte> ContentBytes(object content)
            => content is PyString text ? text.Utf8Bytes : ((PyBytes)content).Memory;

        private int EndAfter(object content, int size)
            => size < 0 ? ContentBytes(content).Length : content is PyString text
                ? text.GetByteIndexAfterRunes(_cursorByte, size)
                : _cursorByte + Math.Min(size, ContentBytes(content).Length - _cursorByte);

        private object Slice(object content, int end, LythonSourceSpan span)
        {
            _context.CheckExecution(span);
            var value = content is PyString text ? (object)text.SliceByByteRange(_cursorByte, end)
                : CopySubprocessBytes(ContentBytes(content).Slice(_cursorByte, end - _cursorByte), _context, span);
            _cursorByte = end;
            return value;
        }

        private object ReadCore(object content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            return Slice(content, EndAfter(content, size), span);
        }

        private object ReadLineCore(object content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            var source = ContentBytes(content).Span;
            var end = EndAfter(content, size);
            for (var offset = _cursorByte; offset < end;)
            {
                _context.CheckExecution(span);
                var count = Math.Min(4096, end - offset);
                var newline = source.Slice(offset, count).IndexOf((byte)'\n');
                if (newline >= 0) { end = offset + newline + 1; break; }
                offset += count;
            }
            return Slice(content, end, span);
        }

        private PyList ReadLinesCore(object content, int hint, LythonSourceSpan span)
        {
            var lines = new PyList(0, _context.MemoryGovernor, span);
            long byteCount = 0;
            while (true)
            {
                var line = ReadLineCore(content, -1, span);
                if (ContentBytes(line).Length == 0) break;
                lines.Add(line);
                byteCount += ContentBytes(line).Length;
                if (hint > 0 && byteCount > hint) break;
            }
            return lines;
        }

        private object CloseBound(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen pipe.close()", span);
            Close();
            return PyNone.Instance;
        }

        private object StreamPredicate(object[] arguments, LythonSourceSpan span, bool readable)
        {
            RequirePopenNoArguments(arguments, "Popen pipe stream predicate", span);
            EnsureOpen(span);
            return readable;
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }

            if (_claimedByPipeline)
            {
                throw new LythonRuntimeException("ValueError", "Popen stdout pipe is connected to another process.", span);
            }
        }

        private void MarkPipelineConsumed(int byteLength)
        {
            _cursorByte = byteLength;
            IsClosed = true;
        }

        private static int ReadSize(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                return -1;
            }

            if (!PyNumberOps.TryAsInteger(arguments[0], out var value) || value < int.MinValue || value > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(size) expects an integer or None.", span);
            }

            return (int)value;
        }

    }


    private static void RequirePopenNoArguments(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects no arguments.", span);
        }
    }
}
