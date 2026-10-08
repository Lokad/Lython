using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed partial class BinaryFileHandle : IPyAsyncContextManager, IPyIteratorValue,
            IPyAsyncIteratorValue, IPyDynamicAttributes, IPyOwnershipSnapshot, IExecutionFileWriter
        {
            // Covers the handle, its builder and retained array headers beside
            // payload capacities, which have separate governed charges.
            internal const long ShellBytes = 256;
            internal const int DefaultWindowBytes = 16 * 1024;
            private readonly ExecutionContext _context;
            private readonly TextFileOperation _operation;
            private readonly GovernedByteBuilder _writeBuffer;
            private readonly int _windowBytes;
            private byte[] _window = [];
            private int _windowLength;
            private int _windowConsumed;
            private long _nextOffset;
            private BigInteger _acquiredBytes;
            private BigInteger _position;
            private bool _eof;
            private bool _published;

            private BinaryFileHandle(string path, TextFileOperation operation, ExecutionContext context,
                int windowBytes, BigInteger position, LythonSourceSpan span)
            {
                Path = path;
                _operation = operation;
                _context = context;
                _windowBytes = windowBytes;
                _position = position;
                context.MemoryGovernor.Reserve(ShellBytes, span);
                context.MemoryGovernor.Commit(ShellBytes);
                _writeBuffer = new GovernedByteBuilder(context.MemoryGovernor, span);
            }

            public string Path { get; }
            public string Mode => TextOpenModeName(_operation) + "b";
            public bool IsClosed { get; private set; }
            internal long OwnedBytes => ShellBytes + _window.Length + _writeBuffer.CommittedCapacity;
            public bool TrySnapshotOwnership(out long chargeBytes)
                => OwnershipSnapshot.Owned(_context.MemoryGovernor, OwnedBytes, out chargeBytes);

            internal static async ValueTask<BinaryFileHandle> OpenAsync(string path, TextFileOperation operation,
                ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            {
                context.RegisterHostCall(span);
                var stat = asynchronous
                    ? await context.HostStatAsync(path, span).ConfigureAwait(false)
                    : context.HostStat(path, span);
                if (stat.Exists && !stat.IsFile)
                    throw new LythonRuntimeException("IsADirectoryError", "Is a directory: " + path, span);
                if (operation == TextFileOperation.Read && !stat.Exists)
                    throw new LythonRuntimeException("FileNotFoundError", "No such file: " + path, span);
                if (operation == TextFileOperation.Read && context.Limits.MaxHostReadBytes is { } maximum && stat.Size > maximum)
                    throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maximum})", span);
                var window = stat.Size < DefaultWindowBytes
                    ? Math.Max(256, (int)stat.Size) : DefaultWindowBytes;
                var handle = new BinaryFileHandle(path, operation, context, window,
                    operation == TextFileOperation.Append && stat.Exists ? stat.Size : BigInteger.Zero, span);
                try
                {
                    context.State.CallTemporaries.TrackFreshMutable(handle, handle.OwnedBytes, span);
                    if (operation == TextFileOperation.Read)
                        await handle.RefillAsync(span, asynchronous).ConfigureAwait(false);
                    else
                        context.State.TrackOpenFileWriter(handle);
                    return handle;
                }
                catch
                {
                    handle.ReleaseBuffers();
                    // TrackFreshMutable refunds a denied fresh shell. Successful
                    // registration retains that shell until the abandoned owner is swept.
                    throw;
                }
            }

            internal void EnsureOpen(LythonSourceSpan? span)
            {
                if (IsClosed) throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }

            private void EnsureReadable(LythonSourceSpan? span)
            {
                EnsureOpen(span);
                if (_operation != TextFileOperation.Read)
                    throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "File not open for reading", span);
            }

            private void EnsureWritable(LythonSourceSpan? span)
            {
                EnsureOpen(span);
                if (_operation == TextFileOperation.Read)
                    throw new LythonRuntimeException(ModuleException("io", "UnsupportedOperation"), "File not open for writing", span);
            }

            private BigInteger Write(object value, LythonSourceSpan span)
            {
                EnsureWritable(span);
                if (value is not PyBytes bytes)
                    throw new LythonRuntimeException("TypeError", "a bytes-like object is required", span);
                for (var offset = 0; offset < bytes.Length;)
                {
                    _context.CheckExecution(span);
                    var count = Math.Min(4096, bytes.Length - offset);
                    _writeBuffer.Append(bytes.Bytes.Slice(offset, count));
                    offset += count;
                    _position += count;
                    ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
                }
                return new BigInteger(bytes.Length);
            }

            private async ValueTask FlushAsync(LythonSourceSpan? span, bool asynchronous)
            {
                EnsureOpen(span);
                if (_operation == TextFileOperation.Read || (_published && _writeBuffer.Length == 0)) return;
                // Empty append must create a missing file too. Host calls receive
                // an independent buffer held under reservation throughout suspension.
                var length = _writeBuffer.Length;
                using var staging = _context.MemoryGovernor.ReserveTemporary(32L + length, span);
                var payload = new byte[length];
                for (var offset = 0; offset < length;)
                {
                    _context.CheckExecution(span);
                    var count = Math.Min(4096, length - offset);
                    _writeBuffer.WrittenSpan.Slice(offset, count).CopyTo(payload.AsSpan(offset, count));
                    offset += count;
                }
                _context.RegisterHostCall(span);
                if (_operation == TextFileOperation.Append || _published)
                {
                    if (asynchronous) await _context.AppendHostBytesAsync(Path, payload, span).ConfigureAwait(false);
                    else _context.AppendHostBytes(Path, payload, span);
                }
                else
                {
                    if (asynchronous) await _context.WriteHostBytesAsync(Path, payload, span).ConfigureAwait(false);
                    else _context.WriteHostBytes(Path, payload, span);
                }
                _published = true;
                _writeBuffer.Release();
                ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
            }

            public object Exit()
            {
                CloseAsync(null, asynchronous: false).GetAwaiter().GetResult();
                return false;
            }

            public async ValueTask<object> ExitAsync()
            {
                await CloseAsync(null, asynchronous: true).ConfigureAwait(false);
                return false;
            }

            private async ValueTask CloseAsync(LythonSourceSpan? span, bool asynchronous)
            {
                if (IsClosed) return;
                await FlushAsync(span, asynchronous).ConfigureAwait(false);
                ReleaseBuffers();
                _context.State.UntrackOpenFileWriter(this);
                IsClosed = true;
            }

            private void ReleaseBuffers()
            {
                _context.MemoryGovernor.Release(_window.Length);
                _window = [];
                _windowLength = _windowConsumed = 0;
                _writeBuffer.Release();
                ChargeReclamationPool.NotifyStorageReplaced(this, OwnedBytes);
            }

            object IPyContextManager.Enter() { EnsureOpen(null); return this; }
            ValueTask<object> IPyAsyncContextManager.EnterAsync()
                => ValueTask.FromResult(((IPyContextManager)this).Enter());
            bool IPyContextManager.Exit(object exceptionType, object exceptionValue, object traceback)
            {
                Exit(); return false;
            }
            async ValueTask<bool> IPyAsyncContextManager.ExitAsync(object exceptionType, object exceptionValue, object traceback)
            {
                await ExitAsync().ConfigureAwait(false); return false;
            }
        }
    }
}
