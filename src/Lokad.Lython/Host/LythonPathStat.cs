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

}
