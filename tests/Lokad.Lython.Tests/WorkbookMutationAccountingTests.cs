using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: guest-mutated cells commit table slots per new address and release on
/// clearing; cell wrappers behave the same. Loaded cells stay outside this
/// accounting.
/// </summary>
public sealed class WorkbookMutationAccountingTests
{
    private static LythonRuntime.OpenPyxlWorksheet NewSheet(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        return sheet;
    }

    [Fact]
    public void CellSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = NewSheet(context, span);
        sheet.SetCellValue(1, 1, new BigInteger(5));
        sheet.SetCellValue(2, 1, new BigInteger(6));
        sheet.SetCellValue(1, 1, new BigInteger(7));
        // Cell-object wrappers stay outside this accounting.
        _ = sheet.GetCellObject(1, 1);
        _ = sheet.GetCellObject(2, 1);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellValue(1, 1, PyNone.Instance);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(new BigInteger(6), sheet.GetCellValue(2, 1));
    }

    [Fact]
    public void UngovernedCellsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellValue(1, 1, new BigInteger(5));
        _ = sheet.GetCellObject(1, 1);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}