using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: worksheet copies duplicate every charged table, so copies accumulate
/// the source cell, style and merge pool totals. Ungoverned copies stay free.
/// </summary>
public sealed class WorksheetCopyAccountingTests
{
    private static LythonRuntime.OpenPyxlStyleValue NewFont()
        => new(new LythonRuntime.OpenPyxlFontStylePayload(
            PyNone.Instance, PyNone.Instance, true, PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance));

    private static object CallMerge(LythonRuntime.OpenPyxlWorksheet sheet, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string range)
    {
        if (!PyMemberAccess.TryResolve(sheet, "merge_cells", context, span, out var found) ||
            found is not LythonRuntime.ICallable callable)
        {
            throw new InvalidOperationException("Member merge_cells not found.");
        }

        return callable.Invoke([CallArgumentValue.Positional(PyString.FromString(range))], span, context);
    }

    [Fact]
    public void CopyDuplicatesChargedTablesExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        sheet.SetCellValue(1, 1, new BigInteger(5));
        sheet.SetCellValue(2, 1, new BigInteger(6));
        sheet.SetCellNumberFormat(1, 1, PyString.FromString("0.00"));
        sheet.SetCellHyperlink(2, 1, PyString.FromString("https://example.test/"));
        sheet.SetCellComment(2, 1, new LythonRuntime.OpenPyxlComment("n", "m"));
        sheet.SetCellStyle(1, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        sheet.SetCellStyle(2, 1, LythonRuntime.OpenPyxlCellStyleComponent.Font, NewFont());
        _ = CallMerge(sheet, context, span, "A3:A4");
        _ = CallMerge(sheet, context, span, "B3:B4");
        // Cell pool: 2 values + 1 format + 1 hyperlink + 1 comment = 5 slots;
        // style pool: 2 entries; merge pool: 2 ranges.
        Assert.Equal(9L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        var copy = sheet.Copy("C");
        Assert.Equal(2L * 9L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(new BigInteger(5), copy.GetCellValue(1, 1));
        Assert.Equal("0.00", copy.GetCellNumberFormat(1, 1));
    }

    [Fact]
    public void UngovernedCopyStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellValue(1, 1, new BigInteger(5));
        sheet.SetCellNumberFormat(1, 1, PyString.FromString("0.00"));
        _ = sheet.Copy("C");
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}