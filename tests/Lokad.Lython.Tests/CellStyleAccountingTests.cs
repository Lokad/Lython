using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: per-cell style entries commit per new key and release on clearing;
/// overwrites and ungoverned sheets stay free.
/// </summary>
public sealed class CellStyleAccountingTests
{
    private static LythonRuntime.OpenPyxlStyleValue NewFont()
        => new(new LythonRuntime.OpenPyxlFontStylePayload(
            PyNone.Instance, PyNone.Instance, true, PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance));

    [Fact]
    public void StyleSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        sheet.SetCellStyle(1, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        sheet.SetCellStyle(2, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        sheet.SetCellStyle(1, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellStyle(1, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, PyNone.Instance);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void UngovernedStylesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellStyle(1, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}