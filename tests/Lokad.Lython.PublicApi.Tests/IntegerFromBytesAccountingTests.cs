using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerFromBytesAccountingTests
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedMagnitudesKeepAStreamingBudget(bool asyncMode)
    {
        const string code = """
            raw = b'\xfe' * 512
            for _ in range(4000):
                int.from_bytes(raw, 'big', signed=True)
            return 4000
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(4000), result.ReturnValue);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }
}
