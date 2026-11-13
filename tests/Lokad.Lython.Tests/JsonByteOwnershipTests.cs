using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonByteOwnershipTests
{
    private static readonly Func<object, Context, LythonSourceSpan, PyString> Input = typeof(LythonRuntime)
        .GetNestedType("JsonModule", BindingFlags.NonPublic)!
        .GetMethod("RequireLoadsText", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<object, Context, LythonSourceSpan, PyString>>();

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BorrowedImmutablePayloadRemainsFundedAfterPrivateSourceCleanup(bool bom)
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = scope });
            var governor = context.MemoryGovernor;
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var payload = Encoding.UTF8.GetBytes((bom ? "\uFEFF" : "") + "[\"abcdefghijé😀\"]");
            var source = new PyBytes(payload, governor, span);
            using var original = GovernedHostBytes.FromValue(source);
            var before = budget.ReservedBytes;
            var document = Input(source, context, span);
            Assert.True(MemoryMarshal.TryGetArray(document.Utf8Bytes, out var window));
            Assert.Same(payload, window.Array);
            Assert.Equal(bom ? 3 : 0, window.Offset);
            Assert.Equal(before + PyString.EstimateApproximateBytes(0), budget.ReservedBytes);
            original.Dispose();
            governor.RefundUnpublishedValue(document);
            Assert.True(budget.ReservedBytes >= PyBytes.EstimateApproximateBytes(payload.Length));
            Assert.Equal("[\"abcdefghijé😀\"]", document.AsString());
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RefusalOrCancellationPrecedesWindowPublication(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var scope = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(scope);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = admission, CancellationToken = cancellation.Token });
            var source = new PyBytes(Encoding.UTF8.GetBytes("[\"abcdefghijé😀\"]"), context.MemoryGovernor);
            var before = budget.ReservedBytes;
            if (cancel) admission.AfterGrowth = _ => cancellation.Cancel();
            else admission.RefuseGrowth = _ => true;
            var failure = Assert.Throws<LythonRuntimeException>(() => Input(source, context, new(0, 1, 1, 1)));
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(PyBytes.EstimateApproximateBytes(source.Length), source.CommittedStorageBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")] [InlineData("cancel")] [InlineData("refuse")]
    public async Task PendingHookRetainsBorrowedBytesUntilItsActualUsersFinish(string outcome)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = admission, CancellationToken = cancellation.Token });
            var payload = Encoding.UTF8.GetBytes("{\"key\":\"abcdefghijé😀\"}");
            var source = new PyBytes(payload, context.MemoryGovernor);
            using var original = GovernedHostBytes.FromValue(source);
            var hook = new PendingHook(original);
            context.Variables["source"] = source;
            context.Variables["hook"] = hook;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json.loads(source,object_hook=hook)").Script!));
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
            try
            {
                await hook.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.NotNull(hook.Value);
                Assert.True(MemoryMarshal.TryGetArray(hook.Value.Utf8Bytes, out var window));
                Assert.Same(payload, window.Array);
                Assert.True(budget.ReservedBytes >= PyBytes.EstimateApproximateBytes(payload.Length) + hook.Value.CommittedOwnedBytes);
                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") admission.RefuseGrowth = _ => true;
                Assert.False(pending.IsCompleted);
                hook.Release.SetResult(outcome == "refuse");
                if (outcome == "success") Assert.IsType<PyDict>(await pending);
                else
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                }
                Assert.Equal("abcdefghijé😀", hook.Value.AsString());
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                hook.Release.TrySetResult(false);
                try { await pending; } catch (LythonRuntimeException) when (outcome != "success") { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PendingHook(GovernedHostBytes source) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal PyString? Value;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The hook must genuinely suspend.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var value = Assert.IsType<PyDict>(Assert.Single(arguments).Value);
            Value = Assert.IsType<PyString>(Assert.Single(value.Items).Value);
            context.Variables.Remove("source");
            source.Dispose();
            Entered.SetResult();
            if (await Release.Task) context.MemoryGovernor.Reserve(4096, span);
            return value;
        }
    }
}
