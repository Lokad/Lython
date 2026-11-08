using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SeparatedSplitOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(19, 3, 4, 1);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ResultOwnerSurvivesSourceCleanupAndReturnsFunding(bool reverse, bool unchanged)
    {
        var budget = new LythonMemoryBudget();
        for (var batch = 0; batch < 2; batch++)
        {
            using (var resultOwner = budget.CreateScope())
            {
                var resultContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = resultOwner });
                PyList result;
                using (var sourceOwner = budget.CreateScope())
                {
                    var sourceContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = sourceOwner });
                    var input = PyString.FromString(unchanged ? "αβ" : "α<>β<><>", sourceContext.MemoryGovernor, Span);
                    var separator = PyString.FromString("<>");
                    using var ambient = PyStructuralGuard.PushAmbient(resultContext, Span);
                    result = reverse ? PyStringOps.RSplit(input, separator, -1, resultContext.MemoryGovernor, Span)
                        : PyStringOps.Split(input, separator, -1, resultContext.MemoryGovernor, Span);
                }
                var fragments = result.Cast<PyString>().ToArray();
                Assert.Equal(unchanged ? ["αβ"] : ["α", "β", "", ""], fragments.Select(value => value.AsString()).ToArray());
                Assert.All(fragments.Where(value => value.ByteLength != 0),
                    value => Assert.Same(resultContext.MemoryGovernor, value.OwnerMemoryGovernor));
                var held = budget.ReservedBytes;
                Assert.True(held > 0);
                result.Clear();
                Assert.Equal(unchanged ? "αβ" : "α", fragments[0].AsString());
                Assert.True(budget.ReservedBytes >= fragments[0].CommittedOwnedBytes);
                Assert.Equal(0, resultContext.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WholeResultRefusalPrecedesFragmentAllocation(bool reverse)
    {
        const long allowance = 1024 * 1024;
        var budget = new LythonMemoryBudget(allowance);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var input = PyString.FromString(new string('a', 65536) + "::" + new string('b', 65536), governor);
            var separator = PyString.FromString("::");
            Assert.True(budget.TryReserve(allowance - budget.ReservedBytes - 70000, out var pressure));
            using (pressure)
            using (PyStructuralGuard.PushAmbient(context, Span))
            {
                var committed = governor.CurrentCommittedBytes;
                var before = GC.GetAllocatedBytesForCurrentThread();
                var failure = Assert.Throws<LythonRuntimeException>(() => reverse
                    ? PyStringOps.RSplit(input, separator, -1, governor, Span)
                    : PyStringOps.Split(input, separator, -1, governor, Span));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
                Assert.True(allocated < 32768, $"Allocated {allocated} bytes before refusing complete output storage.");
                Assert.Equal(committed, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Equal((byte)'a', input.Utf8Span[0]);
                Assert.Equal((byte)'b', input.Utf8Span[^1]);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
