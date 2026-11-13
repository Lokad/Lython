using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class WordBufferLoanTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void EndingReuseKeepsActiveStorageAndRetainedWordsFunded()
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = scope });
            var governor = context.MemoryGovernor;
            var source = PyString.FromString("CAFÉ echo", governor, Span);
            using var active = PyStringOps.PrepareCasedWhitespaceIteration(source, false, context, Span);
            var first = Assert.IsType<PyString>(active.MoveNext().Value);
            using (var nested = PyStringOps.PrepareCasedWhitespaceIteration(
                PyString.FromString(string.Join(' ', Enumerable.Repeat("NESTED", 33)), governor, Span), false, context, Span))
                Assert.Equal("nested", Assert.IsType<PyString>(nested.MoveNext().Value).AsString());
            var beforeEnd = budget.ReservedBytes;
            governor.EndWorkspaceReuse();
            Assert.True(budget.ReservedBytes < beforeEnd);
            Assert.True(budget.ReservedBytes >= active.StorageBytes + first.CommittedOwnedBytes);
            Assert.Equal("echo", Assert.IsType<PyString>(active.MoveNext().Value).AsString());
            var beforeReturn = budget.ReservedBytes;
            active.Dispose();
            Assert.Equal(beforeReturn - active.StorageBytes, budget.ReservedBytes);
            active.Dispose();
            Assert.Equal(beforeReturn - active.StorageBytes, budget.ReservedBytes);
            Assert.Equal("café", first.AsString());
            using var afterEnd = PyStringOps.PrepareCasedWhitespaceIteration(source, true, context, Span);
            Assert.Equal("CAFÉ", Assert.IsType<PyString>(afterEnd.MoveNext().Value).AsString());
            Assert.Equal("café", first.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReentrantWordAdmissionKeepsOuterAndNestedLoansIndependent()
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(scope);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = admission });
            var outerText = PyString.FromString("OUTER alpha bravo café", context.MemoryGovernor, Span);
            var nestedText = PyString.FromString(string.Join(' ', Enumerable.Repeat("NESTED", 33)), context.MemoryGovernor, Span);
            PyString? retained = null;
            admission.AfterGrowth = bytes =>
            {
                if (bytes < 4 * PyString.EstimateApproximateBytes(4)) return;
                admission.AfterGrowth = null;
                using var nested = PyStringOps.PrepareCasedWhitespaceIteration(nestedText, false, context, Span);
                retained = Assert.IsType<PyString>(nested.MoveNext().Value);
            };
            using (var outer = PyStringOps.PrepareCasedWhitespaceIteration(outerText, false, context, Span))
            {
                Assert.NotNull(retained);
                Assert.Equal("outer", Assert.IsType<PyString>(outer.MoveNext().Value).AsString());
                Assert.Equal("nested", retained.AsString());
            }
            using (var reused = PyStringOps.PrepareCasedWhitespaceIteration(outerText, true, context, Span))
                Assert.Equal("OUTER", Assert.IsType<PyString>(reused.MoveNext().Value).AsString());
            Assert.Equal("nested", retained.AsString());
            context.MemoryGovernor.EndWorkspaceReuse();
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RefusalOrCancellationBeforeWordPublicationReturnsTheLoan(bool cancel)
    {
        var budget = new LythonMemoryBudget();
        using var cancellation = new CancellationTokenSource();
        using (var scope = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(scope);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = admission, CancellationToken = cancellation.Token });
            var source = PyString.FromString("alpha bravo café", context.MemoryGovernor, Span);
            using (var initial = PyStringOps.PrepareCasedWhitespaceIteration(source, false, context, Span)) { }
            var before = budget.ReservedBytes;
            if (cancel) admission.AfterGrowth = _ => cancellation.Cancel();
            else admission.RefuseGrowth = _ => true;
            var error = Assert.Throws<LythonRuntimeException>(() => PyStringOps.PrepareCasedWhitespaceIteration(source, true, context, Span));
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.MemoryGovernor.EndWorkspaceReuse();
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var scope = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = scope });
            using var words = PyStringOps.PrepareCasedWhitespaceIteration(PyString.FromString("reused"), false, context, Span);
            Assert.Equal("reused", Assert.IsType<PyString>(words.MoveNext().Value).AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FailedReentrantRetirementDoesNotDisposeTheNewBorrowerOnRetry()
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(scope);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = admission });
            var governor = context.MemoryGovernor;
            var small = PyString.FromString("nested retained", governor, Span);
            var large = PyString.FromString(string.Join(' ', Enumerable.Repeat("OUTER", 9)), governor, Span);
            var values = new LythonRuntime.ExecutableValueStorage(1, 0, 0, context, Span, 0, null, null);
            PyStringOps.CasedWhitespaceIteration? nextBorrower = null;
            try
            {
                values.PublishValue(0, 1, PyStringOps.PrepareCasedWhitespaceIteration(large, false, context, Span));
                admission.AfterShrink = _ =>
                {
                    admission.AfterShrink = null;
                    nextBorrower = PyStringOps.PrepareCasedWhitespaceIteration(small, false, context, Span);
                    throw new InvalidOperationException("refund failed after reentry");
                };
                Assert.Throws<InvalidOperationException>(() => values.RetireTo(0));
                Assert.NotNull(nextBorrower);
                var retained = budget.ReservedBytes;
                values.RetireTo(0);
                Assert.Equal(retained, budget.ReservedBytes);
                Assert.Equal("nested", Assert.IsType<PyString>(nextBorrower.MoveNext().Value).AsString());
                Assert.Equal(0, governor.CurrentReservedBytes);
            }
            finally
            {
                admission.AfterShrink = null;
                nextBorrower?.Dispose();
                values.RetireTo(0);
                values.ReleaseStorage();
                governor.EndWorkspaceReuse();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
