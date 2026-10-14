using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Imported code reserves a coarse compilation estimate before preparation and
// retains it for the execution. Imports/aliases preserve Python cache semantics;
// no retained-node inventory or exact code-graph fee is part of the contract.
public class ImportedCodeRetentionScenarioTests
{
    protected virtual int LargeStatements => 128;
    protected virtual int NestedStatements => 512;
    protected virtual int SmallStatements => 8;
    protected virtual int SharedStatements => 16;
    protected virtual int LoopDefinitions => 64;
    protected virtual long RetentionBudget => 65536;
    protected virtual long FundedBudget => 524288;
    protected virtual long SharedImportBudget => RetentionBudget;
    // Headroom includes compilation plus the retained function shells. Registry
    // entry fees and precise tiny-budget thresholds are intentionally obsolete.
    protected virtual long LoopRetainBudget => 131072;

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

    private void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= RetentionBudget);
    }

    [Fact]
    public async Task UncalledImportedBodiesStayCharged()
    {
        var host = SeededHost(3, LargeStatements, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = SeededHost(3, LargeStatements, out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }
    [Fact]
    public async Task FundedImportedBodiesSucceedAndRun()
    {
        var host = SeededHost(3, LargeStatements, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "return m2.f2()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = FundedBudget, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(3, LargeStatements, out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = FundedBudget, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallImportedBodiesFitTightBudget()
    {
        var host = SeededHost(1, SmallStatements, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "return m0.f0()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(1, SmallStatements, out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SharedImportsPayOnce()
    {
        var host = new MockLythonHost();
        host.SeedFile("/m0.py", "print('loaded')\n" + BigFunction("f0", SharedStatements));
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var script = new LythonEngine().Compile(
            "import m0\nimport m0\nfrom m0 import f0\nimport m0 as mm\nassert mm is m0 and f0 is m0.f0\nreturn f0() + mm.f0()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(2);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = SharedImportBudget, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.Equal("loaded\n", sync.StandardOutput);

        var host2 = new MockLythonHost();
        host2.SeedFile("/m0.py", "print('loaded')\n" + BigFunction("f0", SharedStatements));
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "m0" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = SharedImportBudget, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.Equal("loaded\n", asyncResult.StandardOutput);
    }

    [Fact]
    public async Task FunctionlessModulesAlsoRequireCompilationAdmission()
    {
        var host = new MockLythonHost();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var imports = "";
        for (var i = 0; i < 3; i++)
        {
            host.SeedFile("/p" + i + ".py", string.Concat(Enumerable.Repeat("pass\n", LargeStatements)));
            allowed.Add("p" + i);
            imports += "import p" + i + "\n";
        }

        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed });
        AssertMemoryError(sync);
        Assert.Empty(sync.StandardOutput);

        var host2 = new MockLythonHost();
        var allowed2 = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 3; i++)
        {
            host2.SeedFile("/p" + i + ".py", string.Concat(Enumerable.Repeat("pass\n", LargeStatements)));
            allowed2.Add("p" + i);
        }

        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 });
        AssertMemoryError(asyncResult);
        Assert.Empty(asyncResult.StandardOutput);
    }

    [Fact]
    public async Task ClassMethodBodiesStayCharged()
    {
        var body = "class K:\n    def method(self):\n" + string.Concat(Enumerable.Repeat("        x = 1\n", LargeStatements)) + "        return x\n";
        var host = new MockLythonHost();
        host.SeedFile("/k.py", body);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "k" };
        var script = new LythonEngine().Compile("import k\nprint(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        AssertMemoryError(script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed }));

        var host2 = new MockLythonHost();
        host2.SeedFile("/k.py", body);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "k" };
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task NestedDeferredBodiesStayCharged()
    {
        var body = "def outer():\n    def inner():\n" + string.Concat(Enumerable.Repeat("        x = 1\n", NestedStatements)) + "        return x\n    return inner\n";
        var host = new MockLythonHost();
        host.SeedFile("/n0.py", body);
        host.SeedFile("/n1.py", body);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "n0", "n1" };
        var script = new LythonEngine().Compile("import n0\nimport n1\nprint(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        AssertMemoryError(script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed }));

        var host2 = new MockLythonHost();
        host2.SeedFile("/n0.py", body);
        host2.SeedFile("/n1.py", body);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "n0", "n1" };
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task FundedLoopDefinitionsPreserveCallableCode()
    {
        var loopModule = "fs = []\ni = 0\nwhile i < " + LoopDefinitions + ":\n    def f():\n"
            + string.Concat(Enumerable.Repeat("        t = 1\n", 20))
            + "        return t\n    fs.append(f)\n    i = i + 1\n";
        var host = new MockLythonHost();
        host.SeedFile("/loop.py", loopModule);
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "loop" };
        var script = new LythonEngine().Compile("import loop\nassert loop.fs[0]() == 1\nassert loop.fs[-1]() == 1\nreturn len(loop.fs)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(LoopDefinitions);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = LoopRetainBudget, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/loop.py", loopModule);
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "loop" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = LoopRetainBudget, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CaughtImportFailureAllowsReuse()
    {
        var host = new MockLythonHost();
        host.SeedFile("/big.py", BigFunction("f", LargeStatements));
        host.SeedFile("/small.py", "value = 1\n");
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "big", "small" };
        var source = "state = []\ntry:\n    import big\n    state.append(\"first-ok\")\nexcept MemoryError:\n    state.append(\"first-denied\")\n"
            + "try:\n    import big\n    state.append(\"second-ok\")\nexcept MemoryError:\n    state.append(\"second-denied\")\n"
            + "import small\nstate.append(small.value)\nreturn state\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { "first-denied", "second-denied", new BigInteger(1) };
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/big.py", BigFunction("f", LargeStatements));
        host2.SeedFile("/small.py", "value = 1\n");
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "big", "small" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task WideUncalledBodiesStayCharged()
    {
        // One wide return expression retains tens of thousands of constant
        // nodes without executing: statement counts alone miss it.
        var body = "def f():\n    return [" + string.Join((char)44, Enumerable.Repeat("1", LargeStatements)) + "]\n";
        var host = new MockLythonHost();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var imports = "";
        for (var i = 0; i < 3; i++)
        {
            host.SeedFile("/m" + i + ".py", body);
            allowed.Add("m" + i);
            imports += "import m" + i + "\n";
        }

        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = new MockLythonHost();
        var allowed2 = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 3; i++)
        {
            host2.SeedFile("/m" + i + ".py", body);
            allowed2.Add("m" + i);
        }

        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task ImportedLambdasStayCharged()
    {
        var body = "f = lambda: [" + string.Join((char)44, Enumerable.Repeat("1", LargeStatements)) + "]\n";
        var host = new MockLythonHost();
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        var imports = "";
        for (var i = 0; i < 3; i++)
        {
            host.SeedFile("/m" + i + ".py", body);
            allowed.Add("m" + i);
            imports += "import m" + i + "\n";
        }

        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = new MockLythonHost();
        var allowed2 = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 3; i++)
        {
            host2.SeedFile("/m" + i + ".py", body);
            allowed2.Add("m" + i);
        }

        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task SpoofedModuleNameStillCharged()
    {
        // Import provenance never consults the guest-mutable __name__:
        // pretending to be the entry script changes nothing.
        var host = SeededHost(3, LargeStatements, out var allowed, out var imports);
        foreach (var name in allowed)
        {
            host.SeedFile("/" + name + ".py", "__name__ = '__main__'\n" + BigFunction("f" + name.Substring(1), LargeStatements));
        }

        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = SeededHost(3, LargeStatements, out var allowed2, out _);
        foreach (var name in allowed2)
        {
            host2.SeedFile("/" + name + ".py", "__name__ = '__main__'\n" + BigFunction("f" + name.Substring(1), LargeStatements));
        }

        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task OrdinarySizedBodiesStayCharged()
    {
        var host = SeededHost(3, 3000, out var allowed, out var imports);
        var script = new LythonEngine().Compile(imports + "print(3)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed };
        AssertMemoryError(script.Run(host, options));

        var host2 = SeededHost(3, 3000, out var allowed2, out _);
        AssertMemoryError(await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget, AllowedLocalModules = allowed2 }));
    }

    [Fact]
    public async Task MainScriptBodiesStayHostOwned()
    {
        // The other direction: entry-script definitions alias the reusable
        // host-owned compilation, so a big uncalled main body fits.
        var script = new LythonEngine().Compile(BigFunction("f", LargeStatements) + "return f()\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(1);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
