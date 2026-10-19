using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class AsyncStructuralAmbientTests
{
    [Theory]
    [InlineData("{probe(): 2}")]
    [InlineData("{**mapping()}")]
    [InlineData("{probe()}")]
    [InlineData("{*values()}")]
    [InlineData("set(values())")]
    [InlineData("dict.fromkeys(values())")]
    [InlineData("dict(pairs())")]
    [InlineData("Counter(values())")]
    [InlineData("{probe() for i in [0]}")]
    [InlineData("{probe(): probe() for i in [0]}")]
    public async Task StructuralAmbientNeverSpansGuestSuspension(string expression)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
import time
from collections import Counter
def probe():
    time.sleep(1)
    return 1
def mapping():
    time.sleep(1)
    return {'a': 1}
def values():
    yield probe()
def pairs():
    yield ('a', probe())
""" + "\nresult = " + expression + "\nprint(len(result))", budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        var timing = new AmbientCheckingTiming();
        host.SetTiming(timing);
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1\n", result.StandardOutput);
            Assert.True(timing.Calls > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private sealed class AmbientCheckingTiming : ILythonTiming
    {
        internal int Calls;
        public long MonotonicNanoseconds => 0;

        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            // A synchronous comparer scope must end on the same thread. It
            // cannot remain installed while arbitrary guest/host work awaits.
            Assert.Null(PyStructuralGuard.AmbientContext);
            Calls++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
