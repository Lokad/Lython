using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: readers stream single-pass. Indexing, slicing and len() are not
/// supported (materialize with list(reader)); truth testing and rendering
/// never pull input; every iterator shares the source cursor like CPython.
/// </summary>
public sealed class CsvReaderContractTests
{
    [Fact]
    public async Task ReaderIndexingAndLengthAreRejected()
    {
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["a,b", "1,2"])
            d = csv.DictReader(["a,b", "1,2"])
            return [r[0], len(r), d[0], len(d)]
            """);
        Assert.False(script.IsValid);
        Assert.Equal(4, script.Diagnostics.Count(d => d.Code is "LA3115" or "LA3032"));
    }

    [Fact]
    public async Task ReaderIndexingAndLengthFailAtRuntime()
    {
        var script = new LythonEngine().Compile("""
            import csv
            def at(reader):
                return reader[0]
            def count(reader):
                return len(reader)
            results = []
            for op in [at, count]:
                for maker in [csv.reader, csv.DictReader]:
                    try:
                        op(maker(["a,b"]))
                    except TypeError as e:
                        results.append(type(e).__name__)
                        results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "TypeError", "Object is not subscriptable.",
            "TypeError", "Object is not subscriptable.",
            "TypeError", "object of type 'csv.reader' has no len()",
            "TypeError", "object of type 'csv.DictReader' has no len()",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TruthTestingAndRenderingNeverPullInput()
    {
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["a,b", "1,2"])
            d = csv.DictReader(["a,b", "1,2"])
            return [bool(r), r.line_num, repr(r), r.line_num, bool(d), d.line_num, repr(d), d.line_num, d.fieldnames]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, new BigInteger(0), "<csv.reader object>", new BigInteger(0),
            true, new BigInteger(1), "<csv.DictReader object>", new BigInteger(1),
            new List<object?> { "a", "b" },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SecondPassSeesNothingNew()
    {
        // The cursor is shared like CPython: after one pass the reader is
        // exhausted, while line_num keeps the pulled count.
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["a,b", "1,2"])
            first = list(r)
            second = list(r)
            return [len(first), len(second), r.line_num]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(0), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
