using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComposedExpressionSyntaxTests
{
    [Fact]
    public async Task RepeatedFiltersComposeWithNestedLoopsAndLazyEvaluation()
    {
        var compiled = new LythonEngine().Compile("""
            events = []
            def mark(x):
                events.append(x)
                return True
            print([x for x in range(6) if x > 1 if x < 4])
            print(sorted({x for x in range(6) if x > 1 if x < 4}))
            print({x: x + 1 for x in range(6) if x > 1 if x < 4})
            g = (x * y for x in range(3) if x > 0 if mark(x) for y in range(3) if y > 0 if y < 2)
            print(events)
            print(list(g), events)
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[2, 3]\n[2, 3]\n{2: 3, 3: 4}\n[]\n[1, 2] [1, 2]\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task StarredArgumentsAfterKeywordsEvaluateBeforeThem()
    {
        var compiled = new LythonEngine().Compile("""
            events = []
            def mark(value):
                events.append(value)
                return value
            def f(a, b, c):
                return (a, b, c)
            print(f(b=mark(2), *mark([1]), c=mark(3)), events)
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(1, 2, 3) [[1], 2, 3]\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("f(a=1, 2)")]
    [InlineData("f(**{}, *[1])")]
    public void IllegalArgumentOrderingStillFails(string source)
        => Assert.False(new LythonEngine().Compile(source).IsValid);
}
