using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        internal sealed partial class BinaryFileHandle : IPyAsyncContextManager, IPyIteratorValue,
            IPyAsyncIteratorValue, IPyDynamicAttributes, IExecutionFileWriter
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
            private readonly int _windowBytes;
            private byte[] _window = [];
            private int _windowLength;
            private int _windowConsumed;
            private long _nextOffset;
            private BigInteger _position;
            private bool _eof;

            private BinaryFileHandle(string path, TextFileOperation operation, ExecutionContext context,
                int windowBytes, BigInteger position, LythonSourceSpan span)
            {
                Path = path;
                _operation = operation;
                _services = context.Services;
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

            internal static async ValueTask<BinaryFileHandle> OpenAsync(string path, TextFileOperation operation,
                ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            {
                context.CheckExecution(span);
                var stat = asynchronous
                    ? await context.HostStatAsync(path, span).ConfigureAwait(false)
                    : context.HostStat(path, span);
                if (stat.Exists && !stat.IsFile)
                    throw new LythonRuntimeException("IsADirectoryError", "Is a directory: " + path, span);
                if (operation == TextFileOperation.Read && !stat.Exists)
                    throw new LythonRuntimeException("FileNotFoundError", "No such file: " + path, span);
                var window = stat.Size < DefaultWindowBytes
                    ? Math.Max(256, (int)stat.Size) : DefaultWindowBytes;
                var handle = new BinaryFileHandle(path, operation, context, window,
                    operation == TextFileOperation.Append && stat.Exists ? stat.Size : BigInteger.Zero, span);
                try
                {
                    if (operation != TextFileOperation.Read)
                    {
                        context.State.TrackOpenFileWriter(handle);
                        if (operation == TextFileOperation.Write)
                        {
                            if (asynchronous) await context.Services.WriteHostBytesAsync(path, ReadOnlyMemory<byte>.Empty, span).ConfigureAwait(false);
                            else context.Services.WriteHostBytes(path, ReadOnlyMemory<byte>.Empty, span);
                        }
                        else if (!stat.Exists)
                        {
                            if (asynchronous) await context.Services.AppendHostBytesAsync(path, ReadOnlyMemory<byte>.Empty, span).ConfigureAwait(false);
                            else context.Services.AppendHostBytes(path, ReadOnlyMemory<byte>.Empty, span);
                        }
                    }
                    return handle;
                }
                catch
                {
                    context.State.UntrackOpenFileWriter(handle);
                    handle.ReleaseBuffers();
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
                if (_operation == TextFileOperation.Read || _writeBuffer.Length == 0) return;
                // Opening established the file. Host calls receive
                // an independent buffer held under reservation throughout suspension.
                var length = _writeBuffer.Length;
                using var staging = _services.MemoryGovernor.ReserveTemporary(32L + length, span);
                var payload = new byte[length];
                for (var offset = 0; offset < length;)
                {
                    _services.CheckExecution(span);
                    var count = Math.Min(4096, length - offset);
                    _writeBuffer.WrittenSpan.Slice(offset, count).CopyTo(payload.AsSpan(offset, count));
                    offset += count;
                }
                _services.CheckExecution(span);
                if (asynchronous) await _services.AppendHostBytesAsync(Path,payload,span).ConfigureAwait(false);
                else _services.AppendHostBytes(Path,payload,span);
                _writeBuffer.Release();
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
                var completed = false;
                try
                {
                    if (_services.MemoryGovernor.AdmissionFailure is null)
                        await FlushAsync(span, asynchronous).ConfigureAwait(false);
                    completed = true;
                }
                finally
                {
                    if (completed || _services.MemoryGovernor.AdmissionFailure is not null)
                    {
                        ReleaseBuffers();
                        _services.State.UntrackOpenFileWriter(this);
                        IsClosed = true;
                    }
                }
            }

            private void ReleaseBuffers()
            {
                _services.MemoryGovernor.Release(_window.Length);
                _window = [];
                _windowLength = _windowConsumed = 0;
                _writeBuffer.Release();
            }

            object IExecutionFileWriter.Exit(LythonRuntime.ExecutionContext context) => Exit();
            ValueTask<object> IExecutionFileWriter.ExitAsync(LythonRuntime.ExecutionContext context) => ExitAsync();

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
