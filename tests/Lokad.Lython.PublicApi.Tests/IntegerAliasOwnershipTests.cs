using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerAliasOwnershipTests
{
    private const string Prelude = """
        import math, operator
        class AsInt:
            def __int__(self): return x
        class AsIndex:
            def __index__(self): return x
        class AsTrunc:
            def __trunc__(self): return x

        """;

    public static IEnumerable<object[]> AliasCases()
    {
        foreach (var expression in new[]
        {
            "int(x)", "x.__int__()", "x.__index__()", "x.__trunc__()", "x.__floor__()", "x.__ceil__()",
            "round(x)", "round(x, 2)", "round(x, None)", "x.__round__()", "x.__round__(2)",
            "abs(x)", "operator.abs(x)", "math.floor(x)", "math.ceil(x)", "math.trunc(x)",
            "operator.index(x)", "+x", "operator.pos(x)", "int(AsInt())", "int(AsIndex())",
            "int(AsTrunc())", "operator.index(AsIndex())",
        })
        {
            yield return [expression, false];
            yield return [expression, true];
        }
    }

    [Theory]
    [MemberData(nameof(AliasCases))]
    public async Task ExactIntegerAliasesPreservePythonIdentity(string expression, bool asyncMode)
    {
        var script = new LythonEngine().Compile(Prelude + "x = 1 << 4000\nreturn (" + expression + ") is x\n");
        Assert.True(script.IsValid);
        var result = asyncMode ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(Assert.IsType<bool>(result.ReturnValue));
    }

    public static IEnumerable<object[]> RetentionCases()
    {
        foreach (var expression in new[]
        {
            "int(x)", "x.__int__()", "x.__index__()", "x.__trunc__()", "x.__floor__()", "x.__ceil__()",
            "math.floor(x)", "math.ceil(x)", "math.trunc(x)", "operator.index(x)",
            "operator.index(AsIndex())", "round(x)", "abs(x)", "int(AsInt())",
        })
        {
            yield return [expression, false];
            yield return [expression, true];
        }
    }

    [Theory]
    [MemberData(nameof(RetentionCases))]
    public async Task RetainedAliasesKeepTheirPayloadFunded(string expression, bool asyncMode)
    {
        var script = new LythonEngine().Compile(Prelude + "xs = []\nfor _ in range(2000):\n x = 1 << 4000\n xs.append(" + expression + ")\nreturn len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }
}
