using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class EscapedJsonStorageTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ResultCopyRefusalPreservesInputAndRefundsOnlyScratch(bool asynchronous, bool shared)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var callable = Load(context);
        var governor = context.MemoryGovernor;
        var source = PyString.FromString("\"" + new string('x', 240) + "\\n\"", governor, Span);
        var before = governor.CurrentCommittedBytes;
        var padding = limit - budget.ReservedBytes - 4 * source.Utf8Bytes.Length;
        ILythonMemoryReservation? blocker = null;
        if (shared) Assert.True(budget.TryReserve(padding, out blocker));
        else { governor.Reserve(padding, Span); governor.Commit(padding); }
        using var hostPadding = blocker;
        var committed = governor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Invoke(callable, source, context, asynchronous));
        Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
            error.Attribution?.Reason);
        Assert.Equal(PyString.EstimateApproximateBytes(241), governor.LastDeniedReservationBytes);
        Assert.Same(governor.AdmissionFailure, error);
        Assert.Same(Span, error.Span);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal("\"" + new string('x', 240) + "\\n\"", source.AsString());
        Assert.True(before > 0);
        hostPadding?.Dispose();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecodedValueRetainsItsOwnerAfterScratchEnds(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var callable = Load(context);
        var source = PyString.FromString("\"a\\n\\u03bb\\ud83d\\ude00\"", context.MemoryGovernor, Span);
        var before = budget.ReservedBytes;
        var value = Assert.IsType<PyString>(await Invoke(callable, source, context, asynchronous));
        Assert.Equal("a\nλ😀", value.AsString());
        Assert.NotSame(source, value);
        Assert.True(budget.ReservedBytes - before >= PyString.EstimateApproximateBytes(value.Utf8Bytes.Length));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var held = budget.ReservedBytes;
        using (var otherOwner = budget.CreateScope())
        {
            var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner },regexProvider:Utf8RegexProvider.Instance);
            _ = PyString.FromString(new string('x', 1024), other.MemoryGovernor, Span);
            Assert.True(budget.ReservedBytes > held);
        }
        Assert.Equal(held, budget.ReservedBytes);
        Assert.Equal("a\nλ😀", value.AsString());
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRuntime.ICallable Load(Context context)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json.loads",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }

    private static ValueTask<object> Invoke(LythonRuntime.ICallable callable, PyString source, Context context, bool asynchronous)
    {
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        return asynchronous ? callable.InvokeAsync(arguments, Span, context)
            : ValueTask.FromResult(callable.Invoke(arguments, Span, context));
    }
}
