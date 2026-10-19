using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvExhaustedInputLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExhaustedReaderPreservesCursorAndConservativeBatchFunding(bool retainInput, bool asynchronous)
    {
        var source = """
import csv
size = 6000
lines = ['x' * size, 'y' * size]
reader = csv.reader(lines)
""" + "\n" + (retainInput ? "" : "del lines\n") + """
count = 0
for row in reader:
    assert len(row[0]) == size
    count += 1
del row
with open('/gate', 'rb') as gate:
    assert gate.read(1) == b'g'
print('gate')
assert reader.line_num == 2
assert next(reader, None) is None
candidate = 'z' * 40000
return len(candidate)
""";
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedBytes("/gate", [(byte)'g']);
        else mock!.SeedBytes("/gate", [(byte)'g']);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(48000) };
        var baseline = budget.ReservedBytes;
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal("gate\n", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 48000);
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.True(result.FailureAttribution?.Requested >= 40000);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        using var resumed = new LythonEngine(Utf8RegexProvider.Instance).Run("return len('z' * 40000)", host,
            new LythonRunOptions { MemoryBudget = budget});
        Assert.True(resumed.Success, resumed.Failure?.Message);
        Assert.Equal(new BigInteger(40000), resumed.ReturnValue);
        resumed.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
