using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextwrapAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(8)] // Denied output construction.
    [InlineData(150)] // Denied registry adoption after construction.
    public void DeniedDedentReleasesScratchAndFreshOutput(long cap)
    {
        var context = Context(cap);
        var input = PyString.FromString("  x\n  y");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.TextwrapModule.DedentText(input, Span, context.Services));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal("  x\n  y", input.AsString());
    }

    [Theory]
    [InlineData(4)] // Denied builder growth.
    [InlineData(150)] // Denied registry adoption after construction.
    public async Task DeniedIndentReleasesScratchAndFreshOutput(long cap)
    {
        var context = Context(cap);
        var input = PyString.FromString("a\nb");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                _ = await LythonRuntime.TextwrapModule.IndentTextAsync(input, PyString.FromString(">"),
                    PyNone.Instance, Span, context, true);
            });
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal("a\nb", input.AsString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedCallbackLineAdoptionRefundsOnlyItsFreshCharge(bool ownedInput)
    {
        var context = Context(ownedInput ? 300 : 200);
        var input = ownedInput ? PyString.FromString("a\nb", context.MemoryGovernor, Span) : PyString.FromString("a\nb");
        var callback = new CountingPredicate();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                _ = await LythonRuntime.TextwrapModule.IndentTextAsync(input, PyString.FromString(">"),
                    callback, Span, context, true);
            });
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, callback.Calls);
        Assert.Equal("a\nb", input.AsString());
        GC.KeepAlive(input);
    }

    [Fact]
    public void UnchangedDedentReturnsItsOwnedInputWithoutNewCharges()
    {
        var context = Context(512);
        var input = PyString.FromString("a😀\nb", context.MemoryGovernor, Span);
        context.Services.State.CallTemporaries.TrackFreshString(input, Span);
        context.Services.State.CallTemporaries.Sweep(full: true);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.Same(input, LythonRuntime.TextwrapModule.DedentText(input, Span, context.Services));
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedResultsReclaimExactOutputAndRegistryCharges(bool indent)
    {
        var context = Context(100000);
        var weak = Abandon(context, indent, out var charge);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(before - charge - ChargeReclamationPool.EntryChargeBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> Abandon(LythonRuntime.ExecutionContext context, bool indent, out long charge)
    {
        var result = indent
            ? (PyString)LythonRuntime.TextwrapModule.IndentTextAsync(PyString.FromString("a😀\nb"),
                PyString.FromString(">"), PyNone.Instance, Span, context, false).GetAwaiter().GetResult()
            : LythonRuntime.TextwrapModule.DedentText(PyString.FromString("  a😀\n  b"), Span, context.Services);
        charge = result.CommittedOwnedBytes;
        return new WeakReference<PyString>(result);
    }

    private sealed class CountingPredicate : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return true;
        }
    }

    private static LythonRuntime.ExecutionContext Context(long cap)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
}
