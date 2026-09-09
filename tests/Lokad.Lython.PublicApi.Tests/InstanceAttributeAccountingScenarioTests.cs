using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: instance attribute tables charge per entry, so one object with many
/// attributes cannot bypass the execution memory budget. Overwrites stay free
/// and deletion releases. Distinct member statements isolate table growth:
/// loop-built key strings would commit renderer charges of their own.
/// </summary>
public sealed class InstanceAttributeAccountingScenarioTests
{
    [Fact]
    public async Task ManyAttributesStayCharged()
    {
        // 1500 distinct slots at 64B each exceed a 64KiB budget; the None
        // values and baked member names commit nothing of their own.
        var builder = new StringBuilder("class A:\n    pass\na = A()\n");
        for (var i = 0; i < 1500; i++)
        {
            builder.Append("a.k").Append(i).Append(" = None\n");
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
    public async Task AttributeOverwriteAndDeleteBehave()
    {
        var script = new LythonEngine().Compile(
            """
            class A:
                pass
            a = A()
            a.x = 1
            a.x = 2
            a.y = 3
            del a.y
            return [a.x, hasattr(a, "y")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}