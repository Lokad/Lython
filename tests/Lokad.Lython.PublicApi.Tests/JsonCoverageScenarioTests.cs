using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG16: small-budget drills for many tiny values, large escaped strings and
/// nested objects, complementing the recursion cases.
/// </summary>
public sealed class JsonCoverageScenarioTests
{
    [Fact]
    public async Task ManyTinyValuesStayBounded()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return len(json.dumps([{}] * 1000))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task LargeEscapedStringStaysBounded()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return len(json.dumps(["\u0001" * 200000]))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1200004), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1200004), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NestedObjectsRespectDepth()
    {
        var ok = new LythonEngine().Compile(
            """
            import json
            v = 1
            i = 0
            while i < 300:
                v = [v]
                i = i + 1
            return len(json.dumps(v))
            """);
        Assert.True(ok.IsValid);
        var syncOk = ok.Run(new MockLythonHost());
        Assert.True(syncOk.Success, syncOk.Failure?.Message);

        var deep = new LythonEngine().Compile(
            """
            import json
            v = 1
            i = 0
            while i < 600:
                v = [v]
                i = i + 1
            return json.dumps(v)
            """);
        Assert.True(deep.IsValid);
        var syncDeep = deep.Run(new MockLythonHost());
        Assert.False(syncDeep.Success);
        Assert.Equal("RecursionError", syncDeep.Failure?.ExceptionType);

        var asyncDeep = await deep.RunAsync(new MockLythonHost());
        Assert.False(asyncDeep.Success);
        Assert.Equal("RecursionError", asyncDeep.Failure?.ExceptionType);
    }
}
