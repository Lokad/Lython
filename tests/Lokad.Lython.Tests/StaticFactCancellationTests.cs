using System.Collections;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class StaticFactCancellationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ActiveFactInspectionStopsAndPreservesFirstRefusal(bool refuse, bool callbacks)
    {
        var budget = new LythonMemoryBudget(1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            governor.Reserve(128, null);
            governor.Commit(128);
            using var context = new StaticAnalysisContext(new ScriptSyntax([]), governor: governor,
                cancellationToken: cancellation.Token);
            LythonRuntimeException? refusal = null;
            var span = new LythonSourceSpan(1, 1, 1, 1);
            var facts = AbstractValue.Tuple(new CancellingFacts(() =>
            {
                if (refuse) refusal = governor.RefuseAdmission("test admission refused", 2048, span);
                cancellation.Cancel();
            }), span);
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (callbacks) AbstractState.IsCallbackFreeValue(facts, context);
                else AbstractState.ContainsMutableCollectionFacts(facts, context);
            });
            Assert.True(cancellation.IsCancellationRequested);
            if (refuse) Assert.Same(refusal, error);
            else Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(128, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using var reused = budget.CreateScope();
        Assert.True(reused.TryReserve(1024, out _));
    }

    // Cancel while a real fact is read; do not count cancellation checkpoints.
    private sealed class CancellingFacts(Action cancel) : IReadOnlyList<AbstractValue>
    {
        private Action? _cancel = cancel;
        public int Count => 2;
        public AbstractValue this[int index]
        {
            get
            {
                var action = Interlocked.Exchange(ref _cancel, null)
                    ?? throw new InvalidOperationException("Fact inspection continued after cancellation.");
                action();
                return AbstractValue.Unknown();
            }
        }
        public IEnumerator<AbstractValue> GetEnumerator()
        {
            for (var index = 0; index < Count; index++) yield return this[index];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
