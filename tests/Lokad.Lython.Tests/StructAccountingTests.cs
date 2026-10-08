using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StructAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static LythonRuntime.ICallable Callable(string name)
    {
        Assert.True(LythonRuntime.StructModule.Instance.TryGetMember(name, out var value));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
    }

    private static CallArgumentValue[] Arguments(params object[] values)
        => values.Select(CallArgumentValue.Positional).ToArray();

    [Fact]
    public async Task PackedOutputRemainsFundedAcrossAConversionAwaitAndRefundsOnCancellation()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token, MaxExecutionMemoryBytes = 10000 });
        var hook = new SuspendedIndex();
        var number = new PyInstance(new PyType("Number", [], new() { ["__index__"] = hook }));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var pending = Callable("pack").InvokeAsync(Arguments(PyString.FromString(">1024xI"), number), Span, context).AsTask();
        await hook.Started.Task.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 1060);
        cancellation.Cancel();
        Assert.Equal("execution canceled", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).Message);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class SuspendedIndex : LythonRuntime.ICallable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected asynchronous conversion.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Started.TrySetResult();
            try { await new TaskCompletionSource().Task.WaitAsync(context.Limits.CancellationToken); }
            catch (OperationCanceledException) { context.CheckExecution(span); throw; }
            return BigInteger.One;
        }
    }

    [Fact]
    public void HugePaddingDeniesBeforeAllocationAndRefunds()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        foreach (var size in new[] { "1000000", "2147483648" })
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Callable("pack").Invoke(
                    Arguments(PyString.FromString("<" + size + "x")), Span, context));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
    }

    [Fact]
    public void RangeFailureRefundsThePrivatePackedOutput()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => Callable("pack").Invoke(
                Arguments(PyString.FromString(">2I"), BigInteger.One, BigInteger.MinusOne), Span, context));
            Assert.Equal(PythonExceptionIdentity.Module("struct", "error"), error.Identity);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(128)]
    [InlineData(200)]
    [InlineData(330)]
    public void TupleCouponAndRegistryDenialsRefund(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => Callable("unpack").Invoke(
                Arguments(PyString.FromString(">2d"), new PyBytes(new byte[16])), Span, context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void IteratorDenialRefundsAndDoesNotDecodeRecords()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 200 });
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => Callable("iter_unpack").Invoke(
                Arguments(PyString.FromString(">I"), new PyBytes(new byte[400])), Span, context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void LazyRecordDenialLeavesTheCursorRetryable()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        var iterator = Assert.IsAssignableFrom<IPyIteratorValue>(Callable("iter_unpack").Invoke(
            Arguments(PyString.FromString(">20d"), new PyBytes(new byte[160])), Span, context));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var backingBefore = context.State.CallTemporaries.CommittedBackingBytes;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext(out _)).ExceptionType);
            Assert.Equal(before + context.State.CallTemporaries.CommittedBackingBytes - backingBefore,
                context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        var hint = Assert.IsAssignableFrom<IPyDynamicAttributes>(iterator);
        Assert.True(hint.TryGetMember("__length_hint__", out var member));
        Assert.Equal(BigInteger.One, Assert.IsAssignableFrom<LythonRuntime.ICallable>(member).Invoke([], Span, context));
    }
}
