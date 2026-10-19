namespace Lokad.Lython;

/// <summary>Writes subprocess bytes to an inherited host stream.</summary>
public delegate ValueTask LythonSubprocessOutputWriter(
    ReadOnlyMemory<byte> utf8,
    CancellationToken cancellationToken);

/// <summary>Helpers for applying Lython subprocess stream modes.</summary>
public static class LythonSubprocessCompletion
{
    /// <summary>Completes a subprocess request from already-buffered stdout and stderr.</summary>
    /// <remarks>
    /// When stderr is redirected to stdout but the host only has separate buffers,
    /// stderr is appended after stdout because stream interleaving has already been lost.
    /// The combined copy borrows request.MemoryBudget and remains charged until that
    /// owner ends. The host must fund its acquired buffers and finish all users first.
    /// </remarks>
    public static async ValueTask<LythonSubprocessResult> CompleteBufferedAsync(
        LythonSubprocessRequest request,
        int returnCode,
        ReadOnlyMemory<byte> standardOutputUtf8,
        ReadOnlyMemory<byte> standardErrorUtf8,
        LythonSubprocessOutputWriter inheritStandardOutputAsync,
        LythonSubprocessOutputWriter inheritStandardErrorAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.StandardError == LythonSubprocessStreamMode.StandardOutput)
        {
            switch (request.StandardOutput)
            {
                case LythonSubprocessStreamMode.Pipe:

                    return new LythonSubprocessResult(
                        returnCode,
                        Combine(standardOutputUtf8, standardErrorUtf8),
                        ReadOnlyMemory<byte>.Empty);

                case LythonSubprocessStreamMode.Inherit:
                    if (!standardOutputUtf8.IsEmpty)
                    {
                        await inheritStandardOutputAsync(standardOutputUtf8, cancellationToken).ConfigureAwait(false);
                    }

                    if (!standardErrorUtf8.IsEmpty)
                    {
                        await inheritStandardOutputAsync(standardErrorUtf8, cancellationToken).ConfigureAwait(false);
                    }

                    return new LythonSubprocessResult(
                        returnCode,
                        ReadOnlyMemory<byte>.Empty,
                        ReadOnlyMemory<byte>.Empty);

                case LythonSubprocessStreamMode.DevNull:
                    return new LythonSubprocessResult(
                        returnCode,
                        ReadOnlyMemory<byte>.Empty,
                        ReadOnlyMemory<byte>.Empty);

                case LythonSubprocessStreamMode.StandardOutput:
                    throw new ArgumentException("subprocess.STDOUT is only valid for standard error.", nameof(request));

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.StandardOutput, null);
            }
        }

        var capturedStdout = await RouteBufferedOutputAsync(
                request.StandardOutput,
                standardOutputUtf8,
                inheritStandardOutputAsync,
                cancellationToken)
            .ConfigureAwait(false);
        var capturedStderr = await RouteBufferedOutputAsync(
                request.StandardError,
                standardErrorUtf8,
                inheritStandardErrorAsync,
                cancellationToken)
            .ConfigureAwait(false);

        return new LythonSubprocessResult(returnCode, capturedStdout, capturedStderr);

        ReadOnlyMemory<byte> Combine(ReadOnlyMemory<byte> first, ReadOnlyMemory<byte> second)
        {
            if (first.Length == 0)
                return second;

            if (second.Length == 0)
                return first;

            var length = checked(first.Length + second.Length);
            var governor = request.MemoryGovernor ?? new Runtime.MemoryGovernor(request.MemoryBudget);
            using var storage = governor.ReserveTemporary(32L + length, null);
            var combined = new byte[length];
            Copy(first, combined);
            Copy(second, combined.AsMemory(first.Length));
            storage.Commit(32L + length);
            return combined;
        }

        void Copy(ReadOnlyMemory<byte> source, Memory<byte> target)
        {
            const int chunk = 64 * 1024;
            for (var offset = 0; offset < source.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(chunk, source.Length - offset);
                source.Slice(offset, count).CopyTo(target.Slice(offset, count));
                offset += count;
            }
        }
    }

    private static async ValueTask<ReadOnlyMemory<byte>> RouteBufferedOutputAsync(
        LythonSubprocessStreamMode mode,
        ReadOnlyMemory<byte> utf8,
        LythonSubprocessOutputWriter inheritOutputAsync,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case LythonSubprocessStreamMode.Pipe:

                return utf8;

            case LythonSubprocessStreamMode.Inherit:
                if (utf8.Length != 0)
                    await inheritOutputAsync(utf8, cancellationToken).ConfigureAwait(false);
                return ReadOnlyMemory<byte>.Empty;

            case LythonSubprocessStreamMode.DevNull:
                return ReadOnlyMemory<byte>.Empty;

            case LythonSubprocessStreamMode.StandardOutput:
                throw new ArgumentException("subprocess.STDOUT is only valid for standard error.", nameof(mode));

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

}
