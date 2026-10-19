using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextFileScopeLifetimeTests
{
    [Theory]
    [InlineData("r", false, false, false)]
    [InlineData("r", false, true, false)]
    [InlineData("r", true, false, false)]
    [InlineData("r", true, true, false)]
    [InlineData("w", false, false, false)]
    [InlineData("w", false, true, false)]
    [InlineData("w", true, false, false)]
    [InlineData("w", true, true, false)]
    [InlineData("a", false, false, false)]
    [InlineData("a", false, true, false)]
    [InlineData("a", true, false, false)]
    [InlineData("a", true, true, false)]
    [InlineData("r", false, false, true)]
    [InlineData("r", false, true, true)]
    [InlineData("r", true, false, true)]
    [InlineData("r", true, true, true)]
    public async Task EscapingFilePreservesBatchFundingAndExplicitAliases(
        string mode, bool close, bool asynchronous, bool retain)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile($$"""
def acquire():
    unrelated = 'x' * 16000
    assert len(locals()['unrelated']) == 16000
    file = open('/text', '{{mode}}')
    if {{(close ? "True" : "False")}}:
        file.close()
    return (file, unrelated) if {{(retain ? "True" : "False")}} else (file, None)
file, kept = acquire()
with open('/checkpoint', 'rb') as checkpoint:
    assert checkpoint.read(1) == b'g'
print('gate')
candidate = 'y' * 16000
assert file.closed == {{(close ? "True" : "False")}}
if not file.closed:
    if '{{mode}}' == 'r':
        assert file.read() == 'g'
    else:
        file.write('résumé\n')
    file.close()
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
            if (delayed is not null)
            {
                delayed.SeedFile("/text", "g");
                delayed.SeedBytes("/checkpoint", [(byte)'g']);
            }
            else
            {
                mock!.SeedFile("/text", "g");
                mock.SeedBytes("/checkpoint", [(byte)'g']);
            }
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) };
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal("gate\n", result.StandardOutput);
            Assert.InRange(result.PeakExecutionMemoryBytes, 16000, cap);
            if (cap == 28000)
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(16000), result.ReturnValue);
                if (mode != "r")
                {
                    var actual = await host.ReadTextUtf8Async("/text", CancellationToken.None);
                    Assert.Equal(close ? (mode == "w" ? "" : "g") : (mode == "w" ? "résumé\n" : "grésumé\n"),
                        System.Text.Encoding.UTF8.GetString(actual.Span));
                }
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
