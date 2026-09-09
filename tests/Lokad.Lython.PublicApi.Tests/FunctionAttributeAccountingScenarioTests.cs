using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: function metadata tables own their entries, so one function with
/// many attributes cannot bypass the execution memory budget. Distinct member
/// statements isolate table growth: loop-built key strings would commit
/// renderer charges of their own.
/// </summary>
public sealed class FunctionAttributeAccountingScenarioTests
{
    [Fact]
    public async Task ManyFunctionAttributesStayCharged()
    {
        var builder = new StringBuilder("def f():\n    pass\n");
        for (var i = 0; i < 1500; i++)
        {
            builder.Append("f.a").Append(i).Append(" = None\n");
        }

        builder.Append("return 0\n");
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
    public async Task FunctionAttributeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            def f():
                pass
            f.x = 1
            f.x = 2
            return f.x
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2), asyncResult.ReturnValue);
    }
}