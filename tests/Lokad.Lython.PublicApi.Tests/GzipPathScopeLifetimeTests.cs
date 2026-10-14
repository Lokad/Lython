using System.IO.Compression;
using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipPathScopeLifetimeTests
{
    public static IEnumerable<object[]> Modes()
    {
        foreach (var mode in new[] { "rb", "rt", "wb", "wt", "ab", "at" })
            foreach (var asynchronous in new[] { false, true })
                foreach (var retain in new[] { false, true })
                    yield return [mode, asynchronous, retain];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task EscapingPathHandlePreservesBatchFundingAndExplicitAliases(string mode, bool asynchronous, bool retain)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile($$"""
import gzip
def acquire():
    unrelated = 'x' * 16000
    assert len(locals()['unrelated']) == 16000
    file = gzip.open('/data.gz', '{{mode}}')
    return file, unrelated if {{(retain ? "True" : "False")}} else None
file, kept = acquire()
with open('/checkpoint', 'rb') as checkpoint:
    assert checkpoint.read(1) == b'g'
print('gate')
candidate = 'y' * 16000
if '{{mode}}'.startswith('r'):
    expected = 'alpha\nbeta\n' if '{{mode}}'.endswith('t') else b'alpha\nbeta\n'
    assert file.read() == expected
else:
    file.write('résumé\n' if '{{mode}}'.endswith('t') else b'gamma\n')
file.close()
assert file.closed
return len(candidate)
""", budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var member = Convert.FromHexString("1F8B08007B00000002FF4BCC29C848E44A4A2D49E402006E50306E0B000000");
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
                delayed.SeedBytes("/data.gz", member);
                delayed.SeedBytes("/checkpoint", [(byte)'g']);
            }
            else
            {
                mock!.SeedBytes("/data.gz", member);
                mock.SeedBytes("/checkpoint", [(byte)'g']);
            }
            var options = new LythonRunOptions { MaxExecutionMemoryBytes = cap };
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal("gate\n", result.StandardOutput);
            Assert.InRange(result.PeakExecutionMemoryBytes, 16000, cap);
            if (cap == 28000)
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.Failure?.Attribution?.Reason);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(16000), result.ReturnValue);
                var bytes = delayed?.ReadBytes("/data.gz") ?? mock!.ReadBytes("/data.gz");
                using var compressed = new MemoryStream(bytes);
                using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                var expected = mode.StartsWith('r') ? "alpha\nbeta\n"
                    : (mode.StartsWith('a') ? "alpha\nbeta\n" : "") + (mode.EndsWith('t') ? "résumé\n" : "gamma\n");
                Assert.Equal(expected, await reader.ReadToEndAsync());
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
