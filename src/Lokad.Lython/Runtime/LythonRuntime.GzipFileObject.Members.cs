using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class GzipFileObject
    {
        private bool _busy;
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => _closed,
                "name" => _name,
                "mode" => PyString.FromString(_reading ? "rb" : "wb"),
                "mtime" when _reading => _mtime is { } time ? new BigInteger(time) : PyNone.Instance,
                "fileobj" => _stream,
                _ => MissingMemberValue.Instance,
            };
            if (!ReferenceEquals(value, MissingMemberValue.Instance)) return true;
            var parameters = name switch
            {
                "read" or "readline" => new[] { "size" },
                "readlines" => new[] { "hint" },
                "write" => new[] { "data" },
                "writelines" => new[] { "lines" },
                "flush" => new[] { "zlib_mode" },
                "__exit__" => new[] { "type", "value", "traceback" },
                "seek" => new[] { "offset", "whence" },
                "close" or "tell" or "readable" or "writable" or "seekable" or "fileno" or "rewind" or
                    "__iter__" or "__next__" or "__enter__" => [],
                _ => null,
            };
            if (parameters is null) return false;
            var required = name is "write" or "writelines" or "seek" ? 1 : name == "__exit__" ? 3 : 0;
            var signature = LythonCallableSignature.Create("GzipFile." + name, parameters, required);
            value = BoundCallable.Create(
                (args, span, context) => InvokeMemberAsync(name, args, span, false).GetAwaiter().GetResult(), signature,
                (args, span, context) => InvokeMemberAsync(name, args, span, true));
            return true;
        }

        private void BeginOperation(LythonSourceSpan span)
        {
            if (_busy) throw new LythonRuntimeException("RuntimeError", "reentrant call inside GzipFile", span);
            _busy = true;
        }
        private async ValueTask<object> InvokeMemberAsync(string name, object[] args, LythonSourceSpan span, bool asynchronous)
        {
            _context.CheckExecution(span);
            if (name == "readable") return _reading;
            if (name == "writable") return !_reading;
            if (name is "seek" or "seekable" or "fileno" or "rewind")
                throw new LythonRuntimeException("NotImplementedError", "GzipFile random access and file descriptors are unsupported", span);
            BeginOperation(span);
            try
            {
                switch (name)
                {
                    case "close": case "__exit__":
                        await CloseAsync(span, asynchronous).ConfigureAwait(false);
                        return name == "close" ? PyNone.Instance : false;
                    case "read": case "readline":
                    {
                        EnsureReadable(span, bufferedBase: name == "readline");
                        var size = args.Length == 0 || args[0] is PyNone ? -1
                            : await CoerceIoIntegerAsync(args[0], span, _context, asynchronous).ConfigureAwait(false);
                        return await ReadAsync(size, name == "readline", span, asynchronous).ConfigureAwait(false);
                    }
                    case "readlines":
                    {
                        EnsureReadable(span, bufferedBase: true);
                        var hint = args.Length == 0 || args[0] is PyNone ? -1
                            : await CoerceIoIntegerAsync(args[0], span, _context, asynchronous).ConfigureAwait(false);
                        var result = new PyList([], _context.MemoryGovernor, span);
                        _context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                        long consumed = 0;
                        while (true)
                        {
                            var line = await ReadAsync(-1, true, span, asynchronous).ConfigureAwait(false);
                            if (line.Length == 0) break;
                            result.Add(line); _context.ObserveCollectionCount(result.Count, span);
                            consumed += line.Length;
                            if (hint > 0 && consumed > hint) break;
                        }
                        return result;
                    }
                    case "write": return await WriteAsync(args[0], span, asynchronous).ConfigureAwait(false);
                    case "writelines":
                        EnsureWritable(span);
                        if (asynchronous)
                            await foreach (var item in PyIteration.ToSequenceAsync(args[0], span, _context).ConfigureAwait(false))
                                await WriteAsync(item, span, true).ConfigureAwait(false);
                        else
                            foreach (var item in PyIteration.ToSequence(args[0], span, _context))
                                await WriteAsync(item, span, false).ConfigureAwait(false);
                        return PyNone.Instance;
                    case "flush":
                        EnsureOpen(span);
                        if (!_reading && args.Length != 0)
                        {
                            var mode = await CoerceIoIntegerAsync(args[0], span, _context, asynchronous).ConfigureAwait(false);
                            if (mode != 2) throw new LythonRuntimeException("NotImplementedError", "GzipFile.flush supports Z_SYNC_FLUSH (2) only", span);
                        }
                        await FlushAsync(span, asynchronous).ConfigureAwait(false); return PyNone.Instance;
                    case "tell":
                        EnsureOpen(span);
                        if (!_reading) await DrainPendingAsync(span, asynchronous).ConfigureAwait(false);
                        return _position;
                    case "__iter__": case "__enter__": EnsureOpen(span); return this;
                    case "__next__":
                    {
                        var line = await ReadAsync(-1, true, span, asynchronous).ConfigureAwait(false);
                        if (line.Length == 0) throw new LythonRuntimeException("StopIteration", "", span);
                        return line;
                    }
                    default: throw new InvalidOperationException("Unknown GzipFile member");
                }
            }
            finally { _busy = false; }
        }

        public object Enter() { EnsureOpen(_creationSpan); return this; }
        public ValueTask<object> EnterAsync() => ValueTask.FromResult(Enter());
        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        { CloseAsync(_creationSpan, false).GetAwaiter().GetResult(); return false; }
        public async ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
        { await CloseAsync(_creationSpan, true).ConfigureAwait(false); return false; }
        object IExecutionFileWriter.Exit() { CloseAsync(_creationSpan, false).GetAwaiter().GetResult(); return false; }
        async ValueTask<object> IExecutionFileWriter.ExitAsync()
        { await CloseAsync(_creationSpan, true).ConfigureAwait(false); return false; }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            var result = MoveNextAsync(false).GetAwaiter().GetResult();
            value = result.HasValue ? result.Value : PyNone.Instance;
            return result.HasValue;
        }
        public ValueTask<PyIterationResult> TryMoveNextAsync() => MoveNextAsync(true);
        private async ValueTask<PyIterationResult> MoveNextAsync(bool asynchronous)
        {
            BeginOperation(_creationSpan);
            try
            {
                var line = await ReadAsync(-1, true, _creationSpan, asynchronous).ConfigureAwait(false);
                return line.Length == 0 ? PyIterationResult.End : PyIterationResult.Yield(line);
            }
            finally { _busy = false; }
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<gzip.GzipFile>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
