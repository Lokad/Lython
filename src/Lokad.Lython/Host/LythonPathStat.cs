using System.Globalization;
using System.Numerics;

namespace Lokad.Lython;

/// <summary>Identifies the kind of a host path without contradictory boolean states.</summary>
public enum LythonPathKind
{
    /// <summary>The path does not exist.</summary>
    Missing,
    /// <summary>The path names a regular file.</summary>
    File,
    /// <summary>The path names a directory.</summary>
    Directory,
}

/// <summary>Contains host-provided metadata for a mediated path.</summary>
public sealed record LythonPathStat
{
    /// <summary>Creates validated metadata for a path.</summary>
    public LythonPathStat(LythonPathKind kind, BigInteger size, DateTimeOffset? modifiedAt)
    {
        if (size < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Path size cannot be negative.");
        }

        if (kind == LythonPathKind.Missing && (size != BigInteger.Zero || modifiedAt is not null))
        {
            throw new ArgumentException("A missing path cannot have size or modification metadata.", nameof(kind));
        }

        Kind = kind;
        Size = size;
        ModifiedAtTimestamp = modifiedAt;
    }

    /// <summary>Creates path metadata from the legacy boolean and timestamp representation.</summary>
    [Obsolete("Use the LythonPathKind and DateTimeOffset constructor.")]
    public LythonPathStat(bool exists, bool isFile, bool isDir, BigInteger size, string modifiedAt)
        : this(
            ResolveKind(exists, isFile, isDir),
            size,
            exists ? ParseModifiedAt(modifiedAt) : null)
    {
    }

    /// <summary>Gets the precise path kind.</summary>
    public LythonPathKind Kind { get; }

    /// <summary>Gets whether the path exists.</summary>
    public bool Exists => Kind != LythonPathKind.Missing;

    /// <summary>Gets whether the path is a file.</summary>
    public bool IsFile => Kind == LythonPathKind.File;

    /// <summary>Gets whether the path is a directory.</summary>
    public bool IsDir => Kind == LythonPathKind.Directory;

    /// <summary>Gets the file size in bytes, or zero for a missing path or directory.</summary>
    public BigInteger Size { get; }

    /// <summary>Gets the last-modified timestamp when the host provides one.</summary>
    public DateTimeOffset? ModifiedAtTimestamp { get; }

    /// <summary>Gets the legacy ISO-8601 last-modified representation.</summary>
    public string ModifiedAt => ModifiedAtTimestamp?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static LythonPathKind ResolveKind(bool exists, bool isFile, bool isDir)
    {
        if (!exists && !isFile && !isDir)
        {
            return LythonPathKind.Missing;
        }

        if (exists && isFile != isDir)
        {
            return isFile ? LythonPathKind.File : LythonPathKind.Directory;
        }

        throw new ArgumentException("Path metadata must describe exactly one of missing, file, or directory.");
    }

    private static DateTimeOffset ParseModifiedAt(string modifiedAt)
    {
        if (DateTimeOffset.TryParse(
                modifiedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return timestamp;
        }

        throw new ArgumentException("Path modification time must be an ISO-8601 timestamp.", nameof(modifiedAt));
    }
}
