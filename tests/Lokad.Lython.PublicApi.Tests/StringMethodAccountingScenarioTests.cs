using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG03/MG06: string methods adopt fresh results derived from unowned
/// (shared-constant) receivers, so retained derivations accumulate instead of
/// riding the source budget-free.
/// </summary>
public sealed class StringMethodAccountingScenarioTests
{
    // 20k retained stripped fields own ~130B plus a 16B list slot each, so
    // they fit 1.5MB pre-fix and trip post-fix.
    private const long StringMethodBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedStripsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append("  ab  ".strip())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StringMethodBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= StringMethodBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= StringMethodBudgetBytes);
    }

    [Fact]
    public async Task StringMethodsBehave()
    {
        var script = new LythonEngine().Compile("""
            s = "  Hello World  "
            return [s.strip(), s.lower(), s.upper(), "a-b".replace("-", "+"), "x".center(5), "Hi {}".format("there"), "unprefixed".removeprefix("un"), "abc".removeprefix("z")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "Hello World", "  hello world  ", "  HELLO WORLD  ", "a+b", "  x  ", "Hi there", "prefixed", "abc" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}