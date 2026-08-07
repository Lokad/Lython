using System.Globalization;
using System.Numerics;

namespace Lokad.Lython;

public enum LythonPathKind
{
    Missing,
    File,
    Directory,
}

public sealed record LythonPathStat
{
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

    [Obsolete("Use the LythonPathKind and DateTimeOffset constructor.")]
    public LythonPathStat(bool exists, bool isFile, bool isDir, BigInteger size, string modifiedAt)
        : this(
            ResolveKind(exists, isFile, isDir),
            size,
            exists ? ParseModifiedAt(modifiedAt) : null)
    {
    }

    public LythonPathKind Kind { get; }

    public bool Exists => Kind != LythonPathKind.Missing;

    public bool IsFile => Kind == LythonPathKind.File;

    public bool IsDir => Kind == LythonPathKind.Directory;

    public BigInteger Size { get; }

    public DateTimeOffset? ModifiedAtTimestamp { get; }

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
