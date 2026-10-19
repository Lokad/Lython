using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetNumericStoragePolicyTests : LifetimeScenario
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearAndRefillKeepsItsStreamingBudget(bool asyncMode)
    {
        const string code = """
            values = set()
            for batch in range(100):
                values.update(range(1000))
                values.remove(True)
                values.remove(False)
                assert len(values) == 998
                values.clear()
            return len(values)
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("0", result.ReturnValue?.ToString());
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedLargeMembersDenyAtTheSameCap(bool asyncMode)
    {
        var script = new LythonEngine().Compile("values = set()\nfor i in range(2000):\n values.add((1 << 4000) + i)\nreturn len(values)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeMemberBatchesReturnSharedAccounting(bool asyncMode)
    {
        await AssertBatchCompletes("for i in range(8):\n values = {(1 << 4000) + i}\nreturn values.pop().bit_length()\n", "4001", 262144, asynchronous: asyncMode);
    }

    [Fact]
    public async Task RetainedSetMemberSurvivesGenuineHostSuspension()
    {
        const string code = """
            x = 1 << 4000
            values = {x}
            del x
            with open('/value', 'r') as stream:
                stream.read()
            for _ in range(8):
                z = 1 << 4000
            return values.pop().bit_length()
            """;
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "ready");
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(code, budget);
        var baseline = budget.ReservedBytes;
        Assert.True(script.IsValid);
        using var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4001", result.ReturnValue?.ToString());
        Assert.True(host.CompletedAsynchronously > 0);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
