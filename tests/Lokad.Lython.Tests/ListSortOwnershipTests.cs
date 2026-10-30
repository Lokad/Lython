using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ListSortOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void RefusalBeforeDetachingLeavesTheReceiverAndItsFundingUnchanged()
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var list = new PyList([new BigInteger(2), BigInteger.One], context.MemoryGovernor, Span);
        Assert.True(LythonRuntime.ListMembers.TryGetMember(list, "sort", out var value));
        Assert.True(owner.TryReserve(root.LimitBytes!.Value - root.ReservedBytes, out var pad));
        using (pad)
        {
            var before = root.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => Assert.IsAssignableFrom<LythonRuntime.ICallable>(value).Invoke([], Span, context));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(new object[] { new BigInteger(2), BigInteger.One }, list);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingKeysKeepOriginalStorageOwnedAndRestoreItOnCancellationOrRefusal(bool refuse)
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var list = new PyList([new BigInteger(2), BigInteger.One], context.MemoryGovernor, Span);
        var originalCharge = list.CommittedStorageBytes;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var key = new Key(async value =>
        {
            Assert.Empty(list);
            entered.TrySetResult();
            await resume.Task;
            if (refuse) context.MemoryGovernor.Reserve(root.LimitBytes!.Value, Span);
            context.CheckExecution(Span);
            return value;
        });
        Assert.True(LythonRuntime.ListMembers.TryGetMember(list, "sort", out var method));
        var pending = Assert.IsAssignableFrom<LythonRuntime.ICallable>(method).InvokeAsync([CallArgumentValue.Keyword("key", key)], Span, context).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(root.ReservedBytes > originalCharge);
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= originalCharge + list.CommittedStorageBytes);
            if (!refuse) cancellation.Cancel();
            Assert.False(pending.IsCompleted); // Cleanup must await the actual user.
        }
        finally { resume.TrySetResult(); }
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
        Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(new object[] { new BigInteger(2), BigInteger.One }, list);
        Assert.Equal(originalCharge, list.CommittedStorageBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class Key(Func<object, ValueTask<object>> key) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => throw new InvalidOperationException("The key must be awaited.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => key(arguments[0].Value);
    }
}
