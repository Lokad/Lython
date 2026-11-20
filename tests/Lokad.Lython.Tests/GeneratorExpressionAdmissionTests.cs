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
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        root.Variables["total"] = BigInteger.Zero;
        var frontend = LythonFrontend.Compile("(i for i in (7,) if (total:=i)<0)",regexProvider:Utf8RegexProvider.Instance);
        var expression = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(frontend.Script!.Statements)).Expression;
        var lowered = Assert.IsType<LoweredGeneratorExpression>(LoweredScript.LowerExpression(expression));
        _ = ExecutableScript.Compile(frontend.Script!);
        var governor = root.MemoryGovernor;
        long held = 0;
        var acquired = new PausedOuter
        {
            Acquired = () =>
            {
                // Refuse publication after the actual outer has been acquired.
                // Its pending cleanup must retain all construction funding.
                var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
                governor.Reserve(fill, lowered.Span);
                governor.Commit(fill);
                held = budget.ReservedBytes;
            }
        };
        var pending = LythonRuntime.PyGenerator.CreateExpressionAsync(lowered.Code, root, acquired,
            lowered.Span, lowered.Span, true).AsTask();
        try
        {
            await acquired.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(BigInteger.Zero, root.Variables["total"]);
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
        public Action? Acquired { get; init; }
        public object Current => BigInteger.Zero;
        public IEnumerable<object> Iterate() => new NoSynchronousTraversal();
        public IAsyncEnumerable<object> IterateAsync() => this;
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Acquired?.Invoke();
            return this;
        }
        public ValueTask<bool> MoveNextAsync() { Moves++; return new(false); }
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            Disposing.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }

        private sealed class NoSynchronousTraversal : IEnumerable<object>
        {
            public IEnumerator<object> GetEnumerator()
                => throw new InvalidOperationException("Unexpected synchronous traversal.");
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
