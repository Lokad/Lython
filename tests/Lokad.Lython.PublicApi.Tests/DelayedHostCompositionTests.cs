using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// H01: composed operations suspend through the async host boundary instead
/// of consuming async file capabilities synchronously. Enumerate/zip drive
/// async cursors, json.load reads asynchronously, and strict zip peeks tails
/// asynchronously; every run below completes at least one read asynchronously.
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
}
