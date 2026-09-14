using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03/MG04/MG06: dropped factory, display, method and operator results
// release through the reclamation pool once collected; without tracking,
// every temporary owned its construction charge forever and bounded loops
// could never complete. Call results ride the shared callable funnel;
// displays and operators track at their (shared, per-engine) build points.
public sealed class FactoryLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static async Task AssertCompletes(LythonCompiledScript script, LythonRunOptions options, object? expected)
    {
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    private static LythonRunOptions Bounded() => new() { MaxExecutionMemoryBytes = ThreeMib };

    [Fact]
    public async Task DroppedListCallsRelease()
        => await AssertCompletes(Compile("x = None\nfor i in range(100000):\n    x = list()\nreturn 0\n"), Bounded(), new BigInteger(0));

    [Fact]
    public async Task DroppedListDisplaysRelease()
        => await AssertCompletes(Compile("x = 0\nfor i in range(100000):\n    x = [1, 2]\nreturn x\n"), Bounded(), new List<object?> { new BigInteger(1), new BigInteger(2) });

    [Fact]
    public async Task DroppedDictCallsRelease()
        => await AssertCompletes(Compile("x = None\nfor i in range(100000):\n    x = dict()\nreturn 0\n"), Bounded(), new BigInteger(0));

    [Fact]
    public async Task DroppedDictDisplaysRelease()
        => await AssertCompletes(Compile("x = 0\nfor i in range(100000):\n    x = {'a': 1}\nreturn len(x)\n"), Bounded(), new BigInteger(1));

    [Fact]
    public async Task DroppedTupleDisplaysRelease()
        => await AssertCompletes(Compile("x = ()\nfor i in range(100000):\n    x = (1, 2)\nreturn len(x)\n"), Bounded(), new BigInteger(2));

    [Fact]
    public async Task DroppedStringMethodsRelease()
        => await AssertCompletes(Compile("b = 'a' * 1000\nx = ''\nfor i in range(10000):\n    x = b.upper()\nreturn len(x)\n"), Bounded(), new BigInteger(1000));

    [Fact]
    public async Task DroppedStrippedStringsRelease()
        => await AssertCompletes(Compile("b = ' x' * 500\nx = ''\nfor i in range(10000):\n    x = b.strip()\nreturn len(x)\n"), Bounded(), new BigInteger(999));

    [Fact]
    public async Task DroppedListConcatRelease()
        => await AssertCompletes(Compile("x = 0\nfor i in range(100000):\n    x = [1, 2] + [3, 4]\nreturn len(x)\n"), Bounded(), new BigInteger(4));

    [Fact]
    public async Task DroppedListRepeatRelease()
        => await AssertCompletes(Compile("x = 0\nfor i in range(100000):\n    x = [1, 2] * 2\nreturn len(x)\n"), Bounded(), new BigInteger(4));

    [Fact]
    public async Task DroppedTupleConcatRelease()
        => await AssertCompletes(Compile("x = ()\nfor i in range(100000):\n    x = (1, 2) + (3, 4)\nreturn len(x)\n"), Bounded(), new BigInteger(4));

    [Fact]
    public async Task DroppedDictUnionRelease()
        => await AssertCompletes(Compile("a = {1: 2}\nb = {3: 4}\nx = 0\nfor i in range(100000):\n    x = a | b\nreturn len(x)\n"), Bounded(), new BigInteger(2));

    [Fact]
    public async Task DroppedSetOperationsRelease()
        => await AssertCompletes(Compile("a = {1, 2}\nb = {2, 3}\nx = set()\nfor i in range(100000):\n    x = a | b\n    x = a & b\n    x = a - b\nreturn len(x)\n"), Bounded(), new BigInteger(1));

    [Fact]
    public async Task DroppedSetCopyRelease()
        => await AssertCompletes(Compile("import copy\ns = {1, 2, 3}\nx = None\nfor i in range(100000):\n    x = copy.copy(s)\nreturn len(x)\n"), Bounded(), new BigInteger(3));

    [Fact]
    public async Task RetainedListDisplaysDenyAndAllowReuse()
    {
        var script = Compile("ok = False\nobjs = []\nfor i in range(100000):\n    try:\n        objs.append([1, 2])\n    except MemoryError:\n        ok = True\n        break\nreturn ok\n");
        await AssertCompletes(script, Bounded(), true);
    }

    [Fact]
    public async Task ClearedDictsReleaseExactly()
    {
        var script = Compile("x = {}\nfor i in range(100000):\n    x = {'a': 1, 'b': 2}\n    x.clear()\nreturn len(x)\n");
        await AssertCompletes(script, Bounded(), new BigInteger(0));
    }

    [Fact]
    public async Task ListAliasesStayUsable()
    {
        var script = Compile("a = [1, 2]\nb = a\nb.append(3)\nreturn [len(a), len(b)]\n");
        var expected = new List<object?> { new BigInteger(3), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedTuplesDeny()
    {
        var script = Compile("objs = []\nfor i in range(10000):\n    objs.append(tuple(range(100)))\nreturn len(objs)\n");
        var options = Bounded();
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ThreeMib);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task RetainedStringMethodsDeny()
    {
        var script = Compile("b = 'a' * 1000\nobjs = []\nfor i in range(10000):\n    objs.append(b.upper())\nreturn len(objs)\n");
        var options = Bounded();
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ThreeMib);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ThreeMib);
    }
}