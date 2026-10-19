using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PropertyAdmissionRollbackTests
{
    [Fact]
    public void DescriptorAdmissionDenialLeavesStorageAndReservationsUnchanged()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) },regexProvider:Utf8RegexProvider.Instance);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        using (governor.ReserveTemporary(65536 - governor.CurrentAccountedBytes - PyProperty.ShellBytes + 1, span))
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(before, governor.CurrentCommittedBytes);
    }
}
