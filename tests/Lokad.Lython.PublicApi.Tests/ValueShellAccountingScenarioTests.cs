using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: small wrapper values own their shell beside governed contents while
/// constructor arguments stay aliased.
/// </summary>
public sealed class ValueShellAccountingScenarioTests
{
    // 20k struct_time values own a 64B shell on top of the 176B values tuple
    // and a 16B slot, so they fit 4.5MB pre-fix and trip post-fix. 20k super
    // objects own 64B plus a slot.
    private const long StructTimeBudgetBytes = 4500000;
    private const long SuperBudgetBytes = 1048576;

    [Fact]
    public async Task ManyRetainedStructTimesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import time
            objs = []
            i = 0
            while i < 20000:
                objs.append(time.gmtime(0))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StructTimeBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= StructTimeBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= StructTimeBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedSupersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            class C:
                def m(self):
                    return super()
            objs = []
            i = 0
            while i < 20000:
                objs.append(C().m())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SuperBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ValueShellsBehave()
    {
        var script = new LythonEngine().Compile("""
            import time
            st = time.gmtime(0)
            class B:
                def who(self):
                    return "b"
            class C(B):
                def who(self):
                    return super().who() + "c"
            c = C()
            return [st.tm_year, st.tm_mon, len(st), c.who()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1970), new BigInteger(1), new BigInteger(9), "bc" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}