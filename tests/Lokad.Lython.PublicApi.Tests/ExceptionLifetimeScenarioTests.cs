using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: constructed exception values own their args tuple and rendered message
// through the pool. Dropped constructions (including every caught raise)
// reclaim on sweep instead of stranding 32+16n B plus 128+len B each, which
// also greens the 100k-discarded user-iterable loop whose per-advance
// StopIteration travels through a user frame.
public sealed class ExceptionLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted(long budget) => new() { MaxExecutionMemoryBytes = budget };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task ConstructDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = ValueError(1)\nreturn 0\n", "0");

    [Fact]
    public async Task BareConstructDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = ValueError()\nreturn 0\n", "0");

    [Fact]
    public async Task KeyErrorConstructDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = KeyError(\"k\")\nreturn 0\n", "0");

    [Fact]
    public async Task SharedArgConstructDiscardCompletes()
        => await AssertCompletes(
            "shared_str = \"shared message\"\nfor i in range(100000):\n    e = ValueError(shared_str)\nreturn 0\n", "0");

    [Fact]
    public async Task SharedMultiArgConstructDiscardCompletes()
        => await AssertCompletes(
            "shared_str = \"shared message\"\nfor i in range(100000):\n    e = ValueError(shared_str, shared_str)\nreturn 0\n", "0");

    [Fact]
    public async Task CaughtRaiseDiscardCompletes()
        => await AssertCompletes(
            "def f():\n    raise ValueError(1)\nfor i in range(100000):\n    try:\n        f()\n    except ValueError:\n        pass\nreturn 0\n", "0");

    [Fact]
    public async Task GuestCaughtStopIterationCompletes()
        => await AssertCompletes(
            "class R:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        raise StopIteration()\nr = R()\nfor i in range(100000):\n    try:\n        x = next(r)\n    except StopIteration:\n        pass\nreturn 0\n", "0");

    [Fact]
    public async Task UserIterableDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        raise StopIteration()\nfor i in range(100000):\n    for x in R():\n        pass\nreturn 0\n", "0");

    [Fact]
    public async Task BoundExceptionBehaves()
        => await AssertCompletes(
            "try:\n    raise ValueError(3)\nexcept ValueError as e:\n    y = e.args[0]\nreturn y\n", "3");

    [Fact]
    public async Task RetainedConstructionsDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(ValueError())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }

    [Fact]
    public async Task ArgsReassignDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    e = ValueError(1)\n    e.args = (1, 2)\nreturn 0\n", "0");

    [Fact]
    public async Task EmptyArgsReassignDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    e = ValueError(1)\n    e.args = ()\nreturn 0\n", "0");

    [Fact]
    public async Task CustomAttrDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    e = ValueError(1)\n    e.custom = 2\nreturn 0\n", "0");

    [Fact]
    public async Task CustomAttrBehaves()
    {
        var script = new LythonEngine().Compile(
            "e = ValueError(1)\ne.custom = 2\nreturn [e.custom, str(e)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new System.Numerics.BigInteger(2), "1" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task RetainedCustomAttrDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    e = ValueError(1, 2)\n    e.extra = i\n    objs.append(e)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
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
