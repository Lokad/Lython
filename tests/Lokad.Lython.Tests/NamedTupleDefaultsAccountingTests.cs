using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG05: namedtuple defaults-length validation must not materialize the
/// defaults first: a million-item lazy iterable raises the same TypeError
/// either way, but pre-fix it allocated hundreds of megabytes of transient
/// list backing to discover that. The drill measures per-thread allocations
/// around a synchronous failure.
/// </summary>
public sealed class NamedTupleDefaultsAccountingTests
{
    private const string OversizedDefaults =
        "import collections\nP = collections.namedtuple(\"P\", [\"a\", \"b\"], defaults=(x for x in range(1000000)))\n";

    [Fact]
    public void OversizedDefaultsAllocateNoProportionalTransient()
    {
        var script = new LythonEngine().Compile(OversizedDefaults);
        Assert.True(script.IsValid);
        // Warm up JIT and caches so the measured run counts steady-state work.
        _ = script.Run(new MockLythonHost());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = script.Run(new MockLythonHost());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Equal("collections.namedtuple(..., defaults=...) has more defaults than fields.", result.Failure?.Message);
        Assert.True(allocated < 8388608, $"defaults validation allocated {allocated} bytes before failing");
    }
}
