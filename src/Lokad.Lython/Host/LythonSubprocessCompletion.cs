namespace Lokad.Lython;

/// <summary>Writes subprocess bytes to an inherited host stream.</summary>
public delegate ValueTask LythonSubprocessOutputWriter(
    ReadOnlyMemory<byte> utf8,
    CancellationToken cancellationToken);

/// <summary>Helpers for applying Lython subprocess stream modes.</summary>
public static class LythonSubprocessCompletion
{
    /// <summary>Routes borrowed buffers and copies captured bytes into one owned completion.</summary>
    /// <remarks>
    /// The host keeps acquired buffers funded and valid through this operation.
    /// Captured storage gets an independent owner before allocation. Inherited
    /// writes borrow the input through their actual completion. Separate stderr
    /// redirected to stdout is appended after stdout; lost interleaving cannot
    /// be reconstructed. Dispose the returned result after its readers finish.
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
        var redirected = request.StandardError == LythonSubprocessStreamMode.StandardOutput;
        var stderrMode = redirected ? request.StandardOutput : request.StandardError;
        ValidateOutputMode(request.StandardOutput);
        ValidateOutputMode(stderrMode);
        var length = checked((request.StandardOutput == LythonSubprocessStreamMode.Pipe ? standardOutputUtf8.Length : 0)
            + (stderrMode == LythonSubprocessStreamMode.Pipe ? standardErrorUtf8.Length : 0));
        var owner = (request.MemoryBudget ?? new LythonMemoryBudget()).CreateScope();
        try
        {
            // One backing holds both outputs. A returned slice never borrows
            // host acquisition storage that may end with RunAsync's callback.
            // Admission precedes inherited writes as well as allocation.
            if (length > 0 && !owner.TryReserve(checked(128L + length), out _))
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission,
                    "shared memory budget refused subprocess capture storage.", requestedBytes: checked(128L + length));
            var stdout = await RouteBufferedOutputAsync(request.StandardOutput, standardOutputUtf8,
                inheritStandardOutputAsync, cancellationToken).ConfigureAwait(false);
            var stderr = await RouteBufferedOutputAsync(stderrMode, standardErrorUtf8,
                redirected ? inheritStandardOutputAsync : inheritStandardErrorAsync,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var captured = length == 0 ? Array.Empty<byte>() : new byte[length];
            Copy(stdout, captured);
            Copy(stderr, captured.AsMemory(stdout.Length));
            cancellationToken.ThrowIfCancellationRequested();
            return new LythonSubprocessResult(returnCode,
                captured.AsMemory(0, redirected ? length : stdout.Length),
                redirected ? ReadOnlyMemory<byte>.Empty : captured.AsMemory(stdout.Length), owner);
        }
        catch { owner.Dispose(); throw; }

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

    private static void ValidateOutputMode(LythonSubprocessStreamMode mode)
    {
        if (mode is LythonSubprocessStreamMode.Inherit or LythonSubprocessStreamMode.Pipe or LythonSubprocessStreamMode.DevNull)
            return;
        throw new ArgumentException("subprocess.STDOUT is only valid for standard error; expected a valid output mode.", nameof(mode));
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
