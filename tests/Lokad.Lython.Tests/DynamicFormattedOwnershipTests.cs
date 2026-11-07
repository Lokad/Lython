using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.Tests;

public sealed class DynamicFormattedOwnershipTests
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task DynamicFieldAwaitsProtocolCleanupBeforePublishingOrRefunding(bool cancel)
    {
        var frontend = LythonFrontend.Compile("""
global published
def render(value):
    return f'prefix:{value:>{width}}:suffix'
published = render(value)
effects.append(published)
""");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var formatter = new SuspendedFormat();
            Assert.True(context.TryGetBuiltin("object", out var root));
            var value = new PyInstance(new PyType("Value", [Assert.IsType<PyType>(root)],
                new Dictionary<string, object> { ["__format__"] = formatter }), context.MemoryGovernor, null);
            var original = PyString.FromString("kept", context.MemoryGovernor, null);
            var effects = new PyList(0, context.MemoryGovernor, null);
            context.Variables["value"] = value;
            context.Variables["width"] = new BigInteger(4);
            context.Variables["published"] = original;
            context.Variables["effects"] = effects;
            var baseline = budget.ReservedBytes;
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
            try
            {
                await formatter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Same(original, context.Variables["published"]);
                Assert.Empty(effects);
                if (cancel) cancellation.Cancel();
                else formatter.Release.SetResult();
                await formatter.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Same(original, context.Variables["published"]);
                Assert.Empty(effects);
                formatter.CleanupRelease.SetResult();
                if (cancel)
                {
                    var error = await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
                    if (error is LythonRuntimeException runtime)
                        Assert.Equal(LythonFailureReason.Cancelled, runtime.Attribution?.Reason);
                    else Assert.Equal(cancellation.Token,
                        Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                    Assert.Same(original, context.Variables["published"]);
                    Assert.Empty(effects);
                }
                else
                {
                    await pending;
                    Assert.Equal("prefix:rendered:suffix", Assert.IsType<PyString>(Assert.Single(effects)).AsString());
                }
                Assert.Same(value, context.Variables["value"]);
                Assert.Equal(1, formatter.Calls);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Null(context.CurrentExecutableFrame);
            }
            finally
            {
                formatter.Release.TrySetResult();
                formatter.CleanupRelease.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception error) when (error is OperationCanceledException or LythonRuntimeException) { }
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class SuspendedFormat : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CleanupEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("The asynchronous format callback was invoked synchronously.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Assert.Equal("render", context.FunctionName);
            Assert.Equal(">4", Assert.IsType<PyString>(Assert.Single(arguments).Value).AsString());
            Entered.SetResult();
            try
            {
                await Release.Task.WaitAsync(context.State.CancellationToken);
                return PyString.FromString("rendered", context.MemoryGovernor, span);
            }
            finally
            {
                CleanupEntered.SetResult();
                await CleanupRelease.Task;
            }
        }
    }

    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task DynamicSpecKeepsAliasesAndStopsPublicationOnFundingFailure(bool asynchronous, int failureKind)
    {
        var frontend = LythonFrontend.Compile("""
def render(value, width):
    return f'prefix:{value:>{width}}:suffix'

def through(value, width):
    result = render(value, width)
    return result

effects.append(through(input, 30000))
""");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var origin = new LythonSourceSpan(120, 5, 12, 3);
            var input = PyString.FromString("é😀", context.MemoryGovernor, origin);
            var effects = new PyList(0, context.MemoryGovernor, origin);
            context.Variables["input"] = input;
            context.Variables["effects"] = effects;
            var fundedInput = budget.ReservedBytes;
            var reached = false;
            LythonRuntimeException? first = null;
            hook.AfterGrowth = bytes =>
            {
                if (bytes < 30000) return;
                hook.AfterGrowth = null;
                reached = true;
                Assert.True(budget.ReservedBytes >= fundedInput + 30000);
                if (failureKind == 1) cancellation.Cancel();
                if (failureKind == 2)
                    first = context.MemoryGovernor.RefuseAdmission("controlled dynamic-format admission refusal", 777, origin);
            };
            try
            {
                async Task Execute()
                {
                    if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                    else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                }
                if (failureKind == 0)
                {
                    await Execute();
                    Assert.Equal("prefix:" + new string(' ', 29998) + "é😀:suffix",
                        Assert.IsType<PyString>(Assert.Single(effects)).AsString());
                }
                else
                {
                    var error = await Assert.ThrowsAnyAsync<Exception>(Execute);
                    Assert.Empty(effects);
                    if (failureKind == 1)
                    {
                        // Internal builtin entry points can propagate the token's
                        // exception; the public run boundary normalizes it.
                        if (error is LythonRuntimeException runtime)
                            Assert.Equal(LythonFailureReason.Cancelled, runtime.Attribution?.Reason);
                        else Assert.Equal(cancellation.Token,
                            Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
                    }
                    else
                    {
                        var refusal = Assert.IsType<LythonRuntimeException>(error);
                        Assert.Same(first, refusal);
                        Assert.Equal(origin, refusal.Span);
                        Assert.Equal(777L, refusal.Attribution?.Requested);
                    }
                }
                Assert.True(reached);
                Assert.Equal("é😀", input.AsString());
                Assert.True(budget.ReservedBytes >= fundedInput);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value, out var reusable));
        reusable.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
