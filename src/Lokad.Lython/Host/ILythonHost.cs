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
