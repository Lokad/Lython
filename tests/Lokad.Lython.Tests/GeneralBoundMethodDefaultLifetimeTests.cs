using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GeneralBoundMethodDefaultLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolDefaultsKeepTheReceiverOwnedBeforeBodyEntry(bool cancel)
    {
        const string source = "class Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):\n  tap()\n  return other=='value'\nclass Box:\n def method(self,*,value):return value\nbox=Box()\nBox.method.__kwdefaults__={Key():value}\nmethod=box.method";
        var definition = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var call = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return method()").Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var value = PyString.FromString("owned", context.MemoryGovernor, null);
        context.Variables["value"] = value;
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(definition, context);
        var receiver = context.Variables["box"];
        var storage = new object[2];
        var tap = new DefaultTap(receiver, storage);
        context.Variables["tap"] = tap;
        var baseline = budget.ReservedBytes;
        var field = typeof(LythonRuntime).GetField("_pooledTwoBoundValues", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = field.GetValue(null);
        Task<object?> pending;
        field.SetValue(null, storage);
        try { pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(call, context).AsTask(); }
        finally { field.SetValue(null, previous); }
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Same(receiver, storage[0]);
            Assert.Null(storage[1]); // Keyword default is still being resolved.
            Assert.True(budget.ReservedBytes >= baseline);
            if (cancel)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                tap.Release.TrySetResult();
                Assert.Same(value, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.All(storage, Assert.Null);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes >= baseline);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class DefaultTap(object receiver, object[] storage) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected a genuinely suspended default lookup.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Same(receiver, storage[0]);
            Assert.Null(storage[1]);
            Assert.Null(context.CurrentExecutableFrame);
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return PyNone.Instance;
        }
    }
}
