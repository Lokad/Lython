using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringAffixBoundsExecutionTests
{
    [Theory]
    [InlineData("return 'aé😀z'.startswith('é',1)")]
    [InlineData("return 'aé😀z'.startswith('é',1,3)")]
    [InlineData("return 'aé😀z'.endswith('😀',0,3)")]
    [InlineData("method='aé😀z'.endswith\nreturn method('😀',0,3)")]
    [InlineData("class Bound:\n def __index__(self):return 1\nreturn 'aé😀z'.startswith('é',Bound(),3)")]
    [InlineData("class Bound:\n @property\n def __index__(self):return lambda:3\nreturn 'aé😀z'.endswith('😀',0,Bound())")]
    public async Task NativeAndCompletedGuestBoundsCompleteWithoutSuspension(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
        Assert.True(pending.IsCompletedSuccessfully);
        Assert.Equal(true, await pending);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
