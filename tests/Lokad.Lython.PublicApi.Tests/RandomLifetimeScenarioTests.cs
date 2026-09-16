using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: random generator shells own lifetime like the other factory results do.
public sealed class RandomLifetimeScenarioTests
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
    public async Task RandomCtorDiscardCompletes()
        => await AssertCompletes(
            "import random\nfor i in range(50000):\n    r = random.Random()\nreturn 0\n", "0");

    [Fact]
    public async Task SeededRandomCtorDiscardCompletes()
        => await AssertCompletes(
            "import random\nfor i in range(50000):\n    r = random.Random(123)\nreturn 0\n", "0");

    [Fact]
    public async Task SeededRandomBehaves()
        => await AssertCompletes(
            "import random\nr1 = random.Random(123)\nr2 = random.Random(123)\nreturn str(r1.random() == r2.random()) + str(r1.randint(0, 100) == r2.randint(0, 100))\n", "TrueTrue");

    [Fact]
    public async Task RetainedRandomDenied()
    {
        var script = new LythonEngine().Compile(
            "import random\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(random.Random())\n    i = i + 1\nreturn len(objs)\n");
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
