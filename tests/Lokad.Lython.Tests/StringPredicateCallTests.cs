using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StringPredicateCallTests(ITestOutputHelper output)
{
    [Fact]
    public void DistinctReceiversAvoidBoundCallableAllocation()
    {
        var code = Compile("for text in values:\n result=text.isalpha()\nreturn result");
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            context.Variables["values"] = new PyList(Enumerable.Range(0, 128).Select(index =>
                PyString.FromString("word" + new string((char)('a' + index % 26), 3), context.MemoryGovernor, null)), context.MemoryGovernor);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(Assert.IsType<bool>(result));
            Unwound(context);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured)
            {
                output.WriteLine($"PREDICATE_CALL_ALLOCATION:128:{allocated}");
                Assert.True(allocated < 8192, $"128 predicate calls allocated {allocated} bytes.");
            }
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("admission")]
    public async Task SuspendedReceiverKeepsItsOwnerAndIsNotReplayed(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var text = PyString.FromString("λambda", context.MemoryGovernor, null);
        var receiver = new Receiver(text);
        context.Variables["receive"] = receiver;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile("return receive().isalpha()"), context).AsTask();
        try
        {
            await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
                governor.Reserve(fill, null);
                governor.Commit(fill);
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null));
            }
        }
        finally
        {
            receiver.Release.TrySetResult();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route == "success") Assert.True(Assert.IsType<bool>(await pending.WaitAsync(TimeSpan.FromSeconds(5))));
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            else Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        }
        Assert.Equal(1, receiver.Calls);
        Assert.Equal("λambda", text.AsString());
        Unwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static void Unwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Receiver(PyString result) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous receiver.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return result;
        }
    }
}
