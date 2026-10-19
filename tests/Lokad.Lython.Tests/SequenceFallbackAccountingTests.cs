using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SequenceFallbackAccountingTests
{
    [Theory]
    [InlineData(127)]
    public void DeniedSequenceIteratorLeavesConstructionFundingUnchanged(long cap)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) });
        var sequence = new PyInstance(new PyType("Sequence", Array.Empty<PyType>(), new Dictionary<string, object>()));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => new PySequenceIterator(sequence, context, span));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }
}
