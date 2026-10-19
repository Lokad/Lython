using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class GeneratorExpressionAdmissionTests
{
    [Fact]
    public async Task DeniedScopeAdmissionAwaitsAcquiredOuterCleanupBeforeReturning()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner});
        var function = new Context(root, new ScopeDirectiveFacts(
            new HashSet<string>(), new HashSet<string>(), new HashSet<string> { "total" }));
        function.Variables["total"] = BigInteger.Zero;
        var frontend = LythonFrontend.Compile("(i for i in (7,) if (total:=i)<0)");
        var expression = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(frontend.Script!.Statements)).Expression;
        var lowered = Assert.IsType<LoweredGeneratorExpression>(LoweredScript.LowerStandaloneExpression(expression));
        var facts = lowered.ScopeFacts with
        { GlobalNames = new HashSet<string>(), NonlocalNames = new HashSet<string> { "total" } };
        var outer = PyIteration.AcquireGeneratorOuter(new PyTuple([new BigInteger(7)]), lowered.Span, function);
        var generator = new PyGeneratorExpression(lowered.Clauses, lowered.ItemExpression, function, lowered.Span, outer, facts);
        var acquired = new PausedOuter();
        generator.AttachAsyncOuter(acquired);
        var governor = root.MemoryGovernor;
        var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
        governor.Reserve(fill, lowered.Span);
        governor.Commit(fill);
        var held = budget.ReservedBytes;
        var pending = generator.TryMoveNextAsync().AsTask();
        try
        {
            await acquired.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(BigInteger.Zero, function.Variables["total"]);
            Assert.Equal(0, acquired.Moves);
        }
        finally
        {
            acquired.Release.TrySetResult();
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Same(governor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        }
        Assert.Equal(1, acquired.Disposals);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PausedOuter : IPyAsyncIterableValue, IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Moves { get; private set; }
        public int Disposals { get; private set; }
        public object Current => BigInteger.Zero;
        public IEnumerable<object> Iterate() => throw new InvalidOperationException("Unexpected synchronous traversal.");
        public IAsyncEnumerable<object> IterateAsync() => this;
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync() { Moves++; return new(false); }
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            Disposing.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }
    }
}
