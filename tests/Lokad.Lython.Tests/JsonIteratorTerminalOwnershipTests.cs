using System.Collections;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonIteratorTerminalOwnershipTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EagerContinuationIsFundedBeforeInvokingDefault(bool refuse)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var callback = new DefaultValue();
        var options = LythonRuntime.JsonDumpOptions.Default with { CheckCircular = false, DefaultCallable = callback };

        Assert.True(budget.TryReserve(refuse ? limit - budget.ReservedBytes - 256 : 0, out var blocker));
        using (blocker)
        {
            var baseline = budget.ReservedBytes;
            var pending = LythonRuntime.JsonEncodeIterator.EncodeToStringAsync(new object(), options, context, span, true).AsTask();
            if (refuse)
            {
                callback.Release.TrySetResult(PyNone.Instance);
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending);
                Assert.Equal("MemoryError", failure.ExceptionType);
                Assert.False(callback.Entered);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            else
            {
                var active = budget.ReservedBytes;
                callback.Release.SetResult(PyNone.Instance);
                Assert.Equal("null", Assert.IsType<PyString>(await pending).ToString());
                Assert.True(callback.Entered);
                Assert.InRange(budget.ReservedBytes, baseline + 1, active - 1);
            }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class DefaultValue : LythonRuntime.ICallable
    {
        internal bool Entered;
        internal readonly TaskCompletionSource<object> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected asynchronous invocation.");

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Entered = true;
            return new(Release.Task);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailureRelinquishesSortedStorageBeforeRefund(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var input = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < 32; i++) input.SetItem(PyString.FromString(i.ToString("D2")), new object());
        var inputCharge = budget.ReservedBytes;
        var options = new LythonRuntime.JsonDumpOptions(false, true, true, true, null,
            PyString.FromString(", "), PyString.FromString(": "), null, true);
        var iterator = LythonRuntime.JsonEncodeIterator.Create(input, options, context, span);
        if (asynchronous) Assert.True((await iterator.TryMoveNextAsync()).HasValue);
        else Assert.True(iterator.TryMoveNext().HasValue);
        var prefix = asynchronous ? await iterator.TryMoveNextAsync() : iterator.TryMoveNext();
        Assert.True(prefix.HasValue);
        Assert.Equal("\"00\": ", Assert.IsType<PyString>(prefix.Value).ToString());
        var activeCharge = budget.ReservedBytes;
        Assert.True(activeCharge >= inputCharge + 32 * 32);
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) _ = await iterator.TryMoveNextAsync();
            else iterator.TryMoveNext();
        });
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.InRange(budget.ReservedBytes, inputCharge, activeCharge - 32 * 32);
        // A live failed iterator must not retain the private copied entries
        // whose charge has just been returned. Input aliases remain funded.
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var traversal = iterator.GetType().GetField("_traversal", fields)!.GetValue(iterator)!;
        var type = traversal.GetType();
        var nested = (ICollection)type.GetField("_nestedFrames", fields)!.GetValue(traversal)!;
        Assert.Empty(nested);
        var outer = type.GetField("_outerFrame", fields)!.GetValue(traversal)!;
        Assert.Null(outer.GetType().GetField("SortedEntries")!.GetValue(outer));
        Assert.Null(type.GetField("PendingValue", fields)!.GetValue(traversal));
        Assert.Equal(32, input.Count);
        Assert.False(iterator.TryMoveNext().HasValue);
        GC.KeepAlive(iterator);
        GC.KeepAlive(input);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
