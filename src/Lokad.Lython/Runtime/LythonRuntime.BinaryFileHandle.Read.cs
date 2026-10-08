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
                _context.CheckExecution(span);
                if (_window.Length == 0)
                {
                    _context.MemoryGovernor.Reserve(_windowBytes, span);
                    try { _window = new byte[_windowBytes]; }
                    catch { _context.MemoryGovernor.ReleaseReserved(_windowBytes); throw; }
                    _context.MemoryGovernor.Commit(_windowBytes);
                    ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
                }
                using var acquisition = _context.MemoryGovernor.ReserveTemporary(32L + _windowBytes, span);
                _context.RegisterHostCall(span);
                var payload = asynchronous
                    ? await _context.ReadHostBytesRangeAsync(Path, _nextOffset, _windowBytes, span).ConfigureAwait(false)
                    : _context.ReadHostBytesRange(Path, _nextOffset, _windowBytes, span);
                if (payload.Length > _windowBytes)
                    throw RuntimeErrors.Runtime("host binary range exceeded requested byte count", span);
                var acquired = _acquiredBytes + payload.Length;
                if (_context.Limits.MaxHostReadBytes is { } maximum && acquired > maximum)
                    throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maximum})", span);
                if (_nextOffset > long.MaxValue - payload.Length)
                    throw new LythonRuntimeException("OverflowError", "binary file offset is too large", span);
                payload.Span.CopyTo(_window);
                _windowLength = payload.Length;
                _windowConsumed = 0;
                _nextOffset += payload.Length;
                _acquiredBytes = acquired;
                _eof = payload.Length == 0;
            }

            private async ValueTask<PyBytes> ReadAsync(long size, bool line, LythonSourceSpan? span, bool asynchronous)
            {
                EnsureReadable(span);
                if (!line && size < -1)
                    throw new LythonRuntimeException("ValueError", "read length must be non-negative or -1", span);
                var result = new GovernedByteBuilder(_context.MemoryGovernor, span);
                try
                {
                    while (size != 0)
                    {
                        await RefillAsync(span, asynchronous).ConfigureAwait(false);
                        if (_eof) break;
                        _context.CheckExecution(span);
                        var count = _windowLength - _windowConsumed;
                        if (size > 0) count = (int)Math.Min(count, size);
                        var ended = false;
                        if (line)
                        {
                            for (var index = 0; index < count; index++)
                            {
                                if ((index & 1023) == 0) _context.CheckExecution(span);
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
                    // Builder and independent returned bytes coexist until the
                    // fresh value is registered. Denial never strands scratch.
                    _context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(result.Length), span);
                    var bytes = result.WrittenSpan.ToArray();
                    return CreateBytes(bytes, _context, span);
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
