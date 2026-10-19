using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 1048576);

        var host2 = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed2 }), 1048576);
    }

    [Fact]
    public async Task UncalledLiteralPayloadsStayCharged()
    {
        var body = "def f():\n    return '" + new string('x', 250000) + "'\n";
        var host = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed, out var imports);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 1048576);

        var host2 = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed2 }), 1048576);
    }

    [Fact]
    public async Task UncalledWideDisplaysStayCharged()
    {
        var body = "def f():\n    return [" + string.Concat(Enumerable.Repeat("1,", 10000)) + "1]\n";
        var host = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed, out var imports);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(3145728), AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 3145728);

        var host2 = SeededHost(["m0", "m1", "m2"], [body, body, body], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(3145728), AllowedLocalModules = allowed2 }), 3145728);
    }

    [Fact]
    public async Task PreparedFailedImportsRemainFundedUntilExecutionCleanup()
    {
        var body = "def f():\n    return [" + string.Concat(Enumerable.Repeat("1,", 10000)) + "1]\nraise ValueError('boom')\n";
        var once = "ok = 0\ntry:\n    import m0\nexcept ValueError:\n    ok = 1\nreturn ok\n";
        var twice = "ok = 0\ntry:\n    import m0\nexcept ValueError:\n    ok = 1\ntry:\n    import m0\nexcept ValueError:\n    ok += 1\nreturn ok\n";
        var budget = 8388608L;
        var allowance = new LythonMemoryBudget(16 * 1024 * 1024);
        var peakOnceSync = RunImportRetry(once, body, budget, 1, allowance);
        var peakTwiceSync = RunImportRetry(twice, body, budget, 2, allowance);
        Assert.True(peakTwiceSync > peakOnceSync, "once=" + peakOnceSync + " twice=" + peakTwiceSync);

        var peakOnceAsync = await RunImportRetryAsync(once, body, budget, 1, allowance);
        var peakTwiceAsync = await RunImportRetryAsync(twice, body, budget, 2, allowance);
        Assert.True(peakTwiceAsync > peakOnceAsync, "once=" + peakOnceAsync + " twice=" + peakTwiceAsync);
    }

    private static long RunImportRetry(string entry, string moduleBody, long budget, int expected, LythonMemoryBudget allowance)
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", moduleBody);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var baseline = allowance.ReservedBytes;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(entry, allowance);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        using var result = script.Run(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget), AllowedLocalModules = allowed });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(expected), result.ReturnValue);
        var peak = result.PeakExecutionMemoryBytes;
        result.Dispose();
        script.Dispose();
        Assert.Equal(baseline, allowance.ReservedBytes);
        return peak;
    }

    private static async Task<long> RunImportRetryAsync(string entry, string moduleBody, long budget, int expected, LythonMemoryBudget allowance)
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", moduleBody);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var baseline = allowance.ReservedBytes;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(entry, allowance);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        using var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget), AllowedLocalModules = allowed });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(expected), result.ReturnValue);
        var peak = result.PeakExecutionMemoryBytes;
        result.Dispose();
        script.Dispose();
        Assert.Equal(baseline, allowance.ReservedBytes);
        return peak;
    }
    [Fact]
    public async Task UncalledLambdaPayloadsStayCharged()
    {
        var item = "f = lambda: '" + new string('x', 250000) + "'\n";
        var host = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed, out var imports);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options), 1048576);

        var host2 = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed2 }), 1048576);
    }

    [Fact]
    public async Task EntryGeneratorAndLambdaStayHostOwned()
    {
        // The other direction: entry-script deferred graphs alias the reusable
        // host-owned compilation, so big uncalled entry graphs fit.
        var source = "g = ([" + string.Concat(Enumerable.Repeat("1,", 20000)) + "1] for i in range(1))\n"
            + "f = lambda: '" + new string('x', 250000) + "'\nreturn 1\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    // Honest budget: 3 x 2k int items retain ~160 B each (node plus payload),
    // about 1 MiB before the streams themselves run.

    [Fact]
    public async Task SmallGeneratorStreamsFunded()
    {
        var item = "g = ([" + string.Concat(Enumerable.Repeat("1,", 2000)) + "1] for i in range(1))\n";
        var host = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed, out var imports);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "return len(list(m0.g)[0]) + len(list(m1.g)[0]) + len(list(m2.g)[0])\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(6003);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(2097152), AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(["m0", "m1", "m2"], [item, item, item], out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(2097152), AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallLiteralAndWideCallsFit()
    {
        var lit = "def f():\n    return '" + new string('x', 2500) + "'\n";
        var wide = "def f():\n    return [" + string.Concat(Enumerable.Repeat("1,", 100)) + "1]\n";
        var host = SeededHost(["m0", "m1"], [lit, wide], out var allowed, out var imports);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(imports + "return len(m0.f()) + len(m1.f())\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(2601);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(["m0", "m1"], [lit, wide], out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576), AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
