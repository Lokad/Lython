using System.Reflection;
using System.Numerics;
using System.Text.Json;
using Xunit.Abstractions;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PositionalCallTransportTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    [InlineData(3, true, true)]
    public async Task MatchingFrameUsesInvocationOwnedBoundStorage(int width, bool asynchronous, bool keywords)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var arguments = string.Join(",", Enumerable.Range(0, width).Select(i => keywords ? $"a{i}=value" : "value"));
        var frontend = LythonFrontend.Compile($"def f({parameters}):\n    a0=value\n    tap()\n    return a0\nreturn f({arguments})\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        var storage = new object[width];
        var tap = new StorageTap(storage);
        context.Variables["value"] = marker;
        context.Variables["tap"] = tap;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneBoundValue" : width == 2 ? "_pooledTwoBoundValues" : "_pooledThreeBoundValues",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        field.SetValue(null, storage);
        try
        {
            var result = asynchronous
                ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context)
                : LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, null);
            Assert.Same(marker, result);
            Assert.True(tap.Invoked);
            Assert.All(storage, value => Assert.Null(value));
            Assert.Null(tap.Frame!.CurrentExecutableFrame);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }

    private sealed class StorageTap(object[] storage) : LythonRuntime.ICallable
    {
        internal bool Invoked;
        internal LythonRuntime.ExecutionContext? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Invoked = true;
            Frame = context;
            var field = typeof(LythonRuntime.ExecutableFrameState).GetField("_locals", BindingFlags.Instance | BindingFlags.NonPublic).RequireNotNull();
            Assert.Same(storage, field.GetValue(context.CurrentExecutableFrame));
            return PyNone.Instance;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedLocalsRemainOwnedUntilSuspendedInvocationFinishes(bool cancel)
    {
        var frontend = LythonFrontend.Compile("def f(value):\n    tap()\n    return value\nreturn f(value)\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        var storage = new object[1];
        var tap = new SuspendedStorageTap(storage);
        context.Variables["value"] = marker;
        context.Variables["tap"] = tap;
        var field = typeof(LythonRuntime).GetField("_pooledOneBoundValue", BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        Task<object?> pending;
        field.SetValue(null, storage);
        try { pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context).AsTask(); }
        finally { field.SetValue(null, previous); }
        try
        {
            Assert.NotNull(tap.Frame);
            Assert.False(pending.IsCompleted);
            Assert.Same(marker, storage[0]);
            Assert.NotNull(tap.Frame.CurrentExecutableFrame);
            if (cancel)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                tap.Release.TrySetResult();
                Assert.Same(marker, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(storage[0]);
        Assert.Null(tap.Frame!.CurrentExecutableFrame);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class SuspendedStorageTap(object[] storage) : LythonRuntime.ICallable
    {
        internal LythonRuntime.ExecutionContext? Frame;
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected async call.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = context;
            var field = typeof(LythonRuntime.ExecutableFrameState).GetField("_locals", BindingFlags.Instance | BindingFlags.NonPublic).RequireNotNull();
            Assert.Same(storage, field.GetValue(context.CurrentExecutableFrame));
            await Release.Task.ConfigureAwait(false);
            return PyNone.Instance;
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task CompiledPositionalCallsDoNotBorrowThePackagedArgumentPool(int width, bool asynchronous)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var arguments = string.Join(",", Enumerable.Repeat("value", width));
        var frontend = LythonFrontend.Compile($"def f({parameters}):\n    return a0\nreturn f({arguments})\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        context.Variables["value"] = marker;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneCallArgument" : width == 2 ? "_pooledTwoCallArguments" : "_pooledThreeCallArguments",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        var sentinel = Enumerable.Repeat(CallArgumentValue.Positional(marker), width).ToArray();
        field.SetValue(null, sentinel);
        try
        {
            object? result;
            if (asynchronous)
            {
                var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context);
                Assert.True(pending.IsCompleted);
                result = await pending;
            }
            else result = LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, null);
            Assert.Same(marker, result);
            Assert.Same(sentinel, field.GetValue(null));
            foreach (var argument in sentinel) Assert.Same(marker, argument.Value);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, true)]
    public async Task RealLoweredMethodsPreserveValuesWithoutArgumentPackaging(int width, bool asynchronous, bool keywords)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var definition = LythonFrontend.Compile($"class Box:\n def method(self,{parameters}):return a0\nbox=Box()\nmethod=box.method");
        var code = ExecutableScript.Compile(LoweredScript.Lower(definition.Script.RequireNotNull())).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        _ = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var method = Assert.IsType<PyBoundMethod>(context.Variables["method"]);
        Assert.IsType<PyFunction>(method.Function); // Actual class body stays lowered.
        var marker = new object();
        context.Variables["value"] = marker;
        var arguments = string.Join(",", Enumerable.Range(0, width).Select(i => keywords ? $"a{i}=value" : "value"));
        var call = LythonFrontend.Compile("return method(" + arguments + ")");
        code = ExecutableScript.Compile(LoweredScript.Lower(call.Script.RequireNotNull())).EntryPoint;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneCallArgument" : width == 2 ? "_pooledTwoCallArguments" : "_pooledThreeCallArguments",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        var sentinel = Enumerable.Repeat(CallArgumentValue.Positional(marker), width).ToArray();
        field.SetValue(null, sentinel);
        try
        {
            var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Same(marker, result);
            Assert.Same(sentinel, field.GetValue(null));
            foreach (var argument in sentinel)
                Assert.Same(marker, argument.Value);
            Assert.Same(method, context.Variables["method"]);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoweredMethodInputsRemainOwnedUntilItsNativeHookFinishes(bool cancel)
    {
        var definition = LythonFrontend.Compile("class Box:\n def method(self,value):\n  tap()\n  return value\nbox=Box()\nmethod=box.method");
        var code = ExecutableScript.Compile(LoweredScript.Lower(definition.Script.RequireNotNull())).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        var marker = Runtime.Text.PyString.FromString("owned", context.MemoryGovernor, null);
        var storage = new object[2];
        var tap = new SuspendedMethodTap(storage);
        context.Variables["value"] = marker;
        context.Variables["tap"] = tap;
        var baseline = budget.ReservedBytes;
        var call = LythonFrontend.Compile("return method(value)");
        code = ExecutableScript.Compile(LoweredScript.Lower(call.Script.RequireNotNull())).EntryPoint;
        var field = typeof(LythonRuntime).GetField("_pooledTwoBoundValues", BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        Task<object?> pending;
        field.SetValue(null, storage);
        try { pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask(); }
        finally { field.SetValue(null, previous); }
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.NotNull(tap.Frame);
            Assert.Null(tap.Frame.CurrentExecutableFrame);
            Assert.Same(context.Variables["box"], storage[0]);
            Assert.Same(marker, storage[1]);
            Assert.Same(storage[0], tap.Frame.Variables["self"]);
            Assert.Same(marker, tap.Frame.Variables["value"]);
            Assert.True(budget.ReservedBytes >= baseline);
            if (cancel)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                tap.Release.TrySetResult();
                Assert.Same(marker, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.All(storage, Assert.Null);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes >= baseline);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class SuspendedMethodTap(object[] storage) : LythonRuntime.ICallable
    {
        internal LythonRuntime.ExecutionContext? Frame;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected an asynchronous native call.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = context;
            Assert.Same(storage[0], context.Variables["self"]);
            Assert.Same(storage[1], context.Variables["value"]);
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return PyNone.Instance;
        }
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealMethodCallSitesKeepEveryGuardAndBodyEffect(bool asynchronous, bool defaulted)
    {
        const string definitionSource = "class Box:\n def method(self,value=7):\n  stamp(self,value)\n  return value\nbox=Box()\nmethod=box.method";
        var definition = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(definitionSource).Script!)).EntryPoint;
        var callSource = defaulted ? "return method()" : "return method(value)";
        var call = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(callSource).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var marker = new object();
        long steps;
        string[] fullEffects;
        using (var owner = budget.CreateScope())
        {
            var (context, tape) = await Prepare(owner);
            var result = await Execute(call, context);
            if (defaulted) Assert.Equal(new BigInteger(7), result);
            else Assert.Same(marker, result);
            steps = context.Limits.ExecutionStepCount;
            fullEffects = tape.Events.ToArray();
            Assert.Single(fullEffects);
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var (context, tape) = await Prepare(owner);
                context.Limits.ExecutionStepCount = 10000 - remaining;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Execute(call, context));
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                Assert.Equal(10001, context.Limits.ExecutionStepCount);
                var location = error.Span is { } span ? $"{span.Start},{span.Length},{span.Line},{span.Column}" : "-";
                checkpoints.Add(location + ":" + string.Join(';', tape.Events));
                Unwound(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(defaulted ? 15 : 16, steps);
        Assert.Equal(["body:3,1,0"], fullEffects);
        string[] expected = defaulted
            ? ["-:", "7,6,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:", "7,8,1,8:",
                "40,17,3,3:", "40,5,3,3:", "40,17,3,3:", "40,17,3,3:", "46,4,3,9:", "51,5,3,14:",
                "67,5,4,10:body:3,1,0", "0,15,1,1:body:3,1,0"]
            : ["-:", "7,6,1,8:", "14,5,1,15:", "7,13,1,8:", "7,13,1,8:", "7,13,1,8:", "7,13,1,8:", "7,13,1,8:",
                "40,17,3,3:", "40,5,3,3:", "40,17,3,3:", "40,17,3,3:", "46,4,3,9:", "51,5,3,14:",
                "67,5,4,10:body:3,1,0", "0,20,1,1:body:3,1,0"];
        Assert.Equal(expected, checkpoints);
        output.WriteLine("LOWERED_TRANSPORT_MATRIX:" + JsonSerializer.Serialize(new { asynchronous, defaulted, steps, fullEffects, checkpoints }));

        async ValueTask<(LythonRuntime.ExecutionContext, MethodEffects)> Prepare(ILythonMemoryBudget owner)
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, MaxExecutionSteps = 10000 });
            var tape = new MethodEffects(defaulted ? new BigInteger(7) : marker);
            context.Variables["value"] = marker;
            context.Variables["stamp"] = tape;
            await Execute(definition, context);
            Assert.IsType<PyFunction>(Assert.IsType<PyBoundMethod>(context.Variables["method"]).Function);
            tape.Receiver = context.Variables["box"];
            context.Limits.ExecutionStepCount = 0;
            return (context, tape);
        }

        async ValueTask<object?> Execute(ExecutableCodeObject code, LythonRuntime.ExecutionContext context)
            => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

        static void Unwound(LythonRuntime.ExecutionContext context)
        {
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    private sealed class MethodEffects(object expected) : LythonRuntime.ICallable
    {
        internal object? Receiver;
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Equal(2, arguments.Length);
            Assert.Same(Receiver, arguments[0].Value);
            Assert.Equal(expected, arguments[1].Value);
            Assert.Null(context.CurrentExecutableFrame);
            Events.Add($"body:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth},{context.MemoryGovernor.CurrentReservedBytes}");
            return PyNone.Instance;
        }
    }

}
