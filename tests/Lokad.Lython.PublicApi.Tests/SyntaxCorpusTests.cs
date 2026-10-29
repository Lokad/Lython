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
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid != rejected,
            name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        if (rejected)
        {
            Assert.Contains(compiled.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error && d.Span is not null);
            if (item.Status == "unsupported")
                Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("Unsupported", StringComparison.Ordinal));
        }

        foreach (var asynchronous in new[] { false, true })
            await AssertRunAsync(compiled, item, asynchronous);
    }

    [Theory]
    [InlineData("definition-default-once")]
    [InlineData("closure-nonlocal")]
    [InlineData("generator-closure-change")]
    [InlineData("generator-finally")]
    [InlineData("class-static-class-method")]
    [InlineData("matmul")]
    public async Task CompiledProgramReusePreservesIndependentExecutionState(string name)
    {
        var item = Cases().Select(row => (CorpusCase)row[1]).Single(item => item.Name == name);
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(item.Source);
        Assert.True(compiled.IsValid);
        foreach (var asynchronous in new[] { false, true, false, true })
            await AssertRunAsync(compiled, item, asynchronous);
    }

    private static async Task AssertRunAsync(LythonCompiledScript compiled, CorpusCase item, bool asynchronous)
    {
        var rejected = item.Status is "invalid" or "unsupported";
        var host = new MockLythonHost();
        using var result = asynchronous ? await compiled.RunAsync(host) : compiled.Run(host);
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
            Assert.True(result.Success == item.Success, item.Name + ": " + result.Failure?.Message);
            Assert.Equal(item.StandardOutput, result.StandardOutput);
            Assert.Equal(item.ExceptionType, result.Failure?.ExceptionType);
        }
    }

    public sealed record CorpusCase(
        string Name, string Family, string Status, string Source,
        string StandardOutput, bool Success, string? ExceptionType, string Note);
}
