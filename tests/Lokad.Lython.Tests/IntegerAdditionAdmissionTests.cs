using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class IntegerAdditionAdmissionTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan SumSpan = new(2, 3, 4, 5);
    private static readonly Func<object[], LythonSourceSpan, Context, object> Sum = typeof(LythonRuntime)
        .GetMethod("Sum", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, object>>();
    private static readonly Func<object[], LythonSourceSpan, Context, ValueTask<object>> SumAsync = typeof(LythonRuntime)
        .GetMethod("SumAsync", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, ValueTask<object>>>();
    private static readonly ExecutableCodeObject Add = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return left + right").Script!)).EntryPoint;

    public static IEnumerable<object[]> Denials()
    {
        foreach (var route in new[] { 0, 1, 2, 3 })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shared in new[] { false, true })
        foreach (var room in new[] { 8192, 98304 }) yield return [route, asynchronous, shared, room];
    }

    [Theory]
    [MemberData(nameof(Denials))]
    public async Task MajorAdditionAdmitsOrRefusesBeforeConstructingTheResult(int route, bool asynchronous, bool shared, int room)
    {
        // Warm exception/async bookkeeping in a separate execution. Never reuse
        // a failed governor: refusal is terminal for that command. A middle-band
        // allowance may legitimately succeed with a more precise future estimate.
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MaxExecutionMemoryBytes = shared ? null : 1024 * 1024, MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var start = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, governor, SumSpan);
            context.Variables["left"] = start;
            context.Variables["right"] = route == 2 ? (object)1L : BigInteger.One;
            object[] arguments = [route == 3 ? new PyBytes([1]) : new PyBytes([1]).Iterate(), start];
            var padding = 1024 * 1024 - budget.ReservedBytes - room;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(padding, out blocker));
            else
            {
                governor.Reserve(padding, SumSpan);
                governor.Commit(padding);
            }
            using var hostPadding = blocker;
            var baseline = budget.ReservedBytes;
            var committed = governor.CurrentCommittedBytes;
            LythonRuntimeException? failure = null;
            object? result = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                var pending = Invoke(route, asynchronous, arguments, context);
                Assert.True(pending.IsCompleted, "Controlled native addition must not suspend during allocation measurement.");
                result = await pending;
                if (result is LythonRuntime.NativeIterableOperation operation)
                {
                    result = operation.Result;
                    operation.Dispose();
                }
            }
            catch (LythonRuntimeException error) { failure = error; }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (measured) output.WriteLine("INTEGER_ADDITION_ADMISSION:" + JsonSerializer.Serialize(new
                { route, asynchronous, shared, room, allocated, refused=failure is not null,
                  requested=governor.LastDeniedReservationBytes, reason=failure?.Attribution?.Reason.ToString(),
                  committed, after=governor.CurrentCommittedBytes, reserved=governor.CurrentReservedBytes }));
            if (failure is null)
            {
                Assert.True(room >= 65536, "Insufficient result capacity was admitted.");
                Assert.Equal((BigInteger)start + 1, Assert.IsType<BigInteger>(result));
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Null(governor.AdmissionFailure);
            }
            else
            {
                Assert.Equal("MemoryError", failure.ExceptionType);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit,
                    failure.Attribution?.Reason);
                Assert.Equal(route is 0 or 3 ? SumSpan : new LythonSourceSpan(7, 12, 1, 8), failure.Span);
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Equal(committed, governor.CurrentCommittedBytes);
                if (measured) Assert.True(allocated < 32768, $"Refused magnitude allocated {allocated} bytes.");
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            hostPadding?.Dispose();
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task AdmittedResultRemainsFundedAfterTemporaryStorageEnds(int route, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var start = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, context.MemoryGovernor, SumSpan);
        context.Variables["left"] = start;
        context.Variables["right"] = route == 2 ? (object)1L : BigInteger.One;
        object[] arguments = [route == 3 ? new PyBytes([1]) : new PyBytes([1]).Iterate(), start];
        var baseline = budget.ReservedBytes;
        var result = await Invoke(route, asynchronous, arguments, context);
        if (result is LythonRuntime.NativeIterableOperation operation)
        {
            result = operation.Result;
            operation.Dispose();
        }
        Assert.Equal((BigInteger)start + 1, Assert.IsType<BigInteger>(result));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes > baseline);
        Assert.Null(context.MemoryGovernor.AdmissionFailure);
        // The result and all aliases have had their last use before owner ends.
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingMajorOperandsReturnScratchWithoutFundingAPhantomMagnitude(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var left = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, context.MemoryGovernor, SumSpan);
        var right = LythonRuntime.OwnFreshInteger(-(BigInteger)left, context.MemoryGovernor, SumSpan);
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        var baseline = budget.ReservedBytes;
        Assert.Equal(BigInteger.Zero, Assert.IsType<BigInteger>(await Invoke(1, asynchronous, [], context)));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async ValueTask<object?> Invoke(int route, bool asynchronous, object[] arguments, Context context)
        => route is 0 or 3
            ? asynchronous ? await SumAsync(arguments, SumSpan, context) : Sum(arguments, SumSpan, context)
            : asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(Add, context)
                : LythonRuntime.ExecuteExecutableCodeObject(Add, context, default, null);
}
