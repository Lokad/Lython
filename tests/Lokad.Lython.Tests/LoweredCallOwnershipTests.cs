using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LoweredCallOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WidePacketStaysFundedUntilTheCalleeFinishes(bool expanded, bool asynchronous)
    {
        const int count = 64;
        var source = "return consume(" + (expanded ? "*items" : string.Join(", ", Enumerable.Repeat("None", count))) + ")";
        var script = LoweredScript.Lower(LythonFrontend.Compile(source).Script!);
        var expression = Assert.IsType<LoweredReturnStatement>(Assert.Single(script.Statements)).Expression!;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var callee = new ObservedCallable(count, budget);
            context.Variables["consume"] = callee;
            context.Variables["items"] = new PyTuple(Enumerable.Repeat<object>(PyNone.Instance, count), context.MemoryGovernor);
            var baseline = budget.ReservedBytes;

            if (asynchronous)
            {
                var pending = LythonRuntime.EvaluateLoweredExpressionAsync(expression, context).AsTask();
                try
                {
                    Assert.True(callee.Entered.Task.IsCompletedSuccessfully);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes - baseline >= IntPtr.Size * count);
                }
                finally
                {
                    callee.Resume.TrySetResult();
                    Assert.Same(PyNone.Instance, await pending);
                }
            }
            else Assert.Same(PyNone.Instance, LythonRuntime.EvaluateLoweredExpression(expression, context));

            Assert.True(await callee.Entered.Task - baseline >= IntPtr.Size * count);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalForAWideDirectPacketPrecedesArgumentEffects(bool asynchronous)
    {
        const int count = 64;
        const long limit = 4 * 1024 * 1024;
        var source = "return consume(effect(), " + string.Join(", ", Enumerable.Repeat("None", count - 1)) + ")";
        var script = LoweredScript.Lower(LythonFrontend.Compile(source).Script!);
        var expression = Assert.IsType<LoweredReturnStatement>(Assert.Single(script.Statements)).Expression!;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var callee = new ObservedCallable(count, budget);
            var effect = new ObservedCallable(0, budget);
            context.Variables["consume"] = callee;
            context.Variables["effect"] = effect;
            Assert.True(owner.TryReserve(limit - budget.ReservedBytes - 128, out var occupied));
            using (occupied)
            {
                var before = budget.ReservedBytes;
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(() => LythonRuntime.EvaluateLoweredExpressionAsync(expression, context).AsTask())
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.EvaluateLoweredExpression(expression, context));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.False(effect.Entered.Task.IsCompleted);
                Assert.False(callee.Entered.Task.IsCompleted);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
            }
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ObservedCallable(int count, LythonMemoryBudget budget) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource<long> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Equal(count, arguments.Length);
            Entered.SetResult(budget.ReservedBytes);
            return PyNone.Instance;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Invoke(arguments, span, context);
            await Resume.Task.ConfigureAwait(false);
            Assert.Same(PyNone.Instance, arguments[^1].Value);
            return PyNone.Instance;
        }
    }
}
