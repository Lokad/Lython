using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG09: retained compiled patterns own a conservative compilation allowance,
/// so many compilations cannot bypass the execution memory budget.
/// </summary>
public sealed class RegexCompilationAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedCompilationsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import re
            patterns = []
            i = 0
            while i < 20:
                patterns.append(re.compile("a(b|c)*d"))
                i = i + 1
            return len(patterns)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CompiledPatternSearchStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            import re
            p = re.compile("a(b|c)*d")
            m = p.search("xxabd")
            return [m.group(0), m.group(1)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "abd", "b" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}