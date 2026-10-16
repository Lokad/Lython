using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class UserItemGetDispatchTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BindingAndInvocationUseTheSelectedMode(bool asynchronous, bool descriptor)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        var key = new object();
        context.Variables["checkpoint"] = checkpoint;
        context.Variables["key"] = key;
        var source = descriptor
            ? "class Box:\n @property\n def __getitem__(self):\n  checkpoint()\n  def get(key):\n   checkpoint()\n   return key\n  return get\nbox=Box()\nresult=box[key]"
            : "class Box:\n def __getitem__(self,key):\n  checkpoint()\n  return key\nbox=Box()\nresult=box[key]";
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        var calls = descriptor ? 2 : 1;
        Assert.Equal(asynchronous ? 0 : calls, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? calls : 0, checkpoint.AsyncCalls);
        Assert.Same(key, context.Variables["result"]);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Checkpoint : LythonRuntime.ICallable
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            SyncCalls++;
            return BigInteger.One;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            AsyncCalls++;
            await Task.Yield();
            return BigInteger.One;
        }
    }
}
