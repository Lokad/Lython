using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: named-style application owns its entries: the name slot plus every
/// applied format and component entry commit, and clearing the name releases
/// its slot. Applied entries persist like the values they mirror.
/// Ungoverned sheets stay free.
/// </summary>
public sealed class NamedStyleAccountingTests
{
    private static object CreateNamedStyle()
    {
        var method = typeof(LythonRuntime).GetMethod("CreateNamedStyle", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateNamedStyle not found.");
        object?[] args = [PyString.FromString("M"), PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance, PyString.FromString("0.00"), PyNone.Instance];
        return method.Invoke(null, [args, null, null])
            ?? throw new InvalidOperationException("CreateNamedStyle returned null.");
    }

    [Fact]
    public void NamedStyleSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        sheet.SetCellNamedStyle(1, 1, PyString.FromString("Weird"));
        sheet.SetCellNamedStyle(1, 1, PyString.FromString("Weird"));
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        sheet.SetCellNamedStyle(1, 2, PyString.FromString("Weird"));
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellNamedStyle(1, 1, PyString.FromString("Normal"));
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        sheet.SetCellNamedStyle(1, 2, PyString.FromString("Normal"));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void AppliedEntriesCommitAndPersist()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        var style = CreateNamedStyle();
        sheet.SetCellNamedStyle(1, 1, style);
        // Name slot plus applied number-format entry; components are empty.
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellNamedStyle(1, 1, PyString.FromString("Normal"));
        // Clearing the name releases its slot; the applied format persists.
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal("0.00", sheet.GetCellNumberFormat(1, 1));
    }

    [Fact]
    public void UngovernedNamedStylesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellNamedStyle(1, 1, CreateNamedStyle());
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}