using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionStringAdmissionTests
{
    [Theory]
    [InlineData(false, "held = None", false)]
    [InlineData(true, "held = None", false)]
    [InlineData(false, "del held", false)]
    [InlineData(true, "del held", false)]
    [InlineData(false, "held = None", true)]
    [InlineData(true, "held = None", true)]
    public async Task WithdrawalInsideFunctionPreservesConservativeBatchFunding(bool asynchronous, string withdrawal, bool retain)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($$"""
def work():
    held = 'x' * 16000
    kept = held if {{(retain ? "True" : "False")}} else None
    try:
        candidate = 'y' * 16000
    except MemoryError:
        pass
    {{withdrawal}}
    candidate = 'z' * 16000
    return len(candidate)
return work()
""");
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(28000) };
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
    }
}

