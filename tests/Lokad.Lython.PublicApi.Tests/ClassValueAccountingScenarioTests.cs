using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed class objects own a base plus per-member slots. Empty
/// classes accumulate the base; attribute-heavy classes accumulate the table.
/// </summary>
public sealed class ClassValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyEmptyClassesStayCharged()
    {
        var builder = new StringBuilder("cs = []\ni = 0\nwhile i < 2000:\n    class C:\n        pass\n    cs.append(C)\n    i = i + 1\nreturn 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyMemberClassesStayCharged()
    {
        var builder = new StringBuilder("cs = []\ni = 0\nwhile i < 500:\n    class C:\n");
        for (var k = 0; k < 20; k++)
        {
            builder.Append("        a").Append(k).Append(" = ").Append(k).Append("\n");
        }

        builder.Append("    cs.append(C)\n    i = i + 1\nreturn 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ClassBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            class C:
                x = 1
                def m(self):
                    return 2
            return [C.x, C().m()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}