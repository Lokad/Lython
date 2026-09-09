using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: the comment table shares the fungible per-slot pool with cell
/// values; overwrites stay free and clearing releases. Comment objects stay
/// guest-owned; only the slot is charged.
/// </summary>
public sealed class CommentAccountingTests
{
    [Fact]
    public void CommentSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        sheet.SetCellComment(1, 1, new LythonRuntime.OpenPyxlComment("note", "me"));
        sheet.SetCellComment(1, 1, new LythonRuntime.OpenPyxlComment("other", "you"));
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        sheet.SetCellComment(1, 2, new LythonRuntime.OpenPyxlComment("note", "me"));
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        sheet.SetCellComment(1, 1, PyNone.Instance);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        sheet.SetCellComment(1, 2, PyNone.Instance);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void UngovernedCommentsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.SetCellComment(1, 1, new LythonRuntime.OpenPyxlComment("note", "me"));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
