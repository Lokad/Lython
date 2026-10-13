using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03/MG04: dropped slice temporaries release through the reclamation pool
// once collected; without tracking, every slice owned its construction charge
// forever and bounded call-free loops could never complete.
public class SliceLifetimeScenarioTests : LifetimeScenario
{
    protected virtual int SliceLength => 64;

    private long StreamingBudget => Budget(3145728);

    [Fact]
    public async Task BoundedStringSlicesComplete()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * " + SliceLength + "\n"
            + "x = ''\n"
            + "for i in range(" + Iterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(SliceLength - 1);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget };
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
            "b = list(range(" + SliceLength + "))\n"
            + "x = []\n"
            + "for i in range(" + Iterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(SliceLength - 1);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget };
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
            "b = tuple(range(" + SliceLength + "))\n"
            + "x = ()\n"
            + "for i in range(" + Iterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(SliceLength - 1);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedSlicesDenyThenReleasedStorageAllowsReuse()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * " + SliceLength + "\n"
            // Fund the result before retention exhausts the budget. Catching
            // MemoryError does not create space for a new result container.
            + "result = [False, False, " + (SliceLength - 1) + "]\n"
            + "objs = []\n"
            + "i = 0\n"
            + "while i < " + Iterations(100000) + ":\n"
            + "    try:\n"
            + "        objs.append(b[1:])\n"
            + "    except MemoryError:\n"
            + "        result[0] = True\n"
            + "        break\n"
            + "    i = i + 1\n"
            + "result[1] = len(objs) > 0\n"
            + "kept = objs[-1]\n"
            + "assert len(kept) == result[2]\n"
            + "objs.clear()\n"
            // Reuse the same budget after a genuine retained-value denial,
            // while an escaped slice remains alive throughout the stream.
            + "for i in range(" + Iterations(10000) + "):\n"
            + "    fresh = b[1:]\n"
            + "assert len(fresh) == result[2]\n"
            + "assert len(kept) == result[2]\n"
            + "return result\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { true, true, new BigInteger(SliceLength - 1) };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget };
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
            "b = 'a' * " + SliceLength + "\n"
            + "s = b[1:]\n"
            + "t = s\n"
            + "return [len(s), len(t)]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(SliceLength - 1), new BigInteger(SliceLength - 1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
