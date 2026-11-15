using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class FormatStorageTests
{
    [Fact]
    public void PreparedLayoutRetainsItsExecutionOwner()
    {
        var budget = new LythonMemoryBudget(64 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var template = PyString.FromString("{0:>4}");
            var baseline = budget.ReservedBytes;
            var layout = PyStringOps.PrepareFormatTemplate(template, governor, null, default);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            Assert.Same(governor, layout.Owner);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void LayoutAdmissionRefusesBeforeRetainingStorage()
    {
        var budget = new LythonMemoryBudget(1);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var failure = Assert.Throws<LythonRuntimeException>(() => PyStringOps.PrepareFormatTemplate(
            PyString.FromString("{value:>4}"), governor, null, default));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Same(failure, governor.AdmissionFailure);
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

}
