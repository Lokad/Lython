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
                capacity: 0,
                maxLengthBytes: context.Limits.MaxStringLength,
                maxLengthOwner: "Popen stdin");
        }

        public bool IsClosed { get; private set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "encoding" => PyString.FromString(_owner.EncodingName),
                "errors" => PyString.FromString(_owner.ErrorsName),
                "write" => new BoundCallable((arguments, span, _) => WriteBound(arguments, span), "Popen.stdin.write", ["s"]),
                "writelines" => new BoundCallable((arguments, span, _) => WriteLines(arguments, span), "Popen.stdin.writelines", ["lines"]),
                "flush" => new BoundCallable((arguments, span, _) => Flush(arguments, span), "Popen.stdin.flush", []),
                "close" => new BoundCallable((arguments, span, _) => CloseBound(arguments, span), "Popen.stdin.close", []),
                "writable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: true), "Popen.stdin.writable", []),
                "readable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.readable", []),
                "seekable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.seekable", []),
                "isatty" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public BigInteger Write(PyString text, LythonSourceSpan span)
        {
            EnsureWritable(span);
            _buffer.Append(text);
            return new BigInteger(text.Length);
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
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Popen.stdin.write(s) expects one string.", span);
            }

            return Write(text, span);
        }

        private object WriteLines(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Popen.stdin.writelines(lines) expects one iterable.", span);
            }

            foreach (var item in ToSequence(arguments[0], span))
            {
                if (!PyStringOps.TryAsString(item, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "Popen.stdin.writelines(lines) expects strings.", span);
                }

                _ = Write(text, span);
            }

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

        public PipelinePayload TakePipelinePayload(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = _owner.CompleteForPipeline(timeoutMilliseconds, span, completionPath);
                MarkPipelineConsumed(payload.Input.Length);
                return payload;
            }
            catch
            {
                IsClosed = true;
                throw;
            }
        }

        public async ValueTask<PipelinePayload> TakePipelinePayloadAsync(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = await _owner
                    .CompleteForPipelineAsync(timeoutMilliseconds, span, completionPath)
                    .ConfigureAwait(false);
                MarkPipelineConsumed(payload.Input.Length);
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
                "encoding" => PyString.FromString(_owner.EncodingName),
                "errors" => PyString.FromString(_owner.ErrorsName),
                "read" => new BoundCallable(
                    (arguments, span, _) => Read(arguments, span),
                    async (arguments, span, _) => await ReadAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.read",
                    ["size"],
                    0),
                "readline" => new BoundCallable(
                    (arguments, span, _) => ReadLine(arguments, span),
                    async (arguments, span, _) => await ReadLineAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readline",
                    ["size"],
                    0),
                "readlines" => new BoundCallable(
                    (arguments, span, _) => ReadLines(arguments, span),
                    async (arguments, span, _) => await ReadLinesAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readlines",
                    ["hint"],
                    0),
                "close" => new BoundCallable((arguments, span, _) => CloseBound(arguments, span), "Popen pipe.close", []),
                "readable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: true), "Popen pipe.readable", []),
                "writable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.writable", []),
                "seekable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.seekable", []),
                "isatty" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value))
            {
                yield return value;
            }
        }

        public async IAsyncEnumerable<object> IterateAsync()
        {
            while (true)
            {
                var (hasValue, value) = await TryMoveNextAsync().ConfigureAwait(false);
                if (!hasValue)
                {
                    yield break;
                }

                yield return value;
            }
        }

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            EnsureOpen(PopenSyntheticSpan);
            var line = ReadLineCore(_owner.GetOutput(_isStandardError, PopenSyntheticSpan), -1, PopenSyntheticSpan);
            if (line.Length == 0)
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
            return line.Length == 0 ? (false, PyNone.Instance) : (true, line);
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

        public PyString ReadRemainingAfterCompletion(LythonSourceSpan span)
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

        private PyString ReadCore(PyString content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_cursorByte >= content.Utf8Bytes.Length)
            {
                return PyString.Empty;
            }

            var end = size < 0
                ? content.Utf8Bytes.Length
                : ByteOffsetAfterRunes(content, _cursorByte, size);
            var value = content.SliceByByteRange(_cursorByte, end);
            _cursorByte = end;
            return value;
        }

        private PyString ReadLineCore(PyString content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_cursorByte >= content.Utf8Bytes.Length)
            {
                return PyString.Empty;
            }

            var source = content.Utf8Bytes.Span;
            var newline = source[_cursorByte..].IndexOf((byte)'\n');
            var end = newline < 0 ? source.Length : _cursorByte + newline + 1;
            if (size >= 0)
            {
                end = Math.Min(end, ByteOffsetAfterRunes(content, _cursorByte, size));
            }

            var value = content.SliceByByteRange(_cursorByte, end);
            _cursorByte = end;
            return value;
        }

        private PyList ReadLinesCore(PyString content, int hint, LythonSourceSpan span)
        {
            var lines = new List<object>();
            var byteCount = 0;
            while (true)
            {
                var line = ReadLineCore(content, -1, span);
                if (line.Length == 0)
                {
                    break;
                }

                lines.Add(line);
                byteCount += line.Utf8Bytes.Length;
                if (hint > 0 && byteCount > hint)
                {
                    break;
                }
            }

            return new PyList(lines, _context.MemoryGovernor, span);
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

        private static int ByteOffsetAfterRunes(PyString content, int startByte, int runeCount)
            => content.GetByteIndexAfterRunes(startByte, runeCount);
    }

    private readonly record struct PipelinePayload(ReadOnlyMemory<byte> Input, long CumulativeOutputBytes);

    private static void RequirePopenNoArguments(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects no arguments.", span);
        }
    }
}
