using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N03 (statistics/difflib part): raw drains stream with memory/count/work
// checks via N09 lease/governed acquisition. Argparse live choices covered
// separately; here mode frequency and diff sequence acquisition.
public sealed class RawDrainClosureTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    private static void AssertDenied(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ModeHugeInput_DeniesUnderTinyLimits()
    {
        const string code = "import statistics, itertools\nreturn statistics.mode(itertools.repeat(7, 200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
        AssertDenied(await script.RunAsync(new MockLythonHost(), Tiny()));
    }

    [Fact]
    public async Task ModeFunded_SucceedsBothModes()
    {
        const string code = "import statistics\nreturn [statistics.mode([1, 1, 2]), statistics.multimode([1, 1, 2, 2])]\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ModeInfiniteInput_DeniesBounded()
    {
        const string code = "import statistics, itertools\nreturn statistics.mode(itertools.count())\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
    }

    [Fact]
    public void ModeInvalidElements_ThrowTypeError()
    {
        const string code = "import statistics\nreturn statistics.mode([1, [2]])\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UnifiedDiffHuge_DeniesUnderTinyLimits()
    {
        const string code = "import difflib\nreturn len(list(difflib.unified_diff([str(i) for i in range(200000)], [])))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
        AssertDenied(await script.RunAsync(new MockLythonHost(), Tiny()));
    }

    [Fact]
    public async Task UnifiedDiffFunded_SucceedsBothModes()
    {
        const string code = "import difflib\nreturn len(list(difflib.unified_diff(['a', 'b'], ['a', 'c'])))\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public void UnifiedDiffInvalidElements_Rejects()
    {
        const string code = "import difflib\nreturn list(difflib.unified_diff([1, 2], ['a']))\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.False(result.Success);
    }

    [Fact]
    public void DiffBytesAlias_SucceedsFunded()
    {
        const string code = "import difflib\nreturn list(difflib.diff_bytes(difflib.unified_diff, [b\"alpha\\n\"], [b\"beta\\n\"]))\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
    }
}
