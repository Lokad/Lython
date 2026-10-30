namespace Lokad.Lython;

/// <summary>
/// Defines every ambient capability visible to a Lython execution.
/// </summary>
/// <remarks>
/// Paths use the host's contained namespace. OpenFileAsync receives the original
/// path and explicit contained Cwd, preserving dot segments and trailing slashes.
/// The older string-path operations receive absolute POSIX-style
/// paths without backslashes, NUL characters, unresolved dot segments, or Windows
/// drive prefixes. Implementations must still map them beneath their contained root,
/// honor cancellation, avoid ambient process-wide state, and report unsupported
/// operations explicitly.
/// Callbacks may run on reused runtime threads or async continuation threads;
/// implementations must not depend on thread identity or thread-local state
/// surviving between executions.
/// Operations may report <see cref="LythonHostException"/> with safe contained
/// diagnostic text and a Python-shaped failure category. A reported memory
/// admission refusal terminates the command after cleanup; cancellation uses
/// OperationCanceledException. Unknown CLR failures retain redacted diagnostics.
/// </remarks>
public interface ILythonHost
{
    /// <summary>Gets the absolute working directory used to resolve relative Lython paths.</summary>
    string Cwd { get; }

    /// <summary>Gets the host-mediated local wall-clock reading.</summary>
    DateTimeOffset LocalNow { get; }

    /// <summary>Gets the host-mediated UTC wall-clock reading.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Atomically opens one owned byte-file identity in the contained namespace.</summary>
    /// <remarks>Preserve request path intent until host resolution. Reserve acquisition
    /// scratch before allocation or create/truncate effects, using the borrowed allowance.
    /// Persistent host storage needs a host-owned lifetime. On failure the host retires
    /// unpublished resources. No whole-file or path-reopening fallback is provided.</remarks>
    ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
        ILythonMemoryBudget memoryBudget, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("owned byte-file I/O");

    /// <summary>Reads a complete UTF-8 text file from the contained path.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken);

    /// <summary>Reads at most <paramref name="count"/> bytes of a UTF-8 text file starting at <paramref name="offset"/>.
    /// <remarks>
    /// Offsets and lengths are byte-based, so a window may split a UTF-8 sequence;
    /// decoding split windows rests with the caller. An offset at or past
    /// the end yields an empty payload. Negative offsets or counts fail explicitly.
    /// The default implementation composes <see cref="ReadTextUtf8Async"/> and is
    /// therefore correct but unbounded; hosts with native ranged I/O should override
    /// it so large files can be acquired in a bounded window.
    /// </remarks>
    async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, CancellationToken cancellationToken)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Range offset cannot be negative.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Range count cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var whole = await ReadTextUtf8Async(path, cancellationToken).ConfigureAwait(false);
        if (offset >= whole.Length || count == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var take = (int)Math.Min(count, (long)whole.Length - offset);
        return whole.Slice((int)offset, take);
    }

    /// <summary>Replaces a contained text file with the supplied well-formed UTF-8 bytes.</summary>
    /// <remarks>
    /// Write-buffer ownership: the runtime supplies a freshly-allocated buffer on every call
    /// and never mutates or reuses it afterwards. The buffer is guaranteed valid only until the
    /// returned task completes, so consume it (decode, hash, stream to disk) or copy it before
    /// completing; a synchronous run additionally requires an already-completed task (see
    /// <see cref="ILythonSynchronousHostCapability"/>). Bytes retained afterwards are host-owned
    /// storage, never charged against the execution memory budget: a host that keeps every
    /// completed file grows with the data it keeps by its own policy, not by a runtime leak. A
    /// host that persists externally (streams the payload out, then drops it) needs no second
    /// resident copy of a completed file.
    /// </remarks>
    ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    /// <summary>Appends well-formed UTF-8 bytes to a contained text file.</summary>
    /// <remarks>
    /// Write-buffer ownership: the runtime supplies a freshly-allocated buffer on every call
    /// and never mutates or reuses it afterwards. The buffer is guaranteed valid only until the
    /// returned task completes, so consume it (decode, hash, stream to disk) or copy it before
    /// completing; a synchronous run additionally requires an already-completed task (see
    /// <see cref="ILythonSynchronousHostCapability"/>). Bytes retained afterwards are host-owned
    /// storage, never charged against the execution memory budget: a host that keeps every
    /// completed file grows with the data it keeps by its own policy, not by a runtime leak. A
    /// host that persists externally (streams the payload out, then drops it) needs no second
    /// resident copy of a completed file.
    /// </remarks>
    ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    /// <summary>Reads a complete binary file when the host exposes binary I/O.</summary>
    /// <remarks>The default implementation rejects binary access explicitly.</remarks>
    ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("binary file I/O");

    /// <summary>Replaces a contained binary file when the host exposes binary I/O.</summary>
    /// <remarks>The default implementation rejects binary access explicitly.
    /// Write-buffer ownership: the runtime supplies a freshly-allocated buffer on every call
    /// and never mutates or reuses it afterwards. The buffer is guaranteed valid only until the
    /// returned task completes, so consume it or copy it before completing; a synchronous run
    /// additionally requires an already-completed task (see
    /// <see cref="ILythonSynchronousHostCapability"/>). Bytes retained afterwards are host-owned
    /// storage, never charged against the execution memory budget.
    /// </remarks>
    ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("binary file I/O");

    /// <summary>Appends bytes to a contained binary file when the host exposes binary I/O.</summary>
    /// <remarks>
    /// Implementations must preserve the existing prefix and create a missing file. The default
    /// implementation composes the other host operations and is therefore neither atomic nor
    /// allocation-free; hosts with native append support should override it.
    /// Write-buffer ownership: the runtime supplies a freshly-allocated buffer on every call
    /// and never mutates or reuses it afterwards. The buffer is guaranteed valid only until the
    /// returned task completes, so consume it or copy it before completing; a synchronous run
    /// additionally requires an already-completed task (see
    /// <see cref="ILythonSynchronousHostCapability"/>). Bytes retained afterwards are host-owned
    /// storage, never charged against the execution memory budget.
    /// </remarks>
    async ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stat = await StatAsync(path, cancellationToken).ConfigureAwait(false);
        if (!stat.Exists)
        {
            await WriteBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        var prefix = await ReadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var combined = new byte[checked(prefix.Length + bytes.Length)];
        prefix.CopyTo(combined);
        bytes.CopyTo(combined.AsMemory(prefix.Length));
        await WriteBytesAsync(path, combined, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads at most <paramref name="count"/> bytes of a binary file starting at <paramref name="offset"/>.
    /// <remarks>
    /// An offset at or past the end yields an empty payload. Negative offsets or
    /// counts fail explicitly. The default implementation composes
    /// <see cref="ReadBytesAsync"/> and is therefore correct but unbounded; hosts
    /// with native ranged I/O should override it so large files can be acquired in
    /// a bounded window.
    /// </remarks>
    async ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path, long offset, int count, CancellationToken cancellationToken)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Range offset cannot be negative.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Range count cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var whole = await ReadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (offset >= whole.Length || count == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var take = (int)Math.Min(count, (long)whole.Length - offset);
        return whole.Slice((int)offset, take);
    }

    /// <summary>Determines whether a file-system entry exists at the contained path.</summary>
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);

    /// <summary>Lists direct child names without recursively traversing the contained directory.</summary>
    ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken);

    /// <summary>Creates one contained directory and fails if its parent is unavailable.</summary>
    ValueTask MkDirAsync(string path, CancellationToken cancellationToken);

    /// <summary>Removes one contained file or empty directory.</summary>
    ValueTask RemoveAsync(string path, CancellationToken cancellationToken);

    /// <summary>Removes a regular file, never a directory.</summary>
    /// <remarks>The host must enforce the kind at mutation time. A missing entry
    /// succeeds only when missingOk is true. No stat/remove fallback is provided.</remarks>
    ValueTask RemoveFileAsync(string path, bool missingOk, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("file removal");

    /// <summary>Removes an empty directory, never a regular file.</summary>
    /// <remarks>The host must enforce the kind at mutation time.</remarks>
    ValueTask RemoveDirectoryAsync(string path, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("directory removal");

    /// <summary>Replaces a contained destination with the source as one host operation.</summary>
    /// <remarks>Self-replacement of an existing entry succeeds without removing it.
    /// Missing sources, incompatible kinds and rejected operations must preserve
    /// the destination. Hosts must implement their storage's replacement semantics;
    /// Lython never emulates replacement by deleting the old destination first.</remarks>
    ValueTask ReplaceAsync(string source, string destination, CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("path replacement");

    /// <summary>Copies one contained file without overwriting an existing destination.</summary>
    ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken);

    /// <summary>Moves one contained file without overwriting an existing destination.</summary>
    ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken);

    /// <summary>Returns precise metadata for a contained path, including missing paths.</summary>
    ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken);

    /// <summary>Gets optional host-mediated standard input.</summary>
    ILythonTextInput? StandardInput => null;

    /// <summary>Gets optional host-mediated standard output.</summary>
    ILythonTextOutput? StandardOutput => null;

    /// <summary>Gets optional host-mediated standard error.</summary>
    ILythonTextOutput? StandardError => null;

    /// <summary>Gets the optional contained subprocess capability.</summary>
    ILythonSubprocessRunner? SubprocessRunner => null;

    /// <summary>Gets the optional monotonic-clock and delay capability.</summary>
    ILythonTiming? Timing => null;

}
