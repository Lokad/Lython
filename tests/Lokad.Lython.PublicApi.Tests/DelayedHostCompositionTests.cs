using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// H01: composed operations suspend through the async host boundary instead
/// of consuming async file capabilities synchronously. Enumerate/zip drive
/// async cursors, json.load reads asynchronously, and strict zip peeks tails
/// asynchronously; every run below completes at least one read asynchronously.
/// Investigation #4 extends the same composition to generator outers and the
/// dict/Counter/defaultdict/deque/bytes/join/fromkeys materializers, with
/// math/time constructors over host-derived values; synchronous runs keep
/// failing fast with RunAsync guidance instead of wedging.
/// </summary>
public sealed class DelayedHostCompositionTests
{
    [Fact]
    public async Task DelayedAsyncEnumerateOverOpenFile()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/s.txt") as f:
                pairs = [(i, line) for i, line in enumerate(f)]
            return [len(pairs), pairs[0][0], pairs[0][1], pairs[2][0], pairs[2][1]]
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/s.txt", "a\nb\nc\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(3), new BigInteger(0), "a\n", new BigInteger(2), "c\n" }, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncJsonLoadOverOpenFile()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            with open("/v.json") as f:
                value = json.load(f)
            return [value["a"], value["b"]]
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/v.json", "{\"a\": 1, \"b\": \"x\"}");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1), "x" }, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncZipOverTwoDictReaders()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/a.csv") as before, open("/b.csv") as after:
                n = 0
                for a, b in zip(csv.DictReader(before), csv.DictReader(after)):
                    n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/a.csv", "id,pad\n0,y\n1,y\n");
        host.SeedFile("/b.csv", "id,pad\n0,z\n1,z\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(2), result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncStrictZipReportsLongerTail()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/c.csv") as one, open("/d.csv") as two:
                try:
                    for a, b in zip(csv.DictReader(one), csv.DictReader(two), strict=True):
                        pass
                    return "no-error"
                except ValueError:
                    return "longer"
            """);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/c.csv", "id\n0\n");
        host.SeedFile("/d.csv", "id\n0\n1\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("longer", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    private static DelayedLythonHost SeedCompositionHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/n.txt", "3\n1\n4\n");
        host.SeedFile("/d.txt", "2020-01-02");
        return host;
    }

    private static MockLythonHost SeedCompositionSyncHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/n.txt", "3\n1\n4\n");
        host.SeedFile("/d.txt", "2020-01-02");
        return host;
    }

    private static async Task AssertAsyncMatchesSync(string source, object? expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(diagnostic => diagnostic.Code + ":" + diagnostic.Message)));

        var sync = script.Run(SeedCompositionSyncHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var delayed = SeedCompositionHost();
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task GenexpOuterSuspendsPerPull()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    return sum(1 for line in f)\n",
            new BigInteger(5));
    }

    [Fact]
    public async Task DictFromSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    return dict(enumerate(f))[0]\n",
            "3\n");
    }

    [Fact]
    public async Task CounterFromSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import Counter\nwith open(\"/r.txt\") as f:\n    return Counter(f)[\"1\\n\"]\n",
            new BigInteger(2));
    }

    [Fact]
    public async Task DefaultDictFromSuspendingPairsComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import defaultdict\nwith open(\"/r.txt\") as f:\n    d = defaultdict(int, ((line, 1) for line in f))\n    return [len(d), d[\"1\\n\"]]\n",
            new List<object?> { new BigInteger(4), new BigInteger(1) });
    }

    [Fact]
    public async Task DequeFromSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "from collections import deque\nwith open(\"/r.txt\") as f:\n    return len(deque(f))\n",
            new BigInteger(5));
    }

    [Fact]
    public async Task BytesFromSuspendingOperandsComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/n.txt\") as f:\n    return len(bytes(int(x) for x in f))\n",
            new BigInteger(3));
    }

    [Fact]
    public async Task JoinOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    return \"|\".join(line.strip() for line in f)\n",
            "3|1|4|1|5");
    }

    [Fact]
    public async Task FromKeysOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "with open(\"/r.txt\") as f:\n    return len(dict.fromkeys(f))\n",
            new BigInteger(4));
    }

    [Fact]
    public async Task MathAndTimeConstructorsOverHostValuesCompose()
    {
        await AssertAsyncMatchesSync(
            "import datetime\nimport math\nwith open(\"/d.txt\") as f:\n    day = datetime.date.fromisoformat(f.read().strip()).isoformat()\nwith open(\"/n.txt\") as f:\n    total = math.fsum(int(x) for x in f)\nreturn [day, total]\n",
            new List<object?> { "2020-01-02", 8.0 });
    }

    [Theory]
    [InlineData("with open(\"/r.txt\") as f:\n    return sorted(f)\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    return dict(enumerate(f))\n")]
    [InlineData("from collections import Counter\nwith open(\"/r.txt\") as f:\n    return Counter(f)\n")]
    [InlineData("with open(\"/r.txt\") as f:\n    return \"|\".join(f)\n")]
    public void SyncRunsFailFastWithRunAsyncGuidance(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var result = script.Run(SeedCompositionHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }
}
