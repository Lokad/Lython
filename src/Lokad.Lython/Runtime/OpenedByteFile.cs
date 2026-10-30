namespace Lokad.Lython.Runtime;

// The text/binary handle's admitted shell covers this transport. One place owns
// host borrows, accepted-prefix validation and uncancelled native retirement.
// Guest buffers/codecs and execution registration remain with their file handle.
internal sealed class OpenedByteFile(ExecutionServices services)
{
    private ILythonByteFile? _file;
    internal bool IsClosed { get; private set; }

    internal async ValueTask OpenAsync(string path, LythonFileOpenOptions options, LythonSourceSpan? span, bool asynchronous)
    {
        _file = await services.OpenFileAsync(path, options, span, asynchronous).ConfigureAwait(false);
        services.CheckExecution(span);
    }

    internal long Position(LythonSourceSpan? span)
        => HostOperation.Invoke(() =>
        {
            var position = _file!.Position;
            return position >= 0 ? position
                : throw new LythonHostException(LythonHostFailureKind.Transport, "host file position cannot be negative");
        }, "file_metadata", span, services.MemoryGovernor);

    internal long? LengthHint(LythonSourceSpan? span)
        => HostOperation.Invoke(() => _file!.LengthHint, "file_metadata", span, services.MemoryGovernor);

    internal async ValueTask<int> ReadAsync(Memory<byte> buffer, LythonSourceSpan? span, bool asynchronous)
    {
        services.CheckExecution(span);
        var count = asynchronous
            ? await HostOperation.AwaitAsync(() => _file!.ReadAsync(buffer, services.Limits.CancellationToken),
                "read_file", span, services.MemoryGovernor).ConfigureAwait(false)
            : HostOperation.Await(_file!, () => _file!.ReadAsync(buffer, services.Limits.CancellationToken),
                "read_file", span, services.MemoryGovernor);
        services.CheckExecution(span);
        if (count < 0 || count > buffer.Length)
            HostOperation.Invoke<int>(static () => throw new LythonHostException(LythonHostFailureKind.Transport,
                "host file read exceeded its buffer"), "read_file", span, services.MemoryGovernor);
        return count;
    }

    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, LythonSourceSpan? span, bool asynchronous)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            services.CheckExecution(span);
            var remaining = buffer[offset..];
            LythonFileWriteResult result;
            try
            {
                result = asynchronous
                    ? await HostOperation.AwaitAsync(() => _file!.WriteAsync(remaining, services.Limits.CancellationToken),
                        "write_file", span, services.MemoryGovernor).ConfigureAwait(false)
                    : HostOperation.Await(_file!, () => _file!.WriteAsync(remaining, services.Limits.CancellationToken),
                        "write_file", span, services.MemoryGovernor);
            }
            catch
            {
                // Throwing gives no accepted-prefix guarantee. Join cleanup and
                // retain the operation's first failure, including admission.
                await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                throw;
            }
            var count = result.AcceptedBytes;
            if (result.Failure is { Kind: LythonHostFailureKind.MemoryAdmission } refusal)
            {
                try { HostOperation.Invoke<object>(() => throw refusal, "write_file", span, services.MemoryGovernor); }
                finally { await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false); }
            }
            if (count is < 0 || count > remaining.Length || (result.Failure is null && count is null or 0))
            {
                await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                HostOperation.Invoke<object>(() => throw new LythonHostException(LythonHostFailureKind.Transport,
                    "host file write reported an invalid accepted prefix"), "write_file", span, services.MemoryGovernor);
            }
            if (result.Failure is { } failure)
            {
                try { HostOperation.Invoke<object>(() => throw failure, "write_file", span, services.MemoryGovernor); }
                catch
                {
                    if (count != 0 || offset != 0) await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
                    throw;
                }
            }
            offset += count!.Value;
        }
    }

    internal async ValueTask RewindAndTruncateAsync(LythonSourceSpan? span, bool asynchronous)
    {
        services.CheckExecution(span);
        try
        {
            if (asynchronous)
                await HostOperation.AwaitAsync(() => _file!.RewindAndTruncateAsync(services.Limits.CancellationToken),
                    "rewrite_file", span, services.MemoryGovernor).ConfigureAwait(false);
            else HostOperation.Await(_file!, () => _file!.RewindAndTruncateAsync(services.Limits.CancellationToken),
                "rewrite_file", span, services.MemoryGovernor);
            services.CheckExecution(span);
        }
        catch
        {
            await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
            throw;
        }
    }

    internal async ValueTask FlushAsync(LythonSourceSpan? span, bool asynchronous)
    {
        services.CheckExecution(span);
        try
        {
            if (asynchronous)
                await HostOperation.AwaitAsync(() => _file!.FlushAsync(services.Limits.CancellationToken),
                    "flush_file", span, services.MemoryGovernor).ConfigureAwait(false);
            else HostOperation.Await(_file!, () => _file!.FlushAsync(services.Limits.CancellationToken),
                "flush_file", span, services.MemoryGovernor);
            services.CheckExecution(span);
        }
        catch (SynchronousHostContractException)
        {
            await CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false);
            throw;
        }
    }

    internal async ValueTask CloseAsync(LythonSourceSpan? span, bool asynchronous)
    {
        if (IsClosed) return;
        IsClosed = true;
        var file = _file; _file = null;
        if (file is null) return;
        // Retirement cannot be rejected because the token was cancelled or a
        // provider cannot promise synchronous completion. Always join disposal.
        if (asynchronous)
            await HostOperation.AwaitAsync(file.DisposeAsync, "close_file", span, services.MemoryGovernor).ConfigureAwait(false);
        else HostOperation.AwaitAsync(file.DisposeAsync, "close_file", span, services.MemoryGovernor)
            .AsTask().GetAwaiter().GetResult();
    }

    internal async ValueTask CloseAfterFailureAsync(LythonSourceSpan? span, bool asynchronous)
    {
        try { await CloseAsync(span, asynchronous).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { /* Preserve the operation's first failure after actual cleanup. */ }
    }
}
