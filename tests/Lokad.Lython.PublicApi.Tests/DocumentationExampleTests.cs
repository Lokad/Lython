using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// The file-processing recipe is executable documentation:
// this test extracts it verbatim, runs it against a seeded host, and checks
// both output files, so the documented example can never silently rot.
public sealed class DocumentationExampleTests
{
    private static string LoadRepresentativeScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "EXAMPLES.md");
            if (File.Exists(candidate))
            {
                var text = File.ReadAllText(candidate);
                var section = text.IndexOf("## Inventory report", StringComparison.Ordinal);
                Assert.True(section >= 0, "docs/EXAMPLES.md lost its inventory example.");
                var fence = text.IndexOf("```python", section, StringComparison.Ordinal);
                Assert.True(fence >= 0, "docs/EXAMPLES.md lost its Python fence.");
                var start = fence + "```python".Length;
                var end = text.IndexOf("```", start, StringComparison.Ordinal);
                Assert.True(end > start, "docs/EXAMPLES.md Python fence is not closed.");
                return text.Substring(start, end - start).Trim() + "\n";
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("docs/EXAMPLES.md was not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void RepresentativeScriptProducesBothOutputs()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(LoadRepresentativeScript());
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        host.SeedFile(
            "/inventory.tsv",
            "sku\tname\tqty\nb002\t  gizmo  \t0\na001\tWidget\t3\nc003\tSprocket \t12\n");

        using var result = script.Run(host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("sku\tname\tqty\na001\tWidget\t3\nc003\tSprocket\t12", host.ReadText("/available.tsv"));
        Assert.Equal(
            "[{\"sku\": \"a001\", \"name\": \"Widget\", \"qty\": 3}, {\"sku\": \"c003\", \"name\": \"Sprocket\", \"qty\": 12}]",
            host.ReadText("/available.json"));
    }
}
