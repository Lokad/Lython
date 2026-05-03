using System.Text.Json;

namespace Lokad.Lython.Tests.Harness;

internal static class FixtureLoader
{
    public static LythonFixture Load(string relativePath)
    {
        var fixtureDirectory = Path.Combine(FindFixturesRoot(), relativePath);
        if (!Directory.Exists(fixtureDirectory))
        {
            throw new DirectoryNotFoundException($"Fixture directory does not exist: {fixtureDirectory}");
        }

        return new LythonFixture(
            fixtureDirectory,
            File.ReadAllText(Path.Combine(fixtureDirectory, "script.py")),
            LoadTree(Path.Combine(fixtureDirectory, "input")),
            LoadTree(Path.Combine(fixtureDirectory, "expected")),
            LoadResult(Path.Combine(fixtureDirectory, "result.json")));
    }

    private static FixtureResult LoadResult(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })
            ?? throw new InvalidOperationException($"Could not deserialize fixture result: {path}");
    }

    private static IReadOnlyDictionary<string, string> LoadTree(string root)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
        {
            return files;
        }

        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file)
                .Replace('\\', '/');
            files["/" + relative] = File.ReadAllText(file);
        }

        return files;
    }

    private static string FindFixturesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the test Fixtures directory.");
    }
}

internal sealed record LythonFixture(
    string Directory,
    string Script,
    IReadOnlyDictionary<string, string> InputFiles,
    IReadOnlyDictionary<string, string> ExpectedFiles,
    FixtureResult Result);

internal sealed record FixtureResult(
    bool Success,
    string? ExceptionType,
    string? DiagnosticCode);
