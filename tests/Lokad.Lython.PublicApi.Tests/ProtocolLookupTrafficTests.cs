using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N10 part 2: side/store scans copy lazily on the first hash-match candidate,
// so distinct-hash builds stay near-linear instead of rescanning (and
// recopying) the whole population per key. Guest == still observes a stable
// snapshot, and builtin-key misses no longer enumerate a mutating list.
public sealed class ProtocolLookupTrafficTests
{
    private const string CustomKeyType = """
        class K:
            def __init__(self, v):
                self.v = v
            def __eq__(self, other):
                return isinstance(other, K) and self.v == other.v
            def __hash__(self):
                return self.v
        """;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static long AllocatedBytes(System.Func<LythonExecutionResult> run)
    {
        var before = System.GC.GetAllocatedBytesForCurrentThread();
        var result = run();
        Assert.True(result.Success, result.Failure?.Message);
        return (long)System.GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public async Task ProtocolDictBuild_TrafficStaysNearLinear()
    {
        var script = Compile(CustomKeyType + "\n" + """
            d = {}
            for i in range(2000):
                d[K(i)] = i
            return len(d)
            """);
        // Warmup run absorbs JIT and one-time setup; the measured run must
        // stay far below the eager-snapshot traffic (~88 MB here).
        _ = script.Run(new MockLythonHost());
        Assert.True(AllocatedBytes(() => script.Run(new MockLythonHost())) < 32000000);
        _ = await script.RunAsync(new MockLythonHost());
        Assert.True(AllocatedBytes(() => script.Run(new MockLythonHost())) < 32000000);
    }

    [Fact]
    public async Task ProtocolSetBuild_TrafficStaysNearLinear()
    {
        var script = Compile(CustomKeyType + "\n" + """
            s = set()
            for i in range(2000):
                s.add(K(i))
            return len(s)
            """);
        _ = script.Run(new MockLythonHost());
        Assert.True(AllocatedBytes(() => script.Run(new MockLythonHost())) < 32000000);
        _ = await script.RunAsync(new MockLythonHost());
        Assert.True(AllocatedBytes(() => script.Run(new MockLythonHost())) < 32000000);
    }

    [Fact]
    public async Task BuiltinMissWithMutatingEquality_ReturnsFalse()
    {
        var script = Compile("""
            class K:
                def __init__(self, v):
                    self.v = v
                def __hash__(self):
                    return self.v
                def __eq__(self, other):
                    d[K(999)] = 999
                    return isinstance(other, K) and self.v == other.v
            d = {}
            d[K(1)] = 1
            return [1 in d, len(d)]
            """);
        var expected = new List<object?> { false, new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SetBuiltinMissWithMutatingEquality_ReturnsFalse()
    {
        var script = Compile("""
            class K:
                def __init__(self, v):
                    self.v = v
                def __hash__(self):
                    return self.v
                def __eq__(self, other):
                    s.add(K(999))
                    return isinstance(other, K) and self.v == other.v
            s = set()
            s.add(K(1))
            return [1 in s, len(s)]
            """);
        var expected = new List<object?> { false, new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AddDuringScan_SeesConsistentTable()
    {
        var script = Compile("""
            class K:
                def __init__(self, v):
                    self.v = v
                def __hash__(self):
                    return self.v
                def __eq__(self, other):
                    if len(seen) == 0:
                        d[K(99)] = 99
                    seen.append(self.v)
                    return isinstance(other, K) and self.v == other.v
            seen = []
            d = {}
            d[K(1)] = 10
            d[K(2)] = 20
            return [d.get(K(2)), len(d)]
            """);
        var expected = new List<object?> { new BigInteger(20), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StorePhaseMutation_SeesConsistentTable()
    {
        var script = Compile("""
            class K:
                def __init__(self, v):
                    self.v = v
                def __hash__(self):
                    return self.v
                def __eq__(self, other):
                    d[5] = 50
                    return isinstance(other, K) and self.v == other.v
            d = {}
            d[5] = 5
            d[K(1)] = 10
            return [d.get(K(9)), sorted([k.v if isinstance(k, K) else k for k in d.keys()])]
            """);
        var expected = new List<object?>
        {
            null,
            new List<object?> { new BigInteger(1), new BigInteger(5) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
