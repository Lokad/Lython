using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExactBytesSumTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);
    private static readonly Func<object[], LythonSourceSpan, Context, object> Sum = typeof(LythonRuntime)
        .GetMethod("Sum", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, object>>();
    private static readonly Func<object[], LythonSourceSpan, Context, ValueTask<object>> SumAsync = typeof(LythonRuntime)
        .GetMethod("SumAsync", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, ValueTask<object>>>();

    public static IEnumerable<object[]> Checkpoints()
    {
        foreach (var length in new[] { 0, 1, 63, 64, 65, 127, 128 })
        foreach (var steps in new[] { 0, 1, 2, 3 })
        foreach (var asynchronous in new[] { false, true }) yield return [length, steps, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Checkpoints))]
    public void FastAndGenericIterationKeepFuelErrorsAndTerminalPull(int length, int steps, bool asynchronous)
    {
        var bytes = new PyBytes(Enumerable.Range(0, length).Select(i => (byte)i).ToArray());
        var direct = Context(steps);
        var generic = Context(steps);
        Assert.Equal(Observe(bytes.Iterate(), generic, asynchronous), Observe(bytes, direct, asynchronous));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SummingManyBytesDoesNotAllocatePerItemIntegers(bool asynchronous)
    {
        var context = Context();
        object[] arguments = [new PyBytes(Enumerable.Repeat((byte)255, 8192).ToArray()), BigInteger.Zero];
        _ = Invoke(arguments, context, asynchronous);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Invoke(arguments, context, asynchronous);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(new BigInteger(255 * 8192), result);
        Assert.True(allocated < 4096, $"Byte sum allocated {allocated} bytes.");
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MagnitudeAdmissionFailsBeforeResultAllocation(bool asynchronous)
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var start = LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, governor, Span);
        object[] arguments = [new PyBytes([1]), start];
        var padding = 1024 * 1024 - governor.CurrentAccountedBytes - 8192;
        governor.Reserve(padding, Span);
        governor.Commit(padding);
        var committed = governor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var failure = Assert.Throws<LythonRuntimeException>(() => Invoke(arguments, context, asynchronous));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(Span, failure.Span);
            Assert.True(allocated < 32768, $"Refused magnitude allocated {allocated} bytes.");
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledByteSumLeavesNoWorkingReservation(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { CancellationToken = cancellation.Token });
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() => Invoke([new PyBytes([1, 2]), BigInteger.Zero], context, asynchronous));
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static Context Context(int? steps = null) => new(new MockLythonHost(),
        new LythonRunOptions { MaxExecutionSteps = steps, MaxExecutionMemoryBytes = 1024 * 1024 });

    private static object Invoke(object[] arguments, Context context, bool asynchronous)
        => asynchronous ? SumAsync(arguments, Span, context).GetAwaiter().GetResult() : Sum(arguments, Span, context);

    private static (object? Result, LythonFailureReason? Reason, LythonSourceSpan? Span, long Steps) Observe(object source, Context context, bool asynchronous)
    {
        try
        {
            var result = Invoke([source, BigInteger.Zero], context, asynchronous);
            return (result, null, null, context.State.Limits.ExecutionStepCount);
        }
        catch (LythonRuntimeException error)
        {
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            return (null, error.Attribution?.Reason, error.Span, context.State.Limits.ExecutionStepCount);
        }
    }
}
