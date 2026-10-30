using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed partial class BinaryFileHandle
        {
            private async ValueTask RefillAsync(LythonSourceSpan? span, bool asynchronous)
            {
                if (_windowConsumed < _windowLength || _eof) return;
                _services.CheckExecution(span);
                if (_window.Length == 0)
                {
                    _services.MemoryGovernor.Reserve(_windowBytes, span);
                    try { _window = new byte[_windowBytes]; }
                    catch { _services.MemoryGovernor.ReleaseReserved(_windowBytes); throw; }
                    _services.MemoryGovernor.Commit(_windowBytes);
                }
                var count = asynchronous
                    ? await HostOperation.AwaitAsync(() => _file!.ReadAsync(_window, _services.Limits.CancellationToken),
                        "read_file", span, _services.MemoryGovernor).ConfigureAwait(false)
                    : HostOperation.Await(_file!, () => _file!.ReadAsync(_window, _services.Limits.CancellationToken),
                        "read_file", span, _services.MemoryGovernor);
                _services.CheckExecution(span);
                if (count < 0 || count > _window.Length)
                    throw RuntimeErrors.Runtime("host file read exceeded its buffer", span);
                _windowLength = count;
                _windowConsumed = 0;
                _eof = count == 0;
            }

            private async ValueTask<PyBytes> ReadAsync(long size, bool line, LythonSourceSpan? span, bool asynchronous)
            {
                EnsureReadable(span);
                if (!line && size < -1)
                    throw new LythonRuntimeException("ValueError", "read length must be non-negative or -1", span);
                var result = new GovernedByteBuilder(_services.MemoryGovernor, span);
                try
                {
                    while (size != 0)
                    {
                        await RefillAsync(span, asynchronous).ConfigureAwait(false);
                        if (_eof) break;
                        _services.CheckExecution(span);
                        var count = _windowLength - _windowConsumed;
                        if (size > 0) count = (int)Math.Min(count, size);
                        var ended = false;
                        if (line)
                        {
                            for (var index = 0; index < count; index++)
                            {
                                if ((index & 1023) == 0) _services.CheckExecution(span);
                                if (_window[_windowConsumed + index] == 10)
                                {
                                    count = index + 1;
                                    ended = true;
                                    break;
                                }
                            }
                        }
                        result.Append(_window.AsSpan(_windowConsumed, count));
                        _windowConsumed += count;
                        _position += count;
                        if (size > 0) size -= count;
                        if (ended) break;
                    }
                    return result.ToPyBytesAndRelease();
                }
                finally { result.Release(); }
            }

            public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
            public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

            public bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var line = ReadAsync(-1, line: true, null, asynchronous: false).GetAwaiter().GetResult();
                value = line.Length == 0 ? PyNone.Instance : line;
                return line.Length != 0;
            }

            public async ValueTask<PyIterationResult> TryMoveNextAsync()
            {
                var line = await ReadAsync(-1, line: true, null, asynchronous: true).ConfigureAwait(false);
                return line.Length == 0 ? PyIterationResult.End : PyIterationResult.Yield(line);
            }
        }
    }
}
