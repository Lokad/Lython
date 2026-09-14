using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG22: a constructed imported function retains its lowered body graph even
// when the body never executes, so each distinct retained body owns its deep
// statement count once per run (first-wins over aliases, re-imports, and
// re-executed sites). Host-owned precompiled code stays outside the charge.
public sealed class ImportedCodeRetentionScenarioTests
{
    private const long OneMib = 1048576;

    private static string BigFunction(string name, int statements)
        => "def " + name + "():\n" + string.Concat(Enumerable.Repeat("    x = 1\n", statements)) + "    return x\n";

    private static MockLythonHost SeededHost(int modules, int statements, out HashSet<string> allowed, out string imports)
    {
        var host = new MockLythonHost();
        allowed = new HashSet<string>(StringComparer.Ordinal);
        imports = "";
        for (var i = 0; i < modules; i++)
        {
            host.SeedFile("/m" + i + ".py", BigFunction("f" + i, statements));
            allowed.Add("m" + i);
            imports += "import m" + i + "\n";
        }

        return host;
    }

    private static void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= OneMib);
    }

    [Fact]
    public async Task UncalledImportedBodiesStayCharged()
    {
        var host = SeededHost(3, 20000, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = SeededHost(3, 20000, out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task FundedImportedBodiesSucceedAndRun()
    {
        var host = SeededHost(3, 20000, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "return m2.f2()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 8388608, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(3, 20000, out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 8388608, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallImportedBodiesFitTightBudget()
    {
        var host = SeededHost(1, 500, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "return m0.f0()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(1, 500, out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SharedImportsPayOnce()
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", BigFunction("f0", 8000));
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var script = new LythonEngine().Compile(
            "import m0\nimport m0\nfrom m0 import f0\nimport m0 as mm\nreturn f0() + mm.f0()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(2);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/m0.py", BigFunction("f0", 8000));
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FunctionlessModulesStayLight()
    {
        var host = new MockLythonHost();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var imports = "";
        for (var i = 0; i < 3; i++)
        {
            host.SeedFile("/p" + i + ".py", string.Concat(Enumerable.Repeat("pass\n", 20000)));
            allowed.Add("p" + i);
            imports += "import p" + i + "\n";
        }

        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);

        var host2 = new MockLythonHost();
        var allowed2 = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 3; i++)
        {
            host2.SeedFile("/p" + i + ".py", string.Concat(Enumerable.Repeat("pass\n", 20000)));
            allowed2.Add("p" + i);
        }

        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task ClassMethodBodiesStayCharged()
    {
        var body = "class K:\n    def method(self):\n" + string.Concat(Enumerable.Repeat("        x = 1\n", 20000)) + "        return x\n";
        var host = new MockLythonHost();
        host.SeedFile("/k.py", body);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "k" };
        var script = new LythonEngine().Compile("import k\nprint(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        AssertMemoryError(script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed }));

        var host2 = new MockLythonHost();
        host2.SeedFile("/k.py", body);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "k" };
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task NestedDeferredBodiesStayCharged()
    {
        var body = "def outer():\n    def inner():\n" + string.Concat(Enumerable.Repeat("        x = 1\n", 10000)) + "        return x\n    return inner\n";
        var host = new MockLythonHost();
        host.SeedFile("/n0.py", body);
        host.SeedFile("/n1.py", body);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "n0", "n1" };
        var script = new LythonEngine().Compile("import n0\nimport n1\nprint(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        AssertMemoryError(script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed }));

        var host2 = new MockLythonHost();
        host2.SeedFile("/n0.py", body);
        host2.SeedFile("/n1.py", body);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "n0", "n1" };
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 }));
    }
    // Loop-re-executed definition sites share one retained body list, so the
    // per-run first-wins mark owns it once beside the per-construction shells.
    [Fact]
    public async Task LoopRedefinitionsShareCodeCharge()
    {
        var loopModule = "fs = []\ni = 0\nwhile i < 3000:\n    def f():\n"
            + string.Concat(Enumerable.Repeat("        t = 1\n", 20))
            + "        return t\n    fs.append(f)\n    i = i + 1\n";
        var host = new MockLythonHost();
        host.SeedFile("/loop.py", loopModule);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "loop" };
        var script = new LythonEngine().Compile("import loop\nreturn len(loop.fs)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(3000);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/loop.py", loopModule);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "loop" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CaughtImportFailureAllowsReuse()
    {
        var host = new MockLythonHost();
        host.SeedFile("/big.py", BigFunction("f", 20000));
        host.SeedFile("/small.py", "value = 1\n");
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "big", "small" };
        var source = "state = []\ntry:\n    import big\n    state.append(\"first-ok\")\nexcept MemoryError:\n    state.append(\"first-denied\")\n"
            + "try:\n    import big\n    state.append(\"second-ok\")\nexcept MemoryError:\n    state.append(\"second-denied\")\n"
            + "import small\nstate.append(small.value)\nreturn state\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { "first-denied", "second-denied", new BigInteger(1) };
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/big.py", BigFunction("f", 20000));
        host2.SeedFile("/small.py", "value = 1\n");
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "big", "small" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
