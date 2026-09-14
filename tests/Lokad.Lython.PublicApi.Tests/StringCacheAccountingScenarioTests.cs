using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG06: the rune-offset table pays for its footprint instead of escaping
/// beside the charged UTF-8 storage, while full decodes stay transient so
/// retained strings never pin UTF-16 copies behind the budget.
/// </summary>
public sealed class StringCacheAccountingScenarioTests
{
    [Fact]
    public async Task RuneIndexCacheIsCharged()
    {
        // MG06 probe: indexing a 400 KiB string builds an 800 KiB rune-offset
        // cache, far above a 600 KiB budget.
        var script = new LythonEngine().Compile(
            """
            s = "é" * 200000
            return s[100]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 614400 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RetainedDecodedStringsFitBudget()
    {
        // Decoding no longer retains anything: 1,500 parsed strings fit the
        // same budget their storage alone already fit.
        var script = new LythonEngine().Compile(
            "objs = ['1' * 1000 for i in range(1500)]\nfor obj in objs:\n    float(obj)\nreturn len(objs)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1500);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RepeatedFloatDecodesStayCorrect()
    {
        var script = new LythonEngine().Compile(
            "s = '3.14159'\ni = 0\nwhile i < 2000:\n    x = float(s)\n    i = i + 1\nreturn x\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(3.14159, Assert.IsType<double>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(3.14159, Assert.IsType<double>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task DroppedDecodedStringsFit()
    {
        var script = new LythonEngine().Compile(
            "i = 0\nwhile i < 5000:\n    x = float('1' * 100)\n    i = i + 1\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
