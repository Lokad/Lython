namespace Lokad.Lython;

/// <summary>
/// Defines every ambient capability visible to a Lython execution.
/// </summary>
/// <remarks>
/// Paths use the host's contained namespace. Implementations must honor cancellation,
/// avoid ambient process-wide state, and report unsupported operations explicitly.
/// </remarks>
public interface ILythonHost
{
    /// <summary>Gets the absolute working directory used to resolve relative Lython paths.</summary>
    string Cwd { get; }

    /// <summary>Gets the host-mediated local wall-clock reading.</summary>
    DateTimeOffset LocalNow { get; }

    /// <summary>Gets the host-mediated UTC wall-clock reading.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Reads a complete UTF-8 text file from the contained path.</summary>
    ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken);

    /// <summary>Replaces a contained text file with the supplied well-formed UTF-8 bytes.</summary>
    ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    /// <summary>Appends well-formed UTF-8 bytes to a contained text file.</summary>
    ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    /// <summary>Reads a complete binary file when the host exposes binary I/O.</summary>
    /// <remarks>The default implementation rejects binary access explicitly.</remarks>
    ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        => throw new NotSupportedException("Host binary file I/O is not available.");

    /// <summary>Replaces a contained binary file when the host exposes binary I/O.</summary>
    /// <remarks>The default implementation rejects binary access explicitly.</remarks>
    ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        => throw new NotSupportedException("Host binary file I/O is not available.");

    /// <summary>Appends bytes to a contained binary file when the host exposes binary I/O.</summary>
    /// <remarks>
    /// Implementations must preserve the existing prefix and create a missing file. The default
    /// implementation composes the other host operations and is therefore neither atomic nor
    /// allocation-free; hosts with native append support should override it.
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

    /// <summary>Determines whether a file-system entry exists at the contained path.</summary>
    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);

    /// <summary>Lists direct child names without recursively traversing the contained directory.</summary>
    ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken);

    /// <summary>Creates one contained directory and fails if its parent is unavailable.</summary>
    ValueTask MkDirAsync(string path, CancellationToken cancellationToken);

    /// <summary>Removes one contained file or empty directory.</summary>
    ValueTask RemoveAsync(string path, CancellationToken cancellationToken);

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

    /// <summary>Recursively walks a contained directory without cancellation.</summary>
    IAsyncEnumerable<LythonWalkEntry> WalkAsync(string path)
        => WalkAsync(path, CancellationToken.None);

    /// <summary>Recursively walks a contained directory in deterministic name order.</summary>
    /// <remarks>
    /// The default implementation composes <see cref="StatAsync"/> and
    /// <see cref="ListDirAsync"/> and honors cancellation on every host call.
    /// </remarks>
    async IAsyncEnumerable<LythonWalkEntry> WalkAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in EnumerateWalk(this, path, cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        static async IAsyncEnumerable<LythonWalkEntry> EnumerateWalk(
            ILythonHost host,
            string root,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var normalized = Normalize(root);
            var stat = await host.StatAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (!stat.Exists)
            {
                throw new InvalidOperationException($"Directory does not exist: {normalized}");
            }

            if (!stat.IsDir)
            {
                throw new InvalidOperationException($"Path is not a directory: {normalized}");
            }

            await foreach (var item in WalkDirectory(host, normalized, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }

        static async IAsyncEnumerable<LythonWalkEntry> WalkDirectory(
            ILythonHost host,
            string directory,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var names = await host.ListDirAsync(directory, cancellationToken).ConfigureAwait(false);
            var directories = new List<string>();
            var files = new List<string>();

            foreach (var name in names)
            {
                var child = directory == "/" ? "/" + name : directory + "/" + name;
                var stat = await host.StatAsync(child, cancellationToken).ConfigureAwait(false);
                if (!stat.Exists)
                {
                    continue;
                }

                if (stat.IsDir)
                {
                    directories.Add(name);
                }
                else if (stat.IsFile)
                {
                    files.Add(name);
                }
            }

            directories.Sort(StringComparer.Ordinal);
            files.Sort(StringComparer.Ordinal);
            yield return new LythonWalkEntry(directory, directories.ToArray(), files.ToArray());

            foreach (var name in directories)
            {
                var child = directory == "/" ? "/" + name : directory + "/" + name;
                await foreach (var item in WalkDirectory(host, child, cancellationToken).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized[..^1];
            }

            return normalized;
        }
    }
}
