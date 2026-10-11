using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IntegerFromBytesOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(12, 4, 2, 3);

    private static LythonRuntime.ICallable Conversion(LythonRuntime.ExecutionContext context)
    {
        Assert.True(context.TryGetBuiltin("int", out var type));
        Assert.True(PyMemberAccess.TryResolve(type!, "from_bytes", context, Span, out var method));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(method);
    }

    private static CallArgumentValue[] Arguments(int length, string order, bool signed) =>
    [
        CallArgumentValue.Positional(new PyBytes(Enumerable.Repeat((byte)0xfe, length).ToArray())),
        CallArgumentValue.Positional(PyString.FromString(order)),
        CallArgumentValue.Keyword("signed", signed),
    ];

    [Theory]
    [InlineData("little", false)]
    [InlineData("little", true)]
    [InlineData("big", false)]
    [InlineData("big", true)]
    public void TheReturnedBoxOwnsItsHeapPayload(string order, bool signed)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var method = Conversion(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var value = method.Invoke(Arguments(512, order, signed), Span, context);
        var integer = Assert.IsType<BigInteger>(value);
        Assert.True(context.Services.State.CallTemporaries.IsTracked(value));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes - before >= integer.GetByteCount() + 32);
        Assert.Same(value, LythonRuntime.OwnFreshInteger(value, context.MemoryGovernor, context.Services.State.CallTemporaries, Span));
        GC.KeepAlive(value);
    }

    [Fact]
    public void OversizedConversionDeniesBeforeCommittingPayload()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });
        var method = Conversion(context);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => method.Invoke(Arguments(10000, "little", false), Span, context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(Span, error.Span);
        Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedTrackingRefundsOnlyTheFreshPayload()
    {
        const long budget = 4096;
        const long payload = 32 + 513; // Unsigned 512 octets with a sign byte.
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        var method = Conversion(context);
        var governor = context.MemoryGovernor;
        var padding = budget - governor.CurrentAccountedBytes - payload;
        governor.Reserve(padding, Span);
        governor.Commit(padding);
        var before = governor.CurrentCommittedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => method.Invoke(Arguments(512, "little", false), Span, context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, context.Services.State.CallTemporaries.Count);
    }
}
