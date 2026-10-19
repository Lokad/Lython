using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FloatTextAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    public static IEnumerable<object[]> RefusalCases()
    {
        foreach (var bytes in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shared in new[] { false, true }) yield return [bytes, asynchronous, shared];
    }

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public async Task RefusalPrecedesDecodingAndPreservesFundedInput(bool bytes, bool asynchronous, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            const long limit = 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner});
            var governor = context.MemoryGovernor;
            var input = Input(new string('9', 32768), bytes, governor);
            context.Variables["source"] = input;
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["float"]);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
            var padding = limit - budget.ReservedBytes - 1024;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(padding, out blocker));
            else { governor.Reserve(padding, Span); governor.Commit(padding); }
            using var hostPadding = blocker;
            var committed = governor.CurrentCommittedBytes;
            LythonRuntimeException? failure = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                var pending = Invoke(callable, arguments, context, asynchronous);
                Assert.True(pending.IsCompleted, "Native float conversion must not suspend.");
                _ = await pending;
            }
            catch (LythonRuntimeException error) { failure = error; }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Same(input, context.Variables["source"]);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            hostPadding?.Dispose();
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (!measured) continue;
            Assert.NotNull(failure);
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
                failure.Attribution?.Reason);
            Assert.Same(governor.AdmissionFailure, failure);
            Assert.Same(Span, failure.Span);
            Assert.True(allocated < 16384, $"Refused float decode allocated {allocated} bytes.");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SuccessfulNormalizationReturnsScratchWithoutRefundingTheInput(bool bytes, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var input = Input(" \t" + string.Concat(Enumerable.Repeat("0_", 1024)) + "1.5\r\n", bytes, context.MemoryGovernor);
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["float"]);
        var before = budget.ReservedBytes;
        Assert.Equal(1.5, Assert.IsType<double>(await Invoke(callable, [CallArgumentValue.Positional(input)], context, asynchronous)));
        Assert.Equal(before, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        using (var otherOwner = budget.CreateScope())
        {
            var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner });
            _ = PyString.FromString(new string('x', 1024), other.MemoryGovernor, null);
            Assert.True(budget.ReservedBytes > before);
        }
        Assert.Equal(before, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public async Task ErrorRenderingRefusalRefundsScratchAndPreservesItsOrigin(bool bytes, bool asynchronous, bool shared)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner});
        var governor = context.MemoryGovernor;
        var input = Input(new string('\0', 2048) + "x", bytes, governor);
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["float"]);
        var padding = limit - budget.ReservedBytes - 16384;
        ILythonMemoryReservation? blocker = null;
        if (shared) Assert.True(budget.TryReserve(padding, out blocker));
        else { governor.Reserve(padding, Span); governor.Commit(padding); }
        using var hostPadding = blocker;
        var before = governor.CurrentCommittedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await Invoke(callable, [CallArgumentValue.Positional(input)], context, asynchronous));
        Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
            error.Attribution?.Reason);
        Assert.Same(governor.AdmissionFailure, error);
        Assert.Same(Span, error.Span);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        hostPadding?.Dispose();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public async Task RefusalPrecedesAssignmentAndUnwindsTheExecutableFrame(bool bytes, bool asynchronous, bool shared)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(
            LythonFrontend.Compile("target = float(source)\nreturn target").Script!)).EntryPoint;
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner});
        var governor = context.MemoryGovernor;
        var input = Input(new string('9', 32768), bytes, governor);
        var original = new PyList([input], governor);
        context.Variables["source"] = input;
        context.Variables["target"] = original;
        var padding = limit - budget.ReservedBytes - 32768;
        ILythonMemoryReservation? blocker = null;
        if (shared) Assert.True(budget.TryReserve(padding, out blocker));
        else { governor.Reserve(padding, Span); governor.Commit(padding); }
        using var hostPadding = blocker;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
            error.Attribution?.Reason);
        Assert.Same(governor.AdmissionFailure, error);
        Assert.Equal(new LythonSourceSpan(9, 13, 1, 10), error.Span);
        Assert.Same(original, context.Variables["target"]);
        Assert.Same(input, Assert.Single(original));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, governor.CurrentReservedBytes);
        hostPadding?.Dispose();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EscapingErrorMessageRemainsFundedAfterScratchEnds(bool bytes, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var input = Input(new string('\0', 2048) + "x", bytes, context.MemoryGovernor);
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["float"]);
        var before = budget.ReservedBytes;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await Invoke(callable, [CallArgumentValue.Positional(input)], context, asynchronous));
        Assert.Equal("ValueError", error.ExceptionType);
        Assert.True(budget.ReservedBytes - before >= 32L + 2L * error.Message.Length,
            "Retained UTF-16 error message lost its funding.");
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.MemoryGovernor.AdmissionFailure);
        var valid = Input("7.5", bytes, context.MemoryGovernor);
        Assert.Equal(7.5, Assert.IsType<double>(await Invoke(callable, [CallArgumentValue.Positional(valid)], context, asynchronous)));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OrdinaryNativeConversionPreservesResultsAndCleanup(bool bytes, bool asynchronous)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return float(value)").Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
                context.Variables["value"] = Input("7.5", bytes, context.MemoryGovernor);
                    Assert.Equal(7.5, Assert.IsType<double>(await Execute()));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                async ValueTask<object?> Execute() => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static object Input(string text, bool bytes, MemoryGovernor governor)
        => bytes ? new PyBytes(Encoding.ASCII.GetBytes(text), governor, Span) : PyString.FromString(text, governor, Span);

    private static ValueTask<object> Invoke(LythonRuntime.ICallable callable, CallArgumentValue[] arguments, Context context, bool asynchronous)
        => asynchronous ? callable.InvokeAsync(arguments, Span, context) : ValueTask.FromResult(callable.Invoke(arguments, Span, context));
}
