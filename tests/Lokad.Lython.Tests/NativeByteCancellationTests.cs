using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NativeByteCancellationTests
{
    [Fact]
    public async Task ActiveNativeCallObservesCancellationWithoutAnInterpreterExitCheck()
    {
        const string method="count";
        var budget=new LythonMemoryBudget(64*1024*1024);
        using(var owner=budget.CreateScope())
        using(var cancellation=new CancellationTokenSource())
        {
            var context=new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions
                { MemoryBudget=owner,CancellationToken=cancellation.Token });
            var span=new LythonSourceSpan(5,2,3,4);
            var input=new byte[16*1024*1024];Array.Fill(input,(byte)'a');
            var value=new PyBytes(input,context.MemoryGovernor,span);
            Assert.True(LythonRuntime.BytesMembers.TryGetMember(value,method,out var member));
            var callable=Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var arguments=method=="count"?new[]{CallArgumentValue.Positional(new PyBytes("aa"u8.ToArray()))}:[];
            // Warm the actual native path on a small input before opening the
            // cancellation window. The direct callable has no VM exit check.
            Assert.True(LythonRuntime.BytesMembers.TryGetMember(new PyBytes("aaaa"u8.ToArray()),method,out var warm));
            ((LythonRuntime.ICallable)warm).Invoke(arguments,span,context);
            var baseline=budget.ReservedBytes;
            var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var run=Task.Factory.StartNew(()=>
            {
                started.SetResult();
                return callable.Invoke(arguments,span,context);
            },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Delay(1);
                Assert.False(run.IsCompleted);
                cancellation.Cancel();
                var failure=await Assert.ThrowsAsync<LythonRuntimeException>(async()=>{await run.WaitAsync(TimeSpan.FromSeconds(10));});
                Assert.Equal(LythonFailureReason.Cancelled,failure.Attribution?.Reason);
                Assert.Equal(span,failure.Span);
            }
            finally
            {
                cancellation.Cancel();
                try{await run.WaitAsync(TimeSpan.FromSeconds(10));}
                catch(LythonRuntimeException failure) when(failure.Attribution?.Reason==LythonFailureReason.Cancelled){}
            }
            Assert.Equal(baseline,budget.ReservedBytes);
            Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal((byte)'a',value.Bytes[0]);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0,context.Limits.CurrentInterpreterDepth);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
