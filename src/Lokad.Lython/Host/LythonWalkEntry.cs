namespace Lokad.Lython;

/// <summary>Describes one directory yielded by the host-mediated directory walker.</summary>
/// <param name="DirectoryPath">The normalized directory path.</param>
/// <param name="DirectoryNames">Immediate child directory names.</param>
/// <param name="FileNames">Immediate child file names.</param>
public sealed record LythonWalkEntry(
    string DirectoryPath,
    IReadOnlyList<string> DirectoryNames,
    IReadOnlyList<string> FileNames);
