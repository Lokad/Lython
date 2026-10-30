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
            private int _windowBytes;
            private ILythonByteFile? _file;
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
            }

            public string Path { get; }
            public string Mode => TextOpenModeName(_operation) + "b";
            public bool IsClosed { get; private set; }
            internal long OwnedBytes => ShellBytes + _window.Length + _writeBuffer.CommittedCapacity;

            internal static async ValueTask<BinaryFileHandle> OpenAsync(string path, TextFileOperation operation,
                ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            {
                context.CheckExecution(span);
                // Shell and registry capacity precede host create/truncate effects.
                var handle = new BinaryFileHandle(path, operation, context, span);
                try
                {
                    context.State.TrackOpenFileWriter(handle);
                    var options = operation == TextFileOperation.Read
                        ? new LythonFileOpenOptions(LythonFileAccess.Read)
                        : new LythonFileOpenOptions(LythonFileAccess.Write, create: true,
                            truncate: operation == TextFileOperation.Write,
                            append: operation == TextFileOperation.Append);
                    handle._file = await context.Services.OpenFileAsync(path, options, span, asynchronous).ConfigureAwait(false);
                    context.CheckExecution(span);
                    var length = HostOperation.Invoke(() => handle._file.LengthHint, "file_metadata", span, context.MemoryGovernor);
                    handle._windowBytes = length is >= 0 and < DefaultWindowBytes
                        ? Math.Max(256, (int)length.Value) : DefaultWindowBytes;
                    handle._position = HostOperation.Invoke(() => handle._file.Position, "file_metadata", span, context.MemoryGovernor);
                    if (handle._position < 0) throw RuntimeErrors.Runtime("host file position cannot be negative", span);
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
                _services.CheckExecution(span);
                var offset = 0;
                try
                {
                    // The builder is stable until the actual borrow ends. No copy
                    // or second payload reservation is needed for host publication.
                    while (offset < _writeBuffer.Length)
                    {
                        _services.CheckExecution(span);
                        var payload = _writeBuffer.WrittenMemory.Slice(offset);
                        LythonFileWriteResult result;
                        try
                        {
                            result = asynchronous
                                ? await HostOperation.AwaitAsync(() => _file!.WriteAsync(payload, _services.Limits.CancellationToken),
                                    "write_file", span, _services.MemoryGovernor).ConfigureAwait(false)
                                : HostOperation.Await(_file!, () => _file!.WriteAsync(payload, _services.Limits.CancellationToken),
                                    "write_file", span, _services.MemoryGovernor);
                        }
                        catch
                        {
                            // A thrown operation gives no accepted-prefix guarantee.
                            await RetireAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                            throw;
                        }
                        var count = result.AcceptedBytes;
                        if (count is < 0 || count > payload.Length ||
                            (result.Failure is null && count is null or 0))
                        {
                            await RetireAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                            HostOperation.Invoke<object>(() => throw new LythonHostException(LythonHostFailureKind.Transport,
                                "host file write reported an invalid accepted prefix"), "write_file", span, _services.MemoryGovernor);
                        }
                        if (result.Failure is { } failure)
                        {
                            try { HostOperation.Invoke<object>(() => throw failure, "write_file", span, _services.MemoryGovernor); }
                            catch
                            {
                                if (count != 0 || offset != 0 || failure.Kind == LythonHostFailureKind.MemoryAdmission)
                                    await RetireAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                                throw;
                            }
                        }
                        offset += count!.Value;
                    }
                    // A later flush failure cannot cause these accepted bytes to replay.
                    _writeBuffer.Release();
                    _services.CheckExecution(span);
                    if (offset != 0)
                        _position = HostOperation.Invoke(() => _file!.Position, "file_metadata", span, _services.MemoryGovernor);
                    if (asynchronous)
                        await HostOperation.AwaitAsync(() => _file!.FlushAsync(_services.Limits.CancellationToken),
                            "flush_file", span, _services.MemoryGovernor).ConfigureAwait(false);
                    else HostOperation.Await(_file!, () => _file!.FlushAsync(_services.Limits.CancellationToken),
                        "flush_file", span, _services.MemoryGovernor);
                    _services.CheckExecution(span);
                }
                catch (SynchronousHostContractException)
                {
                    await RetireAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                    throw;
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
                if (IsClosed) return;
                try
                {
                    if (_services.MemoryGovernor.AdmissionFailure is null && !_services.State.CancellationRequested)
                        await FlushAsync(span, asynchronous).ConfigureAwait(false);
                }
                finally { await RetireAsync(span, asynchronous).ConfigureAwait(false); }
            }

            private async ValueTask RetireAsync(LythonSourceSpan? span, bool asynchronous)
            {
                if (IsClosed) return;
                IsClosed = true;
                _services.State.UntrackOpenFileWriter(this);
                var file = _file; _file = null;
                try
                {
                    if (file is not null)
                    {
                        if (asynchronous) await HostOperation.AwaitAsync(file.DisposeAsync, "close_file", span, _services.MemoryGovernor).ConfigureAwait(false);
                        else HostOperation.AwaitAsync(file.DisposeAsync, "close_file", span, _services.MemoryGovernor)
                            .AsTask().GetAwaiter().GetResult();
                    }
                }
                finally { ReleaseBuffers(); }
            }

            private async ValueTask RetireAfterFailureAsync(LythonSourceSpan? span, bool asynchronous)
            {
                try { await RetireAsync(span, asynchronous).ConfigureAwait(false); }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                { /* Keep the operation's first failure after actual cleanup. */ }
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
