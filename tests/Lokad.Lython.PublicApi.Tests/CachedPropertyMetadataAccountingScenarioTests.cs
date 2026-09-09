using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG14: cached_property descriptors own their metadata tables, so one
/// descriptor with many attributes cannot bypass the execution memory budget.
/// Distinct member statements isolate table growth: loop-built key strings
/// would commit renderer charges of their own.
/// </summary>
public sealed class CachedPropertyMetadataAccountingScenarioTests
{
    [Fact]
    public async Task ManyDescriptorAttributesStayCharged()
    {
        var builder = new StringBuilder("import functools\nclass C:\n    @functools.cached_property\n    def p(self):\n        return 1\ncp = C.p\n");
        for (var i = 0; i < 1500; i++)
        {
            builder.Append("cp.a").Append(i).Append(" = None\n");
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
    public async Task DescriptorAttributeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            class C:
                @functools.cached_property
                def p(self):
                    return 1
            cp = C.p
            cp.x = 1
            cp.x = 2
            return [cp.x, C().p]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}