using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N15: collection mutation consumers compose through async suspension instead
// of failing with synchronous-read guidance. Each awaited path shares its
// shaping, validation and accounting facts with the sync twin; only the
// iterable drain suspends.
public sealed class MutationAsyncCompositionTests
{
    private static DelayedLythonHost SeedDelayedHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n");
        return host;
    }

    private static MockLythonHost SeedSyncHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n");
        return host;
    }

    private static async Task AssertAsyncMatchesSync(string source, object? expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(SeedSyncHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = SeedDelayedHost();
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ListExtendOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    x = []\n    x.extend(f)\n    return len(x)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task ListInPlaceAddOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    x = [9]\n    x += f\n    return [len(x), x[0]]\n",
            new List<object?> { new BigInteger(4), new BigInteger(9) });
    }

    [Fact]
    public async Task DictUpdateOverSuspendingPairsComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    d = {}\n    d.update((l, 1) for l in f)\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task DictInPlaceOrOverSuspendingPairsComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    d = {}\n    d |= ((l, 1) for l in f)\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task DefaultdictUpdateOverSuspendingPairsComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import defaultdict\nwith open(\"/r.txt\") as f:\n    d = defaultdict(int)\n    d.update((l, 1) for l in f)\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task DefaultdictInPlaceOrOverSuspendingPairsComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import defaultdict\nwith open(\"/r.txt\") as f:\n    d = defaultdict(int)\n    d |= ((l, 1) for l in f)\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task SetUpdateOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    s = set()\n    s.update(f)\n    return len(s)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task SetIntersectionUpdateOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    s = set([\"3\\n\", \"9\\n\"])\n    s.intersection_update(l for l in f)\n    return len(s)\n",
            new BigInteger(1));
    }

    [Fact]
    public async Task SetDifferenceUpdateOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    s = set([\"3\\n\", \"9\\n\"])\n    s.difference_update(l for l in f)\n    return len(s)\n",
            new BigInteger(1));
    }

    [Fact]
    public async Task SetSymmetricDifferenceUpdateOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    s = set([\"9\\n\"])\n    s.symmetric_difference_update(l for l in f)\n    return len(s)\n",
            new BigInteger(4));
    }

    [Fact]
    public async Task DequeExtendOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import deque\nwith open(\"/r.txt\") as f:\n    d = deque()\n    d.extend(f)\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task DequeExtendLeftPreservesOrderOverSuspendingSource()
    {
        await AssertAsyncMatchesSync(
            "from collections import deque\nwith open(\"/r.txt\") as f:\n    d = deque()\n    d.extendleft(f)\n    return [len(d), d[0]]\n",
            new List<object?> { new BigInteger(3), "4\n" });
    }

    [Fact]
    public async Task DequeInPlaceAddOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import deque\nwith open(\"/r.txt\") as f:\n    d = deque()\n    d += f\n    return len(d)\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task CounterUpdateOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import Counter\nwith open(\"/r.txt\") as f:\n    c = Counter()\n    c.update(f)\n    return sum(c.values())\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task CounterSubtractOverSuspendingSourceComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import Counter\nwith open(\"/r.txt\") as f:\n    c = Counter([\"3\\n\", \"1\\n\"])\n    c.subtract(l for l in f)\n    return sum(c.values())\n",
            new BigInteger(-1));
    }

    [Fact]
    public async Task NamedTupleFieldNamesFromSuspendingSourceCompose()
    {
        await AssertAsyncMatchesSync(
            "from collections import namedtuple\nwith open(\"/r.txt\") as f:\n    T = namedtuple(\"T\", (l for l in f), rename=True)\n    return [len(T._fields), T._fields[0], T._fields[2]]\n",
            new List<object?> { new BigInteger(3), "_0", "_2" });
    }

    [Fact]
    public async Task NamedTupleDefaultsFromSuspendingSourceCompose()
    {
        await AssertAsyncMatchesSync(
            "from collections import namedtuple\nwith open(\"/r.txt\") as f:\n    T = namedtuple(\"T\", [\"a\", \"b\", \"c\"], defaults=(int(l) for l in f))\n    u = T(0)\n    return [u[0], u[1], u[2]]\n",
            new List<object?> { new BigInteger(0), new BigInteger(1), new BigInteger(4) });
    }

    [Fact]
    public async Task NamedTupleInvalidFieldNamesRejectInBothModes()
    {
        const string source = "from collections import namedtuple\nwith open(\"/r.txt\") as f:\n    return namedtuple(\"T\", (l for l in f))\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var sync = script.Run(SeedSyncHost());
        Assert.False(sync.Success);
        Assert.Equal("ValueError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(SeedDelayedHost());
        Assert.False(asyncResult.Success);
        Assert.Equal(sync.Failure?.ExceptionType, asyncResult.Failure?.ExceptionType);
        Assert.Equal(sync.Failure?.Message, asyncResult.Failure?.Message);
    }

    [Theory]
    [InlineData("with open(\"/r.txt\") as f:\n    x = []\n    x.extend(f)\n    return x\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    x = [9]\n    x += f\n    return x\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    d = {}\n    d.update((l, 1) for l in f)\n    return d\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    d = {}\n    d |= ((l, 1) for l in f)\n    return d\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    s = set()\n    s.update(f)\n    return s\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    s = set()\n    s.intersection_update(f)\n    return s\n")]
    [InlineData("from collections import deque\nwith open(\"/r.txt\") as f:\n    d = deque()\n    d.extend(f)\n    return d\n")]
    [InlineData("from collections import Counter\nwith open(\"/r.txt\") as f:\n    c = Counter()\n    c.update(f)\n    return c\n")]
    [InlineData("from collections import namedtuple\nwith open(\"/r.txt\") as f:\n    return namedtuple(\"T\", (l for l in f), rename=True)\n")]
    public void SyncRunsFailFastWithRunAsyncGuidance(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var result = script.Run(SeedDelayedHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }
}
