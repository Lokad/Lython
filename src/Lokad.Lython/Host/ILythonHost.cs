namespace Lokad.Lython;

public interface ILythonHost
{
    string Cwd { get; }

    DateTimeOffset LocalNow { get; }

    DateTimeOffset UtcNow { get; }

    ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken);

    ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken);

    ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        => throw new NotSupportedException("Host binary file I/O is not available.");

    ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        => throw new NotSupportedException("Host binary file I/O is not available.");

    ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken);

    ValueTask MkDirAsync(string path, CancellationToken cancellationToken);

    ValueTask RemoveAsync(string path, CancellationToken cancellationToken);

    ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken);

    ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken);

    ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken);

    ILythonTextInput? StandardInput => null;

    ILythonTextOutput? StandardOutput => null;

    ILythonTextOutput? StandardError => null;

    ILythonSubprocessRunner? SubprocessRunner => null;

    ILythonTiming? Timing => null;

    async IAsyncEnumerable<LythonWalkEntry> WalkAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
