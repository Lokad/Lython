using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG09: the compilation allowance scales with capture slots and pattern size,
/// not just pattern count. Twenty retained 50-group patterns share one hoisted
/// pattern string, isolating per-compilation growth from literal costs.
/// </summary>
public sealed class RegexGroupAccountingScenarioTests
{
    private const string BuildManyGroupPatterns =
        """
        import re
        pat = "(a)" * 50
        ps = []
        i = 0
        while i < 20:
            ps.append(re.compile(pat))
            i = i + 1
        return len(ps)
        """;

    [Fact]
    public async Task ManyGroupPatternsStayCharged()
    {
        var script = new LythonEngine().Compile(BuildManyGroupPatterns);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is ~1.32MB (20 x 64KiB), so this fits; the
        // slot/length backstop (187904 per pattern) pushes post-fix past 3.7MB.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2000000 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyManyGroupPatternsSucceed()
    {
        var script = new LythonEngine().Compile(BuildManyGroupPatterns);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };
        var expected = new BigInteger(20);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ManyGroupSearchStillMatches()
    {
        var script = new LythonEngine().Compile(
            """
            import re
            pat = "(a)" * 50
            p = re.compile(pat)
            m = p.search("a" * 60)
            return [p.groups, len(m.groups()), m.groups()[0]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(50), new BigInteger(50), "a" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
