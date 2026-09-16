using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: Counter arithmetic results own lifetime like the other operator factories do.
public sealed class CounterArithmeticLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task CounterAddDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nfor i in range(50000):\n    x = a + b\nreturn 0\n", "0");

    [Fact]
    public async Task CounterSubDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nfor i in range(50000):\n    x = a - b\nreturn 0\n", "0");

    [Fact]
    public async Task CounterOrDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nfor i in range(50000):\n    x = a | b\nreturn 0\n", "0");

    [Fact]
    public async Task CounterAndDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nfor i in range(50000):\n    x = a & b\nreturn 0\n", "0");

    [Fact]
    public async Task CounterUnaryDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2, b=-1)\nfor i in range(50000):\n    x = +a\n    y = -a\nreturn 0\n", "0");

    [Fact]
    public async Task CounterMixedOrDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\nc = Counter(a=1)\nfor i in range(50000):\n    x = c | {'b': 2}\n    y = {'b': 2} | c\nreturn 0\n", "0");

    [Fact]
    public async Task CounterArithmeticBehaves()
        => await AssertCompletes(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nreturn str(sorted((a+b).items())) + str(sorted((a-b).items())) + str(sorted((a|b).items())) + str(sorted((a&b).items()))\n", "[('a', 3), ('b', 1)][('a', 1)][('a', 2), ('b', 1)][('a', 1)]");

    [Fact]
    public async Task RetainedCounterArithmeticDenied()
    {
        var script = new LythonEngine().Compile(
            "from collections import Counter\na = Counter(a=2)\nb = Counter(a=1, b=1)\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(a + b)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
