using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class OrdinaryFunctionTransportTests(ITestOutputHelper output)
{
    public static TheoryData<int, bool> Calls => new()
    { { 0, false }, { 0, true }, { 1, false }, { 1, true }, { 2, false }, { 2, true }, { 3, false }, { 3, true } };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task OrdinaryCallsKeepEveryGuardAndBodyEffect(int shape, bool asynchronous)
    {
        var (definitionSource, callSource) = shape switch
        {
            0 => ("def f(value,offset=1):\n stamp(value)\n return value", "return f(value,offset=2)"),
            1 => ("def f(value,*,offset=1):\n stamp(value)\n return value", "return f(value)"),
            2 => ("class Box:\n def f(self,value,*,offset=1):\n  stamp(value)\n  return value\nf=Box().f", "return f(value,offset=2)"),
            _ => ("def f(value,*args,offset=1,**kwargs):\n stamp(value)\n return value", "return f(value,3,tag=4)")
        };
        var definition = Compile(definitionSource);
        var call = Compile(callSource);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        long steps;
        string[] effects;
        using (var owner = budget.CreateScope())
        {
            var (context, value, tape) = await Prepare(owner);
            Assert.Same(value, await Run(call, context));
            steps = context.Limits.ExecutionStepCount;
            effects = tape.Events.ToArray();
            Assert.Single(effects);
            Check(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.InRange(steps, 1, 64);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var (context, _, tape) = await Prepare(owner);
                context.Limits.ExecutionStepCount = 10000 - remaining;
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(call, context));
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
                Assert.Equal(10001, context.Limits.ExecutionStepCount);
                var s = failure.Span;
                checkpoints.Add((s is null ? "-" : $"{s.Start},{s.Length},{s.Line},{s.Column}") + ":" + string.Join(';', tape.Events));
                Check(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        string[] expected = shape switch
        {
            0 => ["-:", "7,1,1,8:", "9,5,1,10:", "22,1,1,23:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "24,5,2,2:", "30,5,2,8:", "24,12,2,2:", "24,12,2,2:", "24,12,2,2:", "24,12,2,2:body:4,1,0", "45,5,3,9:body:4,1,0", "38,12,3,2:body:4,1,0", "0,24,1,1:body:4,1,0"],
            1 => ["-:", "7,1,1,8:", "9,5,1,10:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "26,5,2,2:", "32,5,2,8:", "26,12,2,2:", "26,12,2,2:", "26,12,2,2:", "26,12,2,2:body:4,1,0", "47,5,3,9:body:4,1,0", "40,12,3,2:body:4,1,0", "0,15,1,1:body:4,1,0"],
            2 => ["-:", "7,1,1,8:", "9,5,1,10:", "22,1,1,23:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "7,17,1,8:", "44,12,3,3:", "44,5,3,3:", "44,12,3,3:", "44,12,3,3:", "50,5,3,9:", "66,5,4,10:body:3,1,0", "0,24,1,1:body:3,1,0"],
            _ => ["-:", "7,1,1,8:", "9,5,1,10:", "15,1,1,16:", "21,1,1,22:", "7,16,1,8:", "7,16,1,8:", "7,16,1,8:", "7,16,1,8:", "7,16,1,8:", "7,16,1,8:", "39,5,2,2:", "45,5,2,8:", "39,12,2,2:", "39,12,2,2:", "39,12,2,2:", "39,12,2,2:body:4,1,0", "60,5,3,9:body:4,1,0", "53,12,3,2:body:4,1,0", "0,23,1,1:body:4,1,0"],
        };
        Assert.Equal(expected, checkpoints);

        output.WriteLine("ORDINARY_FUNCTION_GUARDS:" + JsonSerializer.Serialize(new { shape, asynchronous, steps, effects, checkpoints }));

        async ValueTask<(Context, PyString, Effects)> Prepare(ILythonMemoryBudget owner)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, MaxExecutionSteps = 10000 });
            var value = PyString.FromString("owned", context.MemoryGovernor, null);
            var tape = new Effects(value);
            context.Variables["value"] = value;
            context.Variables["stamp"] = tape;
            await Run(definition, context);
            context.Limits.ExecutionStepCount = 0;
            return (context, value, tape);
        }
        async ValueTask<object?> Run(ExecutableCodeObject code, Context context) => asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "admission")]
    [InlineData(true, "admission")]
    public async Task KeywordDefaultProtocolsKeepOwnedInputsUntilCompletion(bool bound, string route)
    {
        const string key = "class Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):\n  tap()\n  return other=='value'\n";
        var definitionSource = key + (bound
            ? "class Box:\n def method(self,arg,*,value):body();return value\nBox.method.__kwdefaults__={Key():payload}\nf=Box().method"
            : "def f(arg,*,value):body();return value\nf.__kwdefaults__={Key():payload}");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var argument = PyString.FromString("argument", context.MemoryGovernor, null);
        var payload = PyString.FromString("payload", context.MemoryGovernor, null);
        var tap = new SuspendedDefault();
        var effects = new List<string>();
        context.Variables["argument"] = argument;
        context.Variables["payload"] = payload;
        context.Variables["tap"] = tap;
        context.Variables["body"] = new Body(effects);
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile(definitionSource), context);
        var baseline = budget.ReservedBytes;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile("return f(arg=argument)"), context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        try
        {
            var first = await Task.WhenAny(tap.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(tap.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.False(tap.Finished);
            Assert.Empty(effects);
            Assert.True(budget.ReservedBytes >= baseline);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(
                    governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes + 1, origin));
            }
        }
        finally
        {
            tap.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route != "success") { }
        }
        if (route == "success")
        {
            Assert.Same(payload, await pending);
            Assert.Single(effects);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.ExecutionMemoryLimit,
                failure.Attribution?.Reason);
            if (route == "admission")
            {
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Same(origin, failure.Span);
            }
            Assert.Empty(effects);
        }
        Assert.True(tap.Finished);
        Assert.Equal(1, tap.Calls);
        Assert.Equal("argument", argument.AsString());
        Assert.Equal("payload", payload.AsString());
        Check(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Body(List<string> events) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { events.Add("body"); return PyNone.Instance; }
    }

    private sealed class SuspendedDefault : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Expected an asynchronous default-key protocol.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return PyNone.Instance; }
            finally { Finished = true; }
        }
    }

    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Effects(object expected) : LythonRuntime.ICallable
    {
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Same(expected, Assert.Single(arguments).Value);
            Events.Add($"body:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth},{context.MemoryGovernor.CurrentReservedBytes}");
            return PyNone.Instance;
        }
    }
}
