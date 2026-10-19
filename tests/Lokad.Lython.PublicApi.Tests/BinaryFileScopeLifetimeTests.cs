using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BinaryFileScopeLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedEscapingFilePreservesBatchFundingAndWorksWithHeadroom(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
def acquire():
    unrelated = 'x' * 16000
    assert len(locals()['unrelated']) == 16000
    file = open('/blob', 'rb')
    file.close()
    return file
file = acquire()
with open('/blob', 'rb') as checkpoint:
    assert checkpoint.read(1) == b'g'
print('gate')
candidate = 'y' * 16000
assert file.closed
return len(candidate)
""", budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        // The original cap refuses conservatively, even after guest withdrawal.
        // A funded run separately preserves the handle and alias semantics.
        foreach (var cap in new[] { 28000L, 120000L })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            ILythonHost host = (ILythonHost?)delayed ?? mock!;
            if (delayed is not null) delayed.SeedBytes("/blob", [(byte)'g']);
            else mock!.SeedBytes("/blob", [(byte)'g']);
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) };
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            // The delayed checkpoint lets the completed acquire() continuation
            // unwind before testing retention by the escaped file itself.
            Assert.Equal("gate\n", result.StandardOutput);
            if (cap == 28000)
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(16000), result.ReturnValue);
            }
            Assert.InRange(result.PeakExecutionMemoryBytes, 16000, cap);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
