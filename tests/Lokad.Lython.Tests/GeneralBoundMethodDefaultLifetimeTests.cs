using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GeneralBoundMethodDefaultLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolDefaultsKeepTheReceiverOwnedBeforeBodyEntry(bool cancel)
    {
        const string source = "class Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):\n  tap()\n  return other=='value'\nclass Box:\n def method(self,*,value):return self,value\nbox=Box()\nBox.method.__kwdefaults__={Key():value}\nmethod=box.method";
        var definition = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var call = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return method()",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var value = PyString.FromString("owned", context.MemoryGovernor, null);
        context.Variables["value"] = value;
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(definition, context);
        var receiver = context.Variables["box"];
        var baseline = budget.ReservedBytes;
        var tap = new DefaultTap(budget, baseline);
        context.Variables["tap"] = tap;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(call, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            if (cancel)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                tap.Release.TrySetResult();
                var result = Assert.IsType<PyTuple>(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Same(receiver, result[0]);
                Assert.Same(value, result[1]);
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes >= baseline);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NamedDefaultsKeepArgumentsFundedThroughSuspensionAndFailure(int failureKind)
    {
        const string source = """
class Key:
 def __hash__(self):return hash('value')
 def __eq__(self,other):
  tap()
  return other=='value'
def target(first,*,value):return first,value
target.__kwdefaults__={Key():value}
return target(first=value)
""";
        var definition = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var value = PyString.FromString(new string('x', 2048), context.MemoryGovernor, null);
        context.Variables["value"] = value;
        var baseline = budget.ReservedBytes;
        var tap = new DefaultTap(budget, baseline);
        context.Variables["tap"] = tap;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(definition, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            if (failureKind == 1)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else if (failureKind == 2)
            {
                var origin = new LythonSourceSpan(99, 3, 8, 5);
                var first = context.MemoryGovernor.RefuseAdmission("controlled prepared default refusal", 777, origin);
                tap.Release.TrySetResult();
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Same(first, error);
                Assert.Equal(origin, error.Span);
                Assert.Equal(777L, error.Attribution?.Requested);
            }
            else
            {
                tap.Release.TrySetResult();
                var result = Assert.IsType<PyTuple>(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Same(value, result[0]);
                Assert.Same(value, result[1]);
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
            catch (LythonRuntimeException) when (failureKind == 2) { }
            context.State.CloseResources();
        }
        Assert.Equal(new string('x', 2048), value.AsString());
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes >= baseline);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class DefaultTap(LythonMemoryBudget budget, long baseline) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected a genuinely suspended default lookup.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.True(budget.ReservedBytes > baseline);
            Assert.NotNull(context.CurrentExecutableFrame);
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return PyNone.Instance;
        }
    }
}
