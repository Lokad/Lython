using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ProvedStringAdmissionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task ReplacementAfterCaughtDenialKeepsRetainedAliasesFunded(bool asynchronous, bool list, bool retain)
    {
        var script = new LythonEngine().Compile($$"""
held = ['x' * 16000] if {{(list ? "True" : "False")}} else 'x' * 16000
kept = held if {{(retain ? "True" : "False")}} else None
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
if {{(list ? "True" : "False")}}:
    if {{(retain ? "True" : "False")}}:
        held = None
    else:
        del held[0]
else:
    held = None
candidate = 'z' * 16000
return len(candidate)
""");
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 28000 };
        var host = new MockLythonHost();
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.InRange(result.PeakExecutionMemoryBytes, 16000, 28000);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.Failure?.Attribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(16000), result.ReturnValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StringLimitPrecedesMemoryProofAndEmptyResultsStayFree(bool asynchronous)
    {
        var script = new LythonEngine().Compile("""
held = 'é' * 16000
assert '' * 20000000 == ''
assert 'x' * 0 == ''
try:
    candidate = 'y' * 21000
except RuntimeError as error:
    return str(error)
""");
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 48000, MaxStringLength = 20000 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Contains("string length", Assert.IsType<string>(result.ReturnValue), StringComparison.OrdinalIgnoreCase);
        Assert.InRange(result.PeakExecutionMemoryBytes, 32000, 48000);
    }
}
