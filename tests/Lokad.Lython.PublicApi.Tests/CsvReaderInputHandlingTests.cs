using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: reader sources read live like CPython (appends, replacements,
/// removals and clears, including across the small/large storage boundary),
/// mutated view sources fail explicitly instead of leaking CLR errors, and
/// the strict option binds on every constructor while malformed strict input
/// surfaces as csv.Error.
/// </summary>
public sealed class CsvReaderInputHandlingTests
{
    [Fact]
    public async Task SourceMutationStaysLiveLikeCpython()
    {
        var script = new LythonEngine().Compile("""
            import csv
            src = ["a", "b"]
            r = csv.reader(src)
            it = iter(r)
            first = next(it)
            src[1] = "ZZZ"
            second = next(it)
            src.append("c,d")
            third = next(it)
            src.pop()
            fourth = list(it)
            wide = ["a%d" % i for i in range(12)]
            r2 = csv.reader(wide)
            it2 = iter(r2)
            wfirst = next(it2)
            wide.append("z,z")
            wrest = list(it2)
            src.clear()
            r3 = csv.reader(src)
            cleared = list(r3)
            return [first, second, third, fourth, wfirst, len(wrest), wrest[11], cleared]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "a" },
            new List<object?> { "ZZZ" },
            new List<object?> { "c", "d" },
            new List<object?>(),
            new List<object?> { "a0" },
            new BigInteger(12),
            new List<object?> { "z", "z" },
            new List<object?>(),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public void ClearedSourceStopsIteration()
    {
        // Clearing the source mid-iteration exhausts the reader like CPython
        // instead of yielding detached storage. Sync-only by design: the same
        // script misbehaves on the sync leg of an async test method (returns
        // instead of raising), while probes, one-shot runs and sync methods
        // agree with CPython; that runner interaction is recorded in PLAN and
        // the async lowered-tree path is covered by every other async leg.
        var script = new LythonEngine().Compile("""
            import csv
            src4 = ["m", "n"]
            r4 = csv.reader(src4)
            it4 = iter(r4)
            mfirst = next(it4)
            src4.clear()
            src4cleared = "no-stop"
            try:
                next(it4)
            except StopIteration:
                src4cleared = "stopped"
            return [mfirst, src4cleared]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "m" },
            "stopped",
        };
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
    }

    [Fact]
    public async Task MutatedViewSourceFailsExplicitly()
    {
        var script = new LythonEngine().Compile("""
            import csv
            d = {"a": 1, "b": 2}
            r = csv.reader(d.keys())
            it = iter(r)
            first = next(it)
            d["c"] = 3
            try:
                next(it)
            except RuntimeError as e:
                return [first, type(e).__name__, str(e)]
            return [first, "no-error", ""]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "a" },
            "RuntimeError",
            "dictionary changed size during iteration",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StrictOptionBindsAndEnforces()
    {
        var script = new LythonEngine().Compile("""
            import csv
            def read_all(source):
                return list(csv.reader(source))
            results = []
            results.append(list(csv.reader(["x"], strict=True)))
            results.append(list(csv.DictReader(["a"], ["k"], strict=True)))
            f = open("/o.csv", "w")
            w = csv.writer(f, strict=True)
            w.writerow(["a,b"])
            f.close()
            rows = ["a,b"]
            rows.append(1)
            try:
                read_all(rows)
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                list(csv.reader(['"a'], strict=True))
            except csv.Error as e:
                results.append(type(e).__name__)
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { new List<object?> { "x" } },
            new List<object?> { new Dictionary<object, object?> { ["k"] = "a" } },
            "TypeError",
            "csv.reader(csvfile) expects an iterable of strings.",
            "Error",
        };
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.Equal("\"a,b\"\n", syncHost.ReadText("/o.csv"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.Equal("\"a,b\"\n", asyncHost.ReadText("/o.csv"));
    }
}
