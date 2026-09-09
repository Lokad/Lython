using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: hyperlink and number-format tables share the fungible per-slot pool
/// with cell values; overwrites stay free and clearing releases.
/// </summary>
public sealed class HyperlinkFormatAccountingTests
{
    [Fact]
    public void AnnotationSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        sheet.SetCellHyperlink(1, 1, PyString.FromString("https://example.test/"));
        sheet.SetCellNumberFormat(1, 1, PyString.FromString("0.00"));
        sheet.SetCellHyperlink(1, 1, PyString.FromString("https://example.test/other"));
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellHyperlink(1, 1, PyNone.Instance);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        sheet.SetCellNumberFormat(1, 1, PyString.FromString("General"));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void UngovernedAnnotationsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellHyperlink(1, 1, PyString.FromString("https://example.test/"));
        sheet.SetCellNumberFormat(1, 1, PyString.FromString("0.00"));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}