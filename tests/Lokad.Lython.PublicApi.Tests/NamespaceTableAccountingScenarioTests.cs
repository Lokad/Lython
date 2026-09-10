using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: argparse namespaces own one slot per member entry while keys and
/// values stay caller-owned; overwrites stay free.
/// </summary>
public sealed class NamespaceTableAccountingScenarioTests
{
    // 1500 attribute additions own 64B each, so they fit 64KiB pre-fix and
    // trip post-fix. 20k empty namespaces own a 64B shell each.
    private const long AddsBudgetBytes = 65536;
    // 20k empty namespaces add 64B shells on top of ~591KB of list backing
    // and growth transient, so they fit 1MB pre-fix and trip post-fix.
    private const long EmptyBudgetBytes = 1048576;

    private static string AddStatements()
    {
        var source = new StringBuilder("import argparse\nns = argparse.Namespace()\n");
        for (var i = 0; i < 1500; i++)
        {
            source.Append("ns.a").Append(i).Append(" = 1\n");
        }

        return source.Append("return ns.a1499\n").ToString();
    }

    [Fact]
    public async Task ManyNamespaceAttributesStayCharged()
    {
        var script = new LythonEngine().Compile(AddStatements());
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = AddsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= AddsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= AddsBudgetBytes);
    }

    [Fact]
    public async Task ManyEmptyNamespacesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            objs = []
            i = 0
            while i < 20000:
                objs.append(argparse.Namespace())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = EmptyBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task NamespaceTablesBehave()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            ns = argparse.Namespace(x=1)
            ns.y = 2
            ns.x = 10
            p = argparse.ArgumentParser(prog="p")
            p.add_argument("--foo")
            r = p.parse_args(["--foo", "bar"])
            return [ns.x, ns.y, r.foo]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(10), new BigInteger(2), "bar" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}