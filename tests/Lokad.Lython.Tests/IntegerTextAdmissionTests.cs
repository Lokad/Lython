using System.Numerics;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class IntegerTextAdmissionTests(ITestOutputHelper output)
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
    public async Task RefusedConversionAdmitsBeforeDecodingFundedInput(bool bytes, bool asynchronous, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            const long limit = 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, MaxExecutionMemoryBytes = shared ? null : limit });
            var governor = context.MemoryGovernor;
            var input = Input(new string('9', 32768), bytes, governor);
            context.Variables["source"] = input;
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["int"]);
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
                Assert.True(pending.IsCompleted, "Native integer parsing must not suspend.");
                _ = await pending;
            }
            catch (LythonRuntimeException error) { failure = error; }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (measured) output.WriteLine("INTEGER_TEXT_ADMISSION:" + JsonSerializer.Serialize(new
                { bytes, asynchronous, shared, allocated, committed, after = governor.CurrentCommittedBytes }));
            Assert.NotNull(failure);
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit,
                failure.Attribution?.Reason);
            Assert.Same(governor.AdmissionFailure, failure);
            Assert.Same(Span, failure.Span);
            Assert.Same(input, context.Variables["source"]);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            hostPadding?.Dispose();
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured) Assert.True(allocated < 16384, $"Refused integer decode allocated {allocated} bytes.");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ParsedMajorResultAndInputRemainFundedAfterScratchEnds(bool bytes, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var governor = context.MemoryGovernor;
        var input = Input(" \t-0x_" + new string('f', 512) + "\r\n", bytes, governor);
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["int"]);
        var before = budget.ReservedBytes;
        var result = await Invoke(callable, [CallArgumentValue.Positional(input), CallArgumentValue.Positional(new BigInteger(16))],
            context, asynchronous);
        Assert.Equal(-((BigInteger.One << 2048) - 1), Assert.IsType<BigInteger>(result));
        Assert.True(budget.ReservedBytes - before >= 256, "Retained integer magnitude lost its funding.");
        Assert.Equal(0, governor.CurrentReservedBytes);
        var held = budget.ReservedBytes;
        using (var otherOwner = budget.CreateScope())
        {
            var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner });
            _ = PyString.FromString(new string('x', 1024), other.MemoryGovernor, Span);
            Assert.True(budget.ReservedBytes > held);
        }
        Assert.Equal(held, budget.ReservedBytes);
        Assert.Equal(-((BigInteger.One << 2048) - 1), Assert.IsType<BigInteger>(result));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Input(string text, bool bytes, MemoryGovernor governor)
        => bytes ? new PyBytes(Encoding.ASCII.GetBytes(text), governor, Span) : PyString.FromString(text, governor, Span);

    private static ValueTask<object> Invoke(LythonRuntime.ICallable callable, CallArgumentValue[] arguments,
        Context context, bool asynchronous)
        => asynchronous ? callable.InvokeAsync(arguments, Span, context) : ValueTask.FromResult(callable.Invoke(arguments, Span, context));
}
