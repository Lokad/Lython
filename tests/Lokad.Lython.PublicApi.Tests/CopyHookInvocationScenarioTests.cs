using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R45: CPython does not memoize self-copies (if y is not x), so a hook that
/// returns its own object runs again per occurrence; fresh results still
/// memoize and alias.
/// </summary>
public sealed class CopyHookInvocationScenarioTests
{
    [Fact]
    public async Task SelfReturningHookRunsPerOccurrence()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            calls = []
            class S:
                def __deepcopy__(self, memo):
                    calls.append(1)
                    return self
            a = S()
            b = copy.deepcopy([a, a])
            return [b[0] is a, b[0] is b[1], len(calls)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FreshReturningHookRunsOnceAndAliases()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            calls = []
            class F:
                def __deepcopy__(self, memo):
                    calls.append(1)
                    return [1]
            a = F()
            b = copy.deepcopy([a, a])
            return [b[0] is b[1], b[0] == [1], len(calls)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
