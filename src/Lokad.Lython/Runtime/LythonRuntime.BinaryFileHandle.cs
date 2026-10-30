using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed partial class BinaryFileHandle : IPyAsyncContextManager, IPyIteratorValue,
            IPyAsyncIteratorValue, IPyDynamicAttributes, IExecutionFile
        {
            // Byte backing, offsets and run capabilities contain no guest values.
            // Covers the handle, its builder and retained array headers beside
            // payload capacities, which have separate governed charges.
            internal const long ShellBytes = 256;
            internal const int DefaultWindowBytes = 16 * 1024;
            // A file can escape its creating function, or stay registered for
            // final publication. Keep authority/limits without pinning that frame.
            private readonly ExecutionServices _services;
            private readonly TextFileOperation _operation;
            private readonly GovernedByteBuilder _writeBuffer;
            private int _windowBytes;
            private readonly OpenedByteFile _file;
            private bool _closed;
            private byte[] _window = [];
            private int _windowLength;
            private int _windowConsumed;
            private BigInteger _position;
            private bool _eof;

            private BinaryFileHandle(string path, TextFileOperation operation, ExecutionContext context,
                LythonSourceSpan span)
            {
                Path = path;
                _operation = operation;
                _services = context.Services;
                context.MemoryGovernor.Reserve(ShellBytes, span);
                context.MemoryGovernor.Commit(ShellBytes);
                _writeBuffer = new GovernedByteBuilder(context.MemoryGovernor, span);
                _file = new OpenedByteFile(context.Services);
            }

            public string Path { get; }
            public string Mode => TextOpenModeName(_operation) + "b";
            public bool IsClosed => _closed || _file.IsClosed;
            internal long OwnedBytes => ShellBytes + _window.Length + _writeBuffer.CommittedCapacity;

            internal static async ValueTask<BinaryFileHandle> OpenAsync(string path, TextFileOperation operation,
                ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            {
                context.CheckExecution(span);
                // Shell and registry capacity precede host create/truncate effects.
                var handle = new BinaryFileHandle(path, operation, context, span);
                try
                {
                    context.State.TrackOpenFile(handle);
                    var options = operation == TextFileOperation.Read
                        ? new LythonFileOpenOptions(LythonFileAccess.Read)
                        : new LythonFileOpenOptions(LythonFileAccess.Write, create: true,
                            truncate: operation == TextFileOperation.Write,
                            append: operation == TextFileOperation.Append);
                    await handle._file.OpenAsync(path, options, span, asynchronous).ConfigureAwait(false);
                    context.CheckExecution(span);
                    var length = handle._file.LengthHint(span);
                    handle._windowBytes = length is >= 0 and < DefaultWindowBytes
                        ? Math.Max(256, (int)length.Value) : DefaultWindowBytes;
                    handle._position = handle._file.Position(span);
                    return handle;
                }
                catch
                {
                    try { await handle.RetireAsync(span, asynchronous).ConfigureAwait(false); }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    { /* Preserve acquisition failure after awaited cleanup. */ }
                    context.MemoryGovernor.Release(ShellBytes);
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

            internal BigInteger Write(object value, LythonSourceSpan span)
            {
                EnsureWritable(span);
                if (value is not PyBytes bytes)
                    throw new LythonRuntimeException("TypeError", "a bytes-like object is required", span);
                for (var offset = 0; offset < bytes.Length;)
                {
                    _services.CheckExecution(span);
                    var count = Math.Min(4096, bytes.Length - offset);
                    _writeBuffer.Append(bytes.Bytes.Slice(offset, count));
                    offset += count;
                    _position += count;
                }
                return new BigInteger(bytes.Length);
            }

            private async ValueTask FlushAsync(LythonSourceSpan? span, bool asynchronous)
            {
                EnsureOpen(span);
                if (_operation == TextFileOperation.Read) return;
                try
                {
                    if (_writeBuffer.Length != 0)
                    {
                        await _file.WriteAsync(_writeBuffer.WrittenMemory, span, asynchronous).ConfigureAwait(false);
                        // Accepted data cannot replay after metadata/flush failure.
                        _writeBuffer.Release();
                        _services.CheckExecution(span);
                        _position = _file.Position(span);
                    }
                    await _file.FlushAsync(span, asynchronous).ConfigureAwait(false);
                }
                finally
                {
                    if (_file.IsClosed) await RetireAsync(span, asynchronous).ConfigureAwait(false);
                }
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
                if (_closed) return;
                try
                {
                    if (!_file.IsClosed && _services.MemoryGovernor.AdmissionFailure is null && !_services.State.CancellationRequested)
                        await FlushAsync(span, asynchronous).ConfigureAwait(false);
                }
                finally { await RetireAsync(span, asynchronous).ConfigureAwait(false); }
            }

            private async ValueTask RetireAsync(LythonSourceSpan? span, bool asynchronous)
            {
                if (_closed) return;
                _closed = true;
                _services.State.UntrackOpenFile(this);
                try { await _file.CloseAsync(span, asynchronous).ConfigureAwait(false); }
                finally { ReleaseBuffers(); }
            }

            private void ReleaseBuffers()
            {
                _services.MemoryGovernor.Release(_window.Length);
                _window = [];
                _windowLength = _windowConsumed = 0;
                _writeBuffer.Release();
            }

            object IExecutionFile.Exit(LythonRuntime.ExecutionContext context) => Exit();
            ValueTask<object> IExecutionFile.ExitAsync(LythonRuntime.ExecutionContext context) => ExitAsync();

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
