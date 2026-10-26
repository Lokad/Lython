using System.Runtime.CompilerServices;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context=Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class IteratorAdapterFrameLifetimeTests
{
    [Theory]
    [InlineData("filter(None,[1])",false)]
    [InlineData("filter(None,[1])",true)]
    [InlineData("map(int,[1])",false)]
    [InlineData("map(int,[1])",true)]
    [InlineData("itertools.chain([1])",false)]
    [InlineData("itertools.chain([1])",true)]
    public async Task MirroredGuestLocalsDoNotEscapeThroughNativeAdapters(string expression,bool asynchronous)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using(var owner=budget.CreateScope())
        {
            var capture=new Capture();
            var result=await Create(expression,owner,capture,asynchronous);
            Assert.True(capture.Mirrored);
            // Holding the actual invocation frame reproduces retention of its
            // mirrored local. Releasing that control must leave only real users.
            Collect();
            Assert.True(capture.Payload!.IsAlive);
            capture.HeldFrame=null;
            Collect();
            Assert.False(capture.Frame!.IsAlive);
            Assert.False(capture.Payload.IsAlive);
            var iterator=Assert.IsAssignableFrom<IPyAsyncIteratorValue>(result);
            var value=await iterator.TryMoveNextAsync();
            Assert.Equal(BigInteger.One,value.Value);
            GC.KeepAlive(result);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<object> Create(string expression,ILythonMemoryScope owner,Capture capture,bool asynchronous)
    {
        var source="import itertools\ndef make():\n unrelated='x'*8192\n capture(unrelated)\n class Local:pass\n return "+expression+"\nreturn make()";
        var code=ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var context=new Context(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner});
        context.Variables["capture"]=capture;
        return (asynchronous?await LythonRuntime.ExecuteExecutableCodeObjectAsync(code,context,default,null)
            :LythonRuntime.ExecuteExecutableCodeObject(code,context,default,null))!;
    }

    private sealed class Capture:LythonRuntime.ICallable
    {
        internal WeakReference? Frame,Payload;
        internal Context? HeldFrame;
        internal bool Mirrored;
        public object Invoke(CallArgumentValue[] args,LythonSourceSpan span,Context context)
        {
            Frame=new(context);Payload=new(args[0].Value);HeldFrame=context;
            Mirrored=context.MirrorsExecutableLocals;
            return PyNone.Instance;
        }
    }
    private static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
}
