using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: descriptor factory products own their shell while wrapped callables
/// stay aliased; chained property derivations own each fresh shell.
/// </summary>
public sealed class DescriptorValueAccountingScenarioTests
{
    // 30k retained descriptors own 64B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix.
    private const long DescriptorBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedPropertiesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            def f(self):
                return 1
            objs = []
            i = 0
            while i < 30000:
                objs.append(property(f))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DescriptorBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= DescriptorBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= DescriptorBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedStaticMethodsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            def f(a):
                return a
            objs = []
            i = 0
            while i < 30000:
                objs.append(staticmethod(f))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DescriptorBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DescriptorsBehave()
    {
        var script = new LythonEngine().Compile("""
            class C:
                x = 5
                @staticmethod
                def sm(a):
                    return a + 1
                @classmethod
                def cm(cls, a):
                    return a + 2
                @property
                def name(self):
                    return "n"
            c = C()
            return [C.sm(1), C.cm(2), c.name]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(4), "n" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}