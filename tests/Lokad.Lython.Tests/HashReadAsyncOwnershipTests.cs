using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class HashReadAsyncOwnershipTests
{
    [Theory]
    [InlineData("hash((key,))", "success")]
    [InlineData("mapping.get((key,))", "success")]
    [InlineData("(key,) in members", "success")]
    [InlineData("hash((key,))", "cancel")]
    [InlineData("mapping.get((key,))", "cancel")]
    [InlineData("(key,) in members", "cancel")]
    [InlineData("hash((key,))", "admission")]
    [InlineData("mapping.get((key,))", "admission")]
    [InlineData("(key,) in members", "admission")]
    [InlineData("hash(row)", "success")]
    [InlineData("hash(row)", "cancel")]
    [InlineData("hash(row)", "admission")]
    public async Task PendingHashesStayFundedAndUnwindFirstFailure(string expression, string route)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var callback = new HashCallback();
        context.Variables["gethash"] = callback;
        var setup = """
            armed=False
            class Key:
             def __hash__(self):
              if armed:return gethash()
              return 17
            key=Key()
            mapping={(key,):'retained'}
            members={(key,)}
            effects=[]
            """;
        if (expression == "hash(row)") setup += """

            from dataclasses import dataclass
            @dataclass(unsafe_hash=True)
            class Row:
             value:object
             def __getattribute__(self,name):
              if armed and name=='value':return gethash()
              return object.__getattribute__(self,name)
            row=Row(key)
            """;
        LythonRuntime.ExecuteExecutableCodeObject(Compile(setup), context, default, null);
        var mapping = Assert.IsType<PyDict>(context.Variables["mapping"]);
        var members = Assert.IsType<PySet>(context.Variables["members"]);
        var code = Compile("armed=True\ntry:\n return " + expression + "\nexcept BaseException:effects.append('caught')\nfinally:return 'hidden'\n");
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        LythonRuntimeException? refusal = null;
        try
        {
            var first = await Task.WhenAny(callback.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(callback.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(context.State.StructuralTraversal.Depth > 0);
            Assert.True(budget.ReservedBytes > 0);
            Assert.Equal(1, mapping.Count);
            Assert.Equal(1, members.Count);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
                refusal = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(budget.LimitBytes!.Value, origin));
            Assert.False(pending.IsCompleted);
            Assert.False(callback.Finished);
        }
        finally
        {
            callback.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route != "success") { }
        }
        if (route == "success") Assert.Equal("hidden", Assert.IsType<Lokad.Lython.Runtime.Text.PyString>(await pending).AsString());
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            if (refusal is not null)
            {
                Assert.Same(refusal, failure);
                Assert.Same(origin, failure.Span);
            }
        }
        Assert.True(callback.Finished);
        Assert.Equal(1, callback.Calls);
        Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
        Assert.Equal(1, mapping.Count);
        Assert.Equal(1, members.Count);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Empty(context.State.StructuralTraversal.ActivePath!);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        using (var reused = new LythonEngine().Compile("print(7)", budget))
        using (var result = await reused.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("7\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private sealed class HashCallback : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous hash callback.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Null(PyStructuralGuard.AmbientContext);
            using var storage = context.MemoryGovernor.ReserveTemporary(128, span);
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return new BigInteger(17); }
            finally { Finished = true; }
        }
    }
}
