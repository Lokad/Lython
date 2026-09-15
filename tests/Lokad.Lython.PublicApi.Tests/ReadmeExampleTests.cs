using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// D01: the representative script in README.md is executable documentation:
// this test extracts it verbatim, runs it against a seeded host, and checks
// both output files, so the documented example can never silently rot.
public sealed class ReadmeExampleTests
{
    private static string LoadRepresentativeScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "README.md");
            if (File.Exists(candidate) && HasRepresentativeScript(candidate))
            {
                var text = File.ReadAllText(candidate);
                var section = text.IndexOf("## Representative Script", StringComparison.Ordinal);
                Assert.True(section >= 0, "README.md lost its Representative Script section.");
                var fence = text.IndexOf("```python", section, StringComparison.Ordinal);
                Assert.True(fence >= 0, "README.md lost its representative python fence.");
                var start = fence + "```python".Length;
                var end = text.IndexOf("```", start, StringComparison.Ordinal);
                Assert.True(end > start, "README.md representative fence is not closed.");
                return text.Substring(start, end - start).Trim() + "\n";
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("README.md was not found above " + AppContext.BaseDirectory);
    }

    private static bool HasRepresentativeScript(string path)
    {
        // A nearer README (for example the tests folder) must not shadow
        // the repository one: keep walking until the documented section appears.
        return File.ReadAllText(path).Contains("## Representative Script", StringComparison.Ordinal);
    }

    [Fact]
    public void RepresentativeScriptProducesBothOutputs()
    {
        var script = new LythonEngine().Compile(LoadRepresentativeScript());
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        host.SeedFile(
            "/inventory.tsv",
            "sku\tname\tqty\nb002\t  gizmo  \t0\na001\tWidget\t3\nc003\tSprocket \t12\n");

        var result = script.Run(host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("sku\tname\tqty\na001\tWidget\t3\nc003\tSprocket\t12", host.ReadText("/available.tsv"));
        Assert.Equal(
            "[{\"sku\": \"a001\", \"name\": \"Widget\", \"qty\": 3}, {\"sku\": \"c003\", \"name\": \"Sprocket\", \"qty\": 12}]",
            host.ReadText("/available.json"));
    }
}