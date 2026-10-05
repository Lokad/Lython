using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SyntaxCorpusTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SyntaxCorpus", "cases.json");
        var cases = JsonSerializer.Deserialize<CorpusCase[]>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
        foreach (var item in cases)
        {
            yield return [item.Name, item];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task SyntaxContract(string name, CorpusCase item)
    {
        Assert.Equal(name, item.Name);
        var rejected = item.Status is "invalid" or "unsupported";
        // A syntax failure anywhere must prevent even preceding effects.
        var source = rejected ? "print('must not execute')\n" + item.Source : item.Source;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid != rejected,
            name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        if (rejected)
        {
            Assert.Contains(compiled.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error && d.Span is not null);
            if (item.Status == "unsupported")
                Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("Unsupported", StringComparison.Ordinal));
        }

        // Reuse the same compiled program across modes and fresh executions.
        foreach (var asynchronous in new[] { false, true, false, true })
        {
            var host = new MockLythonHost();
            var result = asynchronous ? await compiled.RunAsync(host) : compiled.Run(host);
            if (rejected)
            {
                Assert.IsType<LythonExecutionResult.CompilationFailedState>(result.State);
                Assert.Equal("", result.StandardOutput);
            }
            else if (item.Status == "extension")
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("1", result.ReturnValue?.ToString());
            }
            else
            {
                Assert.True(result.Success == item.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(item.StandardOutput, result.StandardOutput);
                Assert.Equal(item.ExceptionType, result.Failure?.ExceptionType);
            }
        }
    }

    public sealed record CorpusCase(
        string Name, string Family, string Status, string Source,
        string StandardOutput, bool Success, string? ExceptionType, string Note);
}
