namespace Lokad.Lython;

/// <summary>Exposes the CPython version family whose language behavior Lython targets.</summary>
public static class LythonPythonVersion
{
    /// <summary>The targeted Python major version.</summary>
    public const int Major = 3;

    /// <summary>The targeted Python minor version.</summary>
    public const int Minor = 13;

    /// <summary>The reported Python micro version.</summary>
    public const int Micro = 0;

    /// <summary>The reported Python release level.</summary>
    public const string ReleaseLevel = "final";

    /// <summary>The reported Python release serial.</summary>
    public const int Serial = 0;

    /// <summary>The targeted Python language family.</summary>
    public const string VersionFamily = "Python 3.13";

    /// <summary>The Python-compatible semantic version string.</summary>
    public const string Version = "3.13.0";

    /// <summary>The implementation-qualified version displayed by Lython.</summary>
    public const string DisplayVersion = "3.13.0 (Lython)";

    /// <summary>The cache tag reported for Lython-generated artifacts.</summary>
    public const string CacheTag = "lython-3.13";

    /// <summary>The Python-compatible packed hexadecimal version.</summary>
    public const int HexVersion =
        (Major << 24) | (Minor << 16) | (Micro << 8) | 0xF0;
}
