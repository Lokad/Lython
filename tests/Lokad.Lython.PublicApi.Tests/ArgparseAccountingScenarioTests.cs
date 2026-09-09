using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: argparse parser registries charge per registration, so parsers with
/// many specs cannot bypass the execution memory budget. Re-registering one
/// option string isolates registry growth: loop-built names would commit
/// renderer charges of their own.
/// </summary>
public sealed class ArgparseAccountingScenarioTests
{
    [Fact]
    public async Task ManySpecsStayCharged()
    {
        var builder = new StringBuilder("import argparse\np = argparse.ArgumentParser(add_help=False)\n");
        builder.Append("i = 0\nwhile i < 1000:\n    p.add_argument(\"--x\")\n    i = i + 1\nreturn 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyGroupsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import argparse
            p = argparse.ArgumentParser(add_help=False)
            i = 0
            while i < 1000:
                p.add_mutually_exclusive_group()
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyDefaultsStayCharged()
    {
        // 1500 distinct keywords stay baked names; only the table slots are new.
        var builder = new StringBuilder("import argparse\np = argparse.ArgumentParser(add_help=False)\np.set_defaults(");
        for (var i = 0; i < 1500; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append("k").Append(i).Append("=None");
        }

        builder.Append(")\nreturn 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ParserOptionsAndNamespaceStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import argparse
            p = argparse.ArgumentParser(add_help=False)
            p.add_argument("--name")
            p.add_argument("pos")
            g = p.add_mutually_exclusive_group()
            g.add_argument("--fast", action="store_true")
            p.set_defaults(name="dflt")
            ns = p.parse_args(["hello", "--fast"])
            return [ns.name, ns.pos, ns.fast]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "dflt", "hello", true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}