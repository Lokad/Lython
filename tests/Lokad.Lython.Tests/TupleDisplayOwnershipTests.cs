using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class TupleDisplayOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedExpansionKeepsScratchFundedAndAwaitsUnwind(bool cancel)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var sequence = new SuspendedSequence(cancellation.Token);
        context.Variables["items"] = sequence;
        var execution = Evaluate("async", context).AsTask();
        try
        {
            await sequence.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 96);
            Assert.True(budget.ReservedBytes >= before + 96);
            if (cancel) cancellation.Cancel();
            sequence.Resume.TrySetResult();
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            else Assert.Same(PyNone.Instance, Assert.IsType<PyTuple>(await execution)[0]);
            Assert.True(sequence.Disposed);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (cancel) Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        finally
        {
            cancellation.Cancel();
            sequence.Resume.TrySetResult();
            try { await execution; } catch (OperationCanceledException) { }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("syntax")]
    [InlineData("lowered")]
    [InlineData("async")]
    public async Task ExpansionRemainsFundedUntilFinalStorageTakesOwnership(string route)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var sequence = new ObservedSequence(33, count =>
        {
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 32 + 16L * count);
        });
        context.Variables["items"] = sequence;
        var tuple = Assert.IsType<PyTuple>(await Evaluate(route, context));
        Assert.Equal(33, tuple.Count);
        foreach (var item in tuple) Assert.Same(sequence.Shared, item);
        Assert.True(sequence.Disposed);
        Assert.Equal(before + tuple.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("syntax", 5, 200, 4)]
    [InlineData("lowered", 5, 200, 4)]
    [InlineData("async", 5, 200, 4)]
    [InlineData("syntax", 2, 150, 2)]
    [InlineData("lowered", 2, 150, 2)]
    [InlineData("async", 2, 150, 2)]
    public async Task RefusedGrowthOrFinalCopyRefundsScratchAndDisposesSource(
        string route, int count, long available, int accepted)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var sequence = new ObservedSequence(count, _ => { });
        context.Variables["items"] = sequence;
        Assert.True(budget.TryReserve(limit - budget.ReservedBytes - available, out var padding));
        using (padding)
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Evaluate(route, context));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(accepted, sequence.Accepted);
            Assert.True(sequence.Disposed);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ValueTask<object> Evaluate(string route, Context context)
    {
        var script = LythonFrontend.Compile("(*items,)").Script!;
        var expression = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(script.Statements)).Expression;
        var lowered = LoweredScript.LowerStandaloneExpression(expression);
        return route switch
        {
            "syntax" => ValueTask.FromResult(typeof(LythonRuntime)
                .GetMethod("EvaluateExpression", BindingFlags.NonPublic | BindingFlags.Static)!
                .CreateDelegate<Func<ExpressionSyntax, Context, object>>()(expression, context)),
            "lowered" => ValueTask.FromResult(LythonRuntime.EvaluateLoweredExpression(lowered, context)),
            _ => LythonRuntime.EvaluateLoweredExpressionAsync(lowered, context)
        };
    }

    private sealed class ObservedSequence(int count, Action<int> afterPull) : IPyIterableValue
    {
        internal object Shared { get; } = new object();
        internal int Accepted;
        internal bool Disposed;
        public IEnumerable<object> Iterate()
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    yield return Shared;
                    afterPull(++Accepted);
                }
            }
            finally { Disposed = true; }
        }
    }

    private sealed class SuspendedSequence(CancellationToken cancellationToken) : IPyAsyncIterableValue
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed;
        public IEnumerable<object> Iterate()
        {
            yield return PyNone.Instance;
            throw new InvalidOperationException("Expected asynchronous iteration.");
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            try
            {
                yield return PyNone.Instance;
                Entered.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            finally { Disposed = true; }
        }
    }
}
