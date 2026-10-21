using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CounterIntegerAdmissionTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);
    private static readonly ExecutableCodeObject Add = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return counts + other",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    public static IEnumerable<object[]> AdmissionCases()
    {
        foreach (var route in new[] { 0, 1, 2 })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shared in new[] { false, true })
        foreach (var room in new[] { 8192, 98304 }) yield return [route, asynchronous, shared, room];
    }

    [Theory]
    [MemberData(nameof(AdmissionCases))]
    public async Task MajorCountAdditionAdmitsBeforeAllocationAndMutation(int route, bool asynchronous, bool shared, int room)
    {
        // Warm bookkeeping in a different execution. Refused commands are never retried.
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
            var governor = context.MemoryGovernor;
            var key = PyString.FromString("x");
            var seed = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, governor, Span);
            var counts = new PyCounter(governor, Span);
            counts.SetItem(key, seed);
            if (route == 1) counts.SetItem(PyString.FromString("y"), BigInteger.One);
            var other = new PyCounter(governor, Span);
            other.SetItem(key, BigInteger.One);
            var delta = new PyDict(governor, Span);
            delta.SetItem(key, BigInteger.One);
            context.Variables["counts"] = counts;
            context.Variables["other"] = other;
            Assert.True(LythonRuntime.CounterMembers.TryGetMember(counts, route == 1 ? "total" : "update", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            CallArgumentValue[] arguments = route == 1 ? [] : [CallArgumentValue.Positional(delta)];
            var padding = 1024 * 1024 - budget.ReservedBytes - room;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(padding, out blocker));
            else { governor.Reserve(padding, Span); governor.Commit(padding); }
            using var hostPadding = blocker;
            var committed = governor.CurrentCommittedBytes;
            LythonRuntimeException? failure = null;
            object? result = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                var pending = Invoke(route, asynchronous, callable, arguments, context);
                Assert.True(pending.IsCompleted, "Controlled native Counter operations must not suspend during measurement.");
                result = await pending;
            }
            catch (LythonRuntimeException error) { failure = error; }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (measured) output.WriteLine("COUNTER_INTEGER_ADMISSION:" + JsonSerializer.Serialize(new
                { route, asynchronous, shared, room, allocated, refused = failure is not null,
                  reason = failure?.Attribution?.Reason.ToString(), committed,
                  after = governor.CurrentCommittedBytes, reserved = governor.CurrentReservedBytes }));
            if (failure is null)
            {
                Assert.True(room >= 65536, "Insufficient major-result capacity was admitted.");
                var value = route == 0 ? counts.GetCount(key) : route == 2
                    ? Assert.IsType<PyCounter>(result).GetCount(key) : result;
                Assert.Equal((BigInteger)seed + 1, Assert.IsType<BigInteger>(value));
                Assert.True(governor.CurrentCommittedBytes - committed >= 65536, "New major count is unfunded.");
            }
            else
            {
                Assert.Equal("MemoryError", failure.ExceptionType);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Equal(route == 2 ? new LythonSourceSpan(7, 14, 1, 8) : Span, failure.Span);
                Assert.Same(seed, counts.GetCount(key));
                // A private binary-result shell can remain funded until the finite owner ends.
                Assert.InRange(governor.CurrentCommittedBytes - committed, 0, 32767);
                if (measured) Assert.True(allocated < 32768, $"Refused Counter magnitude allocated {allocated} bytes.");
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
    public async Task SuccessfulAdditionKeepsOldAndNewAliasedCountsFunded(int route, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var key = PyString.FromString("x");
        var seed = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, context.MemoryGovernor, Span);
        var counts = new PyCounter(context.MemoryGovernor, Span);
        counts.SetItem(key, seed);
        if (route == 1) counts.SetItem(PyString.FromString("y"), BigInteger.One);
        var other = new PyCounter(context.MemoryGovernor, Span);
        other.SetItem(key, BigInteger.One);
        context.Variables["counts"] = counts;
        context.Variables["other"] = other;
        Assert.True(LythonRuntime.CounterMembers.TryGetMember(counts, route == 1 ? "total" : "update", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var delta = new PyDict(context.MemoryGovernor, Span);
        delta.SetItem(key, BigInteger.One);
        var baseline = budget.ReservedBytes;
        var result = await Invoke(route, asynchronous, callable,
            route == 1 ? [] : [CallArgumentValue.Positional(delta)], context);
        var updated = route == 0 ? counts.GetCount(key) : route == 2
            ? Assert.IsType<PyCounter>(result).GetCount(key) : result;
        Assert.True(budget.ReservedBytes - baseline >= 65536);
        var removedStorage = counts.CommittedStorageBytes;
        counts.Clear();
        if (result is PyCounter binary)
        {
            removedStorage += binary.CommittedStorageBytes;
            binary.Clear();
        }
        Assert.Equal((BigInteger)seed + 1, Assert.IsType<BigInteger>(updated));
        Assert.Equal(BigInteger.One << 524288, Assert.IsType<BigInteger>(seed));
        Assert.True(budget.ReservedBytes - baseline >= 65536 - removedStorage);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void BorrowedCounterIntegerUpdatesUseTheActiveOwner()
    {
        var counts = new PyCounter();
        var key = PyString.FromString("x");
        var seed = BigInteger.One << 524288;
        counts.SetItem(key, seed);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        Assert.True(LythonRuntime.CounterMembers.TryGetMember(counts, "update", out var update));
        var incoming = new PyDict();
        incoming.SetItem(key, BigInteger.One);
        ((LythonRuntime.ICallable)update).Invoke([CallArgumentValue.Positional(incoming)], Span, context);
        Assert.Equal(seed + 1, Assert.IsType<BigInteger>(counts.GetCount(key)));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 65536);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingCountsReleaseScratchWithoutFundingAPhantomMagnitude(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var key = PyString.FromString("x");
        var seed = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, context.MemoryGovernor, Span);
        var negative = LythonRuntime.OwnFreshInteger(-(BigInteger)seed, context.MemoryGovernor, Span);
        var counts = new PyCounter(context.MemoryGovernor, Span);
        counts.SetItem(key, seed);
        var delta = new PyDict(context.MemoryGovernor, Span);
        delta.SetItem(key, negative);
        Assert.True(LythonRuntime.CounterMembers.TryGetMember(counts, "update", out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var baseline = budget.ReservedBytes;
        await Invoke(0, asynchronous, callable, [CallArgumentValue.Positional(delta)], context);
        Assert.Equal(BigInteger.Zero, Assert.IsType<BigInteger>(counts.GetCount(key)));
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async ValueTask<object?> Invoke(int route, bool asynchronous,
        LythonRuntime.ICallable callable, CallArgumentValue[] arguments, Context context)
        => route == 2
            ? asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(Add, context)
                : LythonRuntime.ExecuteExecutableCodeObject(Add, context, default, null)
            : asynchronous ? await callable.InvokeAsync(arguments, Span, context) : callable.Invoke(arguments, Span, context);
}
