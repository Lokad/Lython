using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ScalarStringLiteralDiagnosticsTests
{
    [Fact]
    public async Task SurrogateLiteralsFailWithControlledScalarDiagnostics()
    {
        var sources = new[]
        {
            "print('\\ud800')", "print('\\udfff')", "print('\\U0000D800')",
            "print('\\ud83d\\ude00')", "print(f'\\ud800')", "print(f'{1:\\ud800}')",
            "print('" + (char)0xD800 + "')", "print(r'" + (char)0xDC00 + "')",
            "print(rf'" + (char)0xD800 + "')"
        };
        foreach (var source in sources)
        {
            var budget = new LythonMemoryBudget(128 * 1024);
            using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
            {
                Assert.False(script.IsValid);
                Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA2000" && diagnostic.Message.Contains("surrogate", StringComparison.Ordinal));
                using var sync = script.Run(new MockLythonHost());
                using var asynchronous = await script.RunAsync(new MockLythonHost());
                Assert.False(sync.Success);
                Assert.False(asynchronous.Success);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("print('\\UFFFFFFFF')")]
    [InlineData("print('\\U80000000')")]
    public void OutOfRangeEscapesAreRejectedWithoutSignedWraparound(string source)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA1007");
    }
}
