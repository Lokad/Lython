using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03/MG04: dropped slice temporaries release through the reclamation pool
// once collected; without tracking, every slice owned its construction charge
// forever and bounded call-free loops could never complete.
public sealed class SliceLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;

    [Fact]
    public async Task BoundedStringSlicesComplete()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\n"
            + "x = ''\n"
            + "for i in range(10000):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(999);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundedListSlicesComplete()
    {
        var script = new LythonEngine().Compile(
            "b = list(range(1000))\n"
            + "x = []\n"
            + "for i in range(10000):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(999);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundedTupleSlicesComplete()
    {
        var script = new LythonEngine().Compile(
            "b = tuple(range(1000))\n"
            + "x = ()\n"
            + "for i in range(10000):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(999);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedSlicesDenyAndAllowReuse()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\n"
            + "ok = False\n"
            + "objs = []\n"
            + "i = 0\n"
            + "while i < 100000:\n"
            + "    try:\n"
            + "        objs.append(b[1:])\n"
            + "    except MemoryError:\n"
            + "        ok = True\n"
            + "        break\n"
            + "    i = i + 1\n"
            + "return [ok, len(objs) > 0, len(objs[-1])]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { true, true, new BigInteger(999) };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SliceAliasesStayUsable()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\n"
            + "s = b[1:]\n"
            + "t = s\n"
            + "return [len(s), len(t)]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(999), new BigInteger(999) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}