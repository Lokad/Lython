using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringOwnershipAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuestDeletionKeepsConservativeBatchFundingUntilCleanup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
held = 'x' * 16000
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
del held
candidate = 'z' * 16000
return len(candidate)
""", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 28000 };
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        using var resumed = new LythonEngine().Run("return len('z' * 16000)", host,
            new LythonRunOptions { MemoryBudget = budget, MaxExecutionMemoryBytes = 28000 });
        Assert.True(resumed.Success, resumed.Failure?.Message);
        Assert.Equal(new BigInteger(16000), resumed.ReturnValue);
        resumed.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
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
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.Failure?.Attribution?.Reason);
        result.Dispose();
        script.Dispose();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeletionAfterCaughtDenialPreservesBatchAndAliasFunding(bool asynchronous, bool retain)
    {
        var script = new LythonEngine().Compile($$"""
held = 'x' * 16000
kept = held if {{(retain ? "True" : "False")}} else None
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
del held
candidate = 'z' * 16000
return len(candidate)
""");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 28000 };
        var host = new MockLythonHost();
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        result.Dispose();
        script.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StringLimitPrecedesMemoryAdmissionAndEmptyResultsStayFree(bool asynchronous)
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
