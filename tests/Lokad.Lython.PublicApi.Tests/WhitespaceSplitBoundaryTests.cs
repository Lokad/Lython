using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WhitespaceSplitBoundaryTests
{
    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public async Task LongMixedWordsAndWhitespaceKeepExactCountedRemainders(int size)
    {
        using var script = new LythonEngine().Compile($$"""
            left = 'a' * {{size}} + '\u03b1\0\x7f\U0001f600'
            space = '\u2003' * {{size}}
            text = ' \t' + left + space + 'tail \t'
            assert text.split() == [left, 'tail']
            assert text.split(None, 0) == [left + space + 'tail \t']
            assert text.split(None, 1) == [left, 'tail \t']
            assert text.split(None, 2) == [left, 'tail']
            assert text.split(None, 2147483647) == [left, 'tail']
            assert (' \t' * {{size}}).split() == []
            print('ok')
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
