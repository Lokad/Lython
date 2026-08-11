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
        if (request.StandardError == LythonSubprocessStreamMode.StandardOutput)
        {
            switch (request.StandardOutput)
            {
                case LythonSubprocessStreamMode.Pipe:
                    EnsureWithinOutputLimit(
                        "combined standard output",
                        checked((long)standardOutputUtf8.Length + standardErrorUtf8.Length),
                        request.OutputLimit);
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
                "standard output",
                standardOutputUtf8,
                request.OutputLimit,
                inheritStandardOutputAsync,
                cancellationToken)
            .ConfigureAwait(false);
        var capturedStderr = await RouteBufferedOutputAsync(
                request.StandardError,
                "standard error",
                standardErrorUtf8,
                request.OutputLimit,
                inheritStandardErrorAsync,
                cancellationToken)
            .ConfigureAwait(false);
        EnsureWithinOutputLimit(
            "combined captured output",
            checked((long)capturedStdout.Length + capturedStderr.Length),
            request.OutputLimit);
        return new LythonSubprocessResult(returnCode, capturedStdout, capturedStderr);
    }

    private static async ValueTask<ReadOnlyMemory<byte>> RouteBufferedOutputAsync(
        LythonSubprocessStreamMode mode,
        string streamName,
        ReadOnlyMemory<byte> utf8,
        LythonSubprocessOutputLimit? outputLimit,
        LythonSubprocessOutputWriter inheritOutputAsync,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case LythonSubprocessStreamMode.Pipe:
                EnsureWithinOutputLimit(streamName, utf8.Length, outputLimit);
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

    private static void EnsureWithinOutputLimit(
        string streamName,
        long actualBytes,
        LythonSubprocessOutputLimit? outputLimit)
    {
        if (outputLimit is { } limit && actualBytes > limit.Bytes)
        {
            throw new LythonSubprocessOutputLimitException(streamName, actualBytes, limit.Bytes);
        }
    }

    private static ReadOnlyMemory<byte> Combine(ReadOnlyMemory<byte> first, ReadOnlyMemory<byte> second)
    {
        if (first.Length == 0)
            return second;

        if (second.Length == 0)
            return first;

        var combined = new byte[first.Length + second.Length];
        first.CopyTo(combined);
        second.CopyTo(combined.AsMemory(first.Length));
        return combined;
    }
}
