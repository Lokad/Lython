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
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(inheritStandardOutputAsync);
        ArgumentNullException.ThrowIfNull(inheritStandardErrorAsync);

        if (request.StandardError == LythonSubprocessStreamMode.StandardOutput)
        {
            var combined = Combine(standardOutputUtf8, standardErrorUtf8);
            var captured = await RouteBufferedOutputAsync(
                    request.StandardOutput,
                    combined,
                    request.MaxOutputBytes,
                    inheritStandardOutputAsync,
                    cancellationToken)
                .ConfigureAwait(false);
            return new LythonSubprocessResult(returnCode, captured, ReadOnlyMemory<byte>.Empty);
        }

        var capturedStdout = await RouteBufferedOutputAsync(
                request.StandardOutput,
                standardOutputUtf8,
                request.MaxOutputBytes,
                inheritStandardOutputAsync,
                cancellationToken)
            .ConfigureAwait(false);
        var capturedStderr = await RouteBufferedOutputAsync(
                request.StandardError,
                standardErrorUtf8,
                request.MaxOutputBytes,
                inheritStandardErrorAsync,
                cancellationToken)
            .ConfigureAwait(false);
        return new LythonSubprocessResult(returnCode, capturedStdout, capturedStderr);
    }

    private static async ValueTask<ReadOnlyMemory<byte>> RouteBufferedOutputAsync(
        LythonSubprocessStreamMode mode,
        ReadOnlyMemory<byte> utf8,
        long? maxOutputBytes,
        LythonSubprocessOutputWriter inheritOutputAsync,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case LythonSubprocessStreamMode.Pipe:
                return maxOutputBytes is > 0 && utf8.Length > maxOutputBytes.Value
                    ? utf8.Slice(0, (int)maxOutputBytes.Value)
                    : utf8;

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
