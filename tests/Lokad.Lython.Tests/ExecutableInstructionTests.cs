using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExecutableInstructionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void OperandProjections_RejectMismatchedDomains()
    {
        var call = ExecutableInstruction.Call(3, 4, Span);

        Assert.Equal(3, call.CallSiteIndex);
        Assert.Equal(4, call.CallCacheIndex);
        Assert.Throws<InvalidOperationException>(() => call.NameIndex);
        Assert.Throws<InvalidOperationException>(() => call.MemberCacheIndex);
    }

    [Fact]
    public void Retargeting_PreservesOnlyControlFlowOperands()
    {
        var jump = ExecutableInstruction.Jump(2, Span).WithTargetBlockIndex(7);

        Assert.Equal(7, jump.TargetBlockIndex);
        Assert.Throws<InvalidOperationException>(() => ExecutableInstruction.LoadConst(1, Span).WithTargetBlockIndex(2));
    }
}
