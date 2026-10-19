using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerFromBytesAccountingTests : LifetimeScenario
{
    [Theory]
    [InlineData("little", false, false)]
    [InlineData("little", false, true)]
    [InlineData("big", true, false)]
    [InlineData("big", true, true)]
    public async Task RetainedMagnitudesCannotEscapeTheBudget(string order, bool signed, bool asyncMode)
    {
        var code = "raw = b'\\xfe' * 512\nxs = [int.from_bytes(raw, '" + order + "', signed=" + (signed ? "True" : "False") + ") for _ in range(2000)]\nreturn len(xs)\n";
        var script = new LythonEngine().Compile(code);
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
    public async Task MagnitudeBatchesReturnSharedAccounting(bool asyncMode)
    {
        await AssertBatchCompletes("raw = b'\\xfe' * 512\nfor _ in range(8):\n int.from_bytes(raw, 'big', signed=True)\nreturn 8\n", new BigInteger(8), 262144, asynchronous: asyncMode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedMagnitudeAndAliasesSurviveInputRelease(bool asyncMode)
    {
        const string code = """
            raw = b'\xfe' * 2048
            value = int.from_bytes(raw, 'little')
            raw = None
            aliases = [[value, value, value] for _ in range(8)]
            for _ in range(8):
                int.from_bytes(b'\xfe' * 512, 'big', signed=True)
            return [value.bit_length(), aliases[-1][0] is value, len(aliases)]
            """;
        await AssertBatchCompletes(code, new List<object?> { new BigInteger(16384), true, new BigInteger(8) }, 1048576, asynchronous: asyncMode);
    }
}
