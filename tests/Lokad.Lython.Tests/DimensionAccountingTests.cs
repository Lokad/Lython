using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: row and column dimension entries commit per new index; repeat access
/// and ungoverned sheets stay free.
/// </summary>
public sealed class DimensionAccountingTests
{
    private static LythonRuntime.OpenPyxlWorksheet NewSheet(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        return sheet;
    }

    [Fact]
    public void RepeatedDimensionsReuseAlreadyAdmittedStorage()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = NewSheet(context, span);
        _ = sheet.GetOrCreateRowDimension(1);
        _ = sheet.GetOrCreateRowDimension(2);
        var beforeRepeat = context.MemoryGovernor.CurrentCommittedBytes;
        _ = sheet.GetOrCreateRowDimension(1);
        Assert.Equal(beforeRepeat, context.MemoryGovernor.CurrentCommittedBytes);
        _ = sheet.GetOrCreateColumnDimension(1);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > beforeRepeat);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedDimensionsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        _ = sheet.GetRowDimension(1);
        _ = sheet.GetColumnDimension(1);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}