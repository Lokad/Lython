using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// A completed join owns its retained backing, including unused capacity.
/// Its input drain and unpublished working storage end before return.
/// </summary>
public sealed class StringJoinAccountingTests
{
    [Fact]
    public void JoinKeepsRetainedBackingFundedAndReturnsWorkingStorage()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var span = new LythonSourceSpan(0, 0, 0, 0);
            var parts = new PyList(Enumerable.Range(0, 100).Select(static _ => (object)PyString.FromString("abcdefghij")));
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            var result = Assert.IsType<PyString>(LythonRuntime.StringMembers.Join(PyString.FromString(","), parts, span, context));

            Assert.Equal(string.Join(",", Enumerable.Repeat("abcdefghij", 100)), result.AsString());
            Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
            Assert.True(result.CommittedOwnedBytes >= PyString.EstimateApproximateBytes(result.ByteLength));
            Assert.Equal(result.CommittedOwnedBytes, context.MemoryGovernor.CurrentCommittedBytes - before);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
