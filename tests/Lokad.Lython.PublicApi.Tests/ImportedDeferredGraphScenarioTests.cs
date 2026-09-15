using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M01: retained import graphs must cover every deferred root (generator item
// expressions included) and the actual literal/lowered payloads, not just a
// per-node estimate. These shapes retain megabytes through uncalled values
// while accounting only kilobytes.
public sealed class ImportedDeferredGraphScenarioTests
{
    private static MockLythonHost SeededHost(string[] names, string[] bodies, out HashSet<string> allowed, out string imports)
    {
        var host = new MockLythonHost();
        allowed = new HashSet<string>(StringComparer.Ordinal);
        imports = "";
        for (var i = 0; i < names.Length; i++)
        {
            host.SeedFile("/" + names[i] + ".py", bodies[i]);
            allowed.Add(names[i]);
            imports += "import " + names[i] + "\n";
        }

        return host;
    }

    private static void AssertMemoryError(LythonExecutionResult result, long budget)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= budget);
    }

    [Fact]
    public async Task UncalledGeneratorItemsStayCharged()
    {
        var item = "g = ([" + string.Concat(Enumerable.Repeat("1,", 20000)) + "1] for i in range(1))\n";
        var host = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 1048576);

        var host2 = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed2 }), 1048576);
    }

    [Fact]
    public async Task UncalledLiteralPayloadsStayCharged()
    {
        var body = "def f():\n    return '" + new string('x', 250000) + "'\n";
        var host = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 1048576);

        var host2 = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed2 }), 1048576);
    }

    [Fact]
    public async Task UncalledWideDisplaysStayCharged()
    {
        var body = "def f():\n    return [" + string.Concat(Enumerable.Repeat("1,", 10000)) + "1]\n";
        var host = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3145728, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 3145728);

        var host2 = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 3145728, AllowedLocalModules = allowed2 }), 3145728);
    }

    [Fact]
    public async Task FailedImportReleasesChargeOnRetry()
    {
        // A failing module never reaches the registry: its deferred charge
        // releases, so a caught failure retried pays the same high-water mark
        // instead of one charge per attempt (here ~1.6 MB per leak).
        var body = "def f():\n    return [" + string.Concat(Enumerable.Repeat("1,", 10000)) + "1]\nraise ValueError('boom')\n";
        var once = "ok = 0\ntry:\n    import m0\nexcept ValueError:\n    ok = 1\nreturn ok\n";
        var twice = "ok = 0\ntry:\n    import m0\nexcept ValueError:\n    ok = 1\ntry:\n    import m0\nexcept ValueError:\n    ok += 1\nreturn ok\n";
        var budget = 8388608L;
        var peakOnceSync = RunImportRetry(once, body, budget, 1);
        var peakTwiceSync = RunImportRetry(twice, body, budget, 2);
        Assert.True(peakTwiceSync <= peakOnceSync + 65536, "once=" + peakOnceSync + " twice=" + peakTwiceSync);

        var peakOnceAsync = await RunImportRetryAsync(once, body, budget, 1);
        var peakTwiceAsync = await RunImportRetryAsync(twice, body, budget, 2);
        Assert.True(peakTwiceAsync <= peakOnceAsync + 65536, "once=" + peakOnceAsync + " twice=" + peakTwiceAsync);
    }

    private static long RunImportRetry(string entry, string moduleBody, long budget, int expected)
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", moduleBody);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var script = new LythonEngine().Compile(entry);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var result = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = budget, AllowedLocalModules = allowed });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(expected), result.ReturnValue);
        return result.PeakExecutionMemoryBytes;
    }

    private static async Task<long> RunImportRetryAsync(string entry, string moduleBody, long budget, int expected)
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", moduleBody);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var script = new LythonEngine().Compile(entry);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var result = await script.RunAsync(host, new LythonRunOptions { MaxExecutionMemoryBytes = budget, AllowedLocalModules = allowed });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(expected), result.ReturnValue);
        return result.PeakExecutionMemoryBytes;
    }
}
