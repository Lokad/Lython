using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PropertyRegistrationOriginalAccountingTests
{
    [Fact]
    public void RegistrationDenialRefundsTheUnpublishedDescriptor()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var span = new LythonSourceSpan(0, 0, 0, 0);
        using (governor.ReserveTemporary(65536 - governor.CurrentAccountedBytes - PyProperty.ShellBytes, span))
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
