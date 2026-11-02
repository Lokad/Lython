using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class WhitespaceStorageOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(19, 3, 4, 1);

    [Fact]
    public void RefusedWholeOutputAdmissionPrecedesFragmentAllocation()
    {
        const long allowance = 1024 * 1024;
        var budget = new LythonMemoryBudget(allowance);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var input = PyString.FromString(new string('a', 65536) + " " + new string('b', 65536), governor);
            // Enough for an individual fragment, but not the complete output.
            Assert.True(budget.TryReserve(allowance - budget.ReservedBytes - 70000, out var pressure));
            using (pressure)
            {
                var committed = governor.CurrentCommittedBytes;
                var before = GC.GetAllocatedBytesForCurrentThread();
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                    PyStringOps.SplitWhitespace(input, governor, Span, context));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
                Assert.True(allocated < 32768, $"Allocated {allocated} bytes before refusing complete output storage.");
                Assert.Equal(committed, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Equal((byte)'a', input.Utf8Bytes.Span[0]);
                Assert.Equal((byte)'b', input.Utf8Bytes.Span[^1]);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpilledRangesReturnScratchWhileResultAliasesRemainFunded(bool reverse)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        for (var execution = 0; execution < 2; execution++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
                var governor = context.MemoryGovernor;
                var input = PyString.FromString(string.Join(' ', Enumerable.Repeat("α", 100)), governor);
                var baseline = governor.CurrentCommittedBytes;
                var result = reverse ? PyStringOps.RSplitWhitespace(input, 100, governor, Span, context)
                    : PyStringOps.SplitWhitespace(input, governor, Span, context);
                Assert.Equal(100, result.Count);
                var fragments = result.Cast<PyString>().ToArray();
                Assert.All(fragments, value => Assert.Equal("α", value.AsString()));
                var retained = checked(baseline + result.CommittedStorageBytes + fragments.Sum(value => value.CommittedOwnedBytes));
                Assert.Equal(retained, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                var alias = fragments[0];
                result.Clear();
                Assert.Equal("α", alias.AsString());
                // Clearing may replace and refund the list backing. Its
                // independently retained strings must keep all their funding.
                Assert.Equal(baseline + result.CommittedStorageBytes + fragments.Sum(value => value.CommittedOwnedBytes),
                    governor.CurrentCommittedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
