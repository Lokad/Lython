using System.Runtime.InteropServices;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonWindowOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedHookKeepsDecodedBackingFundedThroughLateCancellation(bool cancel)
    {
        var budget = new LythonMemoryBudget();
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var source = PyString.FromString("{\"key\":\"abcdefghijé😀\"}", context.MemoryGovernor, null);
            context.Variables["source"] = source;
            var hook = new PendingHook(source);
            context.Variables["hook"] = hook;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json.loads(source, object_hook=hook)").Script!));
            async Task<object?> Run() => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
            var pending = Run();
            try
            {
                await hook.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) cancellation.Cancel();
                Assert.False(pending.IsCompleted);
                Assert.NotNull(hook.Value);
                Assert.True(MemoryMarshal.TryGetArray(source.Utf8Bytes, out var input));
                Assert.True(MemoryMarshal.TryGetArray(hook.Value.Utf8Bytes, out var value));
                Assert.Same(input.Array, value.Array);
                Assert.True(budget.ReservedBytes >= input.Array!.Length + 32 + hook.Value.CommittedOwnedBytes);
                hook.Release.SetResult();
                if (cancel)
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending);
                    Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                }
                else Assert.IsType<PyDict>(await pending);
                Assert.Equal("abcdefghijé😀", hook.Value.AsString());
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                hook.Release.TrySetResult();
                try { await pending; } catch (LythonRuntimeException) when (cancel) { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PendingHook(PyString source) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal PyString? Value;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("The hook must genuinely suspend.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var dictionary = Assert.IsType<PyDict>(Assert.Single(arguments).Value);
            Value = Assert.IsType<PyString>(Assert.Single(dictionary.Items).Value);
            context.Variables.Remove("source");
            context.MemoryGovernor.RefundUnpublishedValue(source);
            Entered.SetResult();
            await Release.Task;
            Assert.Equal("abcdefghijé😀", Value.AsString());
            return dictionary;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void RelinquishingEveryShellKeepsTheBackingCapacityAndHeaderFunded(int initialCapacity)
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var span = new LythonSourceSpan(0, 1, 1, 1);
            PyString source;
            using (var builder = new GovernedByteBuilder(governor, span, initialCapacity))
            {
                builder.AppendString("__abcdefghijé😀__");
                source = builder.ToPyStringAndRelease();
            }
            Assert.True(MemoryMarshal.TryGetArray(source.Utf8Bytes, out var storage));
            var capacity = storage.Array!.Length;
            var first = source.CopyOrBorrowUtf8Range(2, source.ByteLength - 4, governor, span);
            var nested = first.CopyOrBorrowUtf8Range(0, first.ByteLength, governor, span);
            governor.RefundUnpublishedValue(source);
            governor.RefundUnpublishedValue(first);
            Assert.True(budget.ReservedBytes >= capacity + 32 + nested.CommittedOwnedBytes);
            Assert.Equal("abcdefghijé😀", nested.AsString());
            governor.RefundUnpublishedValue(nested);
            Assert.True(budget.ReservedBytes >= capacity + 32);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedDecodedWindowsRetainBackingAfterSourceShellRefund(bool asynchronous)
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var span = new LythonSourceSpan(0, 1, 1, 1);
            const string text = "abcdefghijé😀";
            var document = PyString.FromString("prefix-[\"" + text + "\"]-suffix", governor, span);
            Assert.True(MemoryMarshal.TryGetArray(document.Utf8Bytes, out var original));
            var input = document.CopyOrBorrowUtf8Range(7, document.ByteLength - 14, governor, span);
            context.Variables["source"] = input;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json.loads(source)").Script!));
            var decoded = Assert.IsType<PyList>(asynchronous
                ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
            var value = Assert.IsType<PyString>(Assert.Single(decoded));
            Assert.True(MemoryMarshal.TryGetArray(value.Utf8Bytes, out var window));
            Assert.Same(original.Array, window.Array);
            Assert.Equal(original.Offset + 9, window.Offset);
            Assert.Equal(text, value.AsString());
            context.Variables.Remove("source");
            governor.RefundUnpublishedValue(document);
            governor.RefundUnpublishedValue(input);
            Assert.True(governor.CurrentAccountedBytes >= original.Array!.Length + 32 + value.CommittedOwnedBytes);
            Assert.Equal(text, value.AsString());
            Assert.Equal(text.EnumerateRunes().Count(), value.Length);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void OtherOwnerInputKeepsAnIndependentCopy()
    {
        var budget = new LythonMemoryBudget();
        using (var destination = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = destination });
            PyString value;
            using (var source = budget.CreateScope())
            {
                var other = new MemoryGovernor(source);
                var input = PyString.FromString("__abcdefghijé😀__", other, null);
                value = input.CopyOrBorrowUtf8Range(2, input.ByteLength - 4, context.MemoryGovernor, new(0, 1, 1, 1));
                Assert.True(MemoryMarshal.TryGetArray(input.Utf8Bytes, out var original));
                Assert.True(MemoryMarshal.TryGetArray(value.Utf8Bytes, out var copied));
                Assert.NotSame(original.Array, copied.Array);
            }
            Assert.True(budget.ReservedBytes > 0);
            Assert.Equal("abcdefghijé😀", value.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalOrCancellationDoesNotTransferUnadmittedBacking(bool cancel)
    {
        var budget = new LythonMemoryBudget();
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
        var governor = context.MemoryGovernor;
        var span = new LythonSourceSpan(0, 1, 1, 1);
        var input = PyString.FromString("__abcdefghijé😀__", governor, span);
        var before = governor.CurrentAccountedBytes;
        var owned = input.CommittedOwnedBytes;
        if (cancel) hook.AfterGrowth = _ => cancellation.Cancel();
        else hook.RefuseGrowth = _ => true;
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        var error = Assert.Throws<LythonRuntimeException>(() => input.CopyOrBorrowUtf8Range(2, input.ByteLength - 4, governor, span));
        Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
        Assert.Equal(owned, input.CommittedOwnedBytes);
        Assert.Equal(before, governor.CurrentAccountedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        if (cancel) Assert.Null(governor.AdmissionFailure);
        else
        {
            Assert.Same(error, governor.AdmissionFailure);
            Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => input.CopyOrBorrowUtf8Range(2, input.ByteLength - 4, governor, span)));
        }
    }
}
