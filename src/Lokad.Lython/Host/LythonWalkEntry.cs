namespace Lokad.Lython;

public sealed record LythonWalkEntry(
    string DirectoryPath,
    IReadOnlyList<string> DirectoryNames,
    IReadOnlyList<string> FileNames);
