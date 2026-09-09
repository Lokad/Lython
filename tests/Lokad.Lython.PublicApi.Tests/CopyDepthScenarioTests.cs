using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: deepcopy tracks graph depth so pathologically deep values fail with
/// RecursionError instead of exhausting the host stack. Cycles still resolve
/// through the memo.
/// </summary>
public sealed class CopyDepthScenarioTests
{
    [Fact]
    public async Task DeepCopyAtLimitSucceeds()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            v = 1
            i = 0
            while i < 512:
                v = [v]
                i = i + 1
            m = copy.deepcopy(v)
            cur = m
            i = 0
            while i < 512:
                cur = cur[0]
                i = i + 1
            return cur
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DeepCopyPastLimitFailsRecursion()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            v = 1
            i = 0
            while i < 600:
                v = [v]
                i = i + 1
            m = copy.deepcopy(v)
            return 0
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RecursionError", sync.Failure?.ExceptionType);
        Assert.Contains("recursion depth", sync.Failure?.Message, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("RecursionError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("recursion depth", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeepCopyCyclePreservesIdentity()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            l = []
            l.append(l)
            m = copy.deepcopy(l)
            return m[0] is m
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(true, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }
}