using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAsyncIteratorCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBetweenInstructionsDisposesNativeWorkingStorage(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var iterable = new CancellingSource(context.MemoryGovernor, cancellation);
        context.Variables["source"] = iterable;
        var syntax = LythonFrontend.Compile("for item in source:pass",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(1, iterable.Disposals);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("fault")]
    [InlineData("close")]
    [InlineData("return")]
    [InlineData("break")]
    [InlineData("exhaust")]
    [InlineData("admission")]
    [InlineData("cancel")]
    public async Task TerminalRoutesReleaseNativeIteratorStorage(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var source = new OwnedSource(route == "exhaust");
        if (route == "cancel") source.OnMove = cancellation.Cancel;
        var body = route switch
        {
            "fault" => " for item in source:\n  yield 1//0",
            "return" => " if False:yield 0\n for item in source:return",
            "break" => " if False:yield 0\n for item in source:break",
            "admission" => " for item in source:yield [0]*100",
            _ => " for item in source:yield item",
        };
        var generator = Create(body, context, source);
        if (route == "close")
            Assert.True((await generator.TryMoveNextAsync()).HasValue);
        if (route == "admission")
        {
            var governor = context.MemoryGovernor;
            var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(fill, new(1, 1, 1, 1));
            governor.Commit(fill);
        }
        var pending = AdvanceOrClose().AsTask();
        if (route is "fault" or "admission" or "cancel")
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            else if (route == "cancel") Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            else Assert.Equal("ZeroDivisionError", error.ExceptionType);
        }
        else await pending;
        Assert.Equal(1, source.Disposals);
        Assert.False((await generator.TryMoveNextAsync()).HasValue);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        async ValueTask AdvanceOrClose()
        {
            if (route == "close")
            {
                Assert.True(generator.TryGetMember("close", out var close));
                _ = await Assert.IsAssignableFrom<ICallable>(close).InvokeAsync([], new(1, 1, 1, 1), context);
            }
            else _ = await generator.TryMoveNextAsync();
        }
    }

    [Fact]
    public async Task CaughtInnerFailureDisposesInnerAndKeepsOuterIterationAlive()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var outer = new OwnedSource();
        var inner = new OwnedSource();
        var generator = Create(" for item in source:\n  try:\n   for unused in inner:yield 1//0\n  except ZeroDivisionError:yield item", context, outer, inner);
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        var yielded = await generator.TryMoveNextAsync();
        Assert.Equal(1, inner.Disposals);
        Assert.True(yielded.HasValue);
        Assert.Equal(new BigInteger(7), yielded.Value);
        Assert.Equal(0, outer.Disposals);
        Assert.False((await generator.TryMoveNextAsync()).HasValue);
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(1, outer.Disposals);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes, 1, baseline);
        // A returned frame can remain funded while the execution cache is live.
        context.State.CloseResources();
        Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes, 1, baseline - 1);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalUnwindDisposesInnerThenOuterEvenIfCleanupFails(bool cleanupFails)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var outer = new OwnedSource();
        var inner = new OwnedSource();
        if (cleanupFails) inner.CleanupFailure = new InvalidOperationException("native cleanup failed");
        var generator = Create(" for item in source:\n  for unused in inner:yield 1//0", context, outer, inner);
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        var order = new List<string>();
        inner.OnDispose = () => order.Add("inner");
        outer.OnDispose = () => order.Add("outer");
        var error = await Record.ExceptionAsync(async () => await generator.TryMoveNextAsync());
        Assert.Equal(new[] { "inner", "outer" }, order);
        if (cleanupFails) Assert.Same(inner.CleanupFailure, error);
        else Assert.Equal("ZeroDivisionError", Assert.IsType<LythonRuntimeException>(error).ExceptionType);
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(1, outer.Disposals);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes, 1, baseline);
        context.State.CloseResources();
        Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes, 1, baseline - 1);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("return")]
    [InlineData("break")]
    [InlineData("fault")]
    public async Task LeavingLoopDisposesCursorBeforeOuterFinally(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var source = new OwnedSource();
        var stamp = new Stamp();
        context.Variables["stamp"] = stamp;
        var statement = route == "fault" ? "1//0" : route;
        var generator = Create(" if False:yield 0\n try:\n  for item in source:" + statement + "\n finally:stamp()", context, source);
        source.OnDispose = () =>
        {
            Assert.Equal(0, stamp.Calls);
            Assert.True(budget.ReservedBytes > 0);
        };
        var pending = generator.TryMoveNextAsync().AsTask();
        if (route == "fault")
            Assert.Equal("ZeroDivisionError", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).ExceptionType);
        else Assert.False((await pending).HasValue);
        Assert.Equal(1, stamp.Calls);
        Assert.Equal(1, source.Disposals);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Stamp : ICallable
    {
        public int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            return PyNone.Instance;
        }
    }

    private static LythonRuntime.PyGenerator Create(string body, Context context, params object[] sources)
    {
        var frontend = LythonFrontend.Compile("def scan(source):\n" + body,regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(frontend.Script);
        var definition = Assert.IsType<FunctionDefinitionStatementSyntax>(Assert.Single(frontend.Script.Statements));
        var code = Assert.Single(ExecutableScript.Compile(frontend.Script).EntryPoint.FunctionBindings()).CodeObject;
        if (sources.Length > 1) context.Variables["inner"] = sources[1];
        var function = new PyGeneratorFunction(definition.ParameterLayout, code, context, default);
        return Assert.IsType<LythonRuntime.PyGenerator>(
            function.Invoke([CallArgumentValue.Positional(sources[0])], definition.Span, context));
    }

    private sealed class OwnedSource(bool empty = false) : IPyIterableValue, IEnumerable<object>, IEnumerator<object>
    {
        public int Disposals { get; private set; }
        public Action? OnMove { get; set; }
        public Action? OnDispose { get; set; }
        public Exception? CleanupFailure { get; set; }
        private int _moves;
        public object Current => new BigInteger(7);
        object System.Collections.IEnumerator.Current => Current;
        public IEnumerable<object> Iterate() => this;
        public IEnumerator<object> GetEnumerator() => this;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext()
        {
            OnMove?.Invoke();
            return !empty && _moves++ == 0;
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose()
        {
            Disposals++;
            OnDispose?.Invoke();
            if (CleanupFailure is not null) throw CleanupFailure;
        }
    }

    private sealed class CancellingSource(MemoryGovernor governor, CancellationTokenSource cancellation) : IPyIterableValue
    {
        public int Disposals { get; private set; }
        public IEnumerable<object> Iterate()
        {
            using var storage = governor.ReserveTemporary(4096, null);
            try
            {
                cancellation.Cancel();
                yield return BigInteger.One;
            }
            finally { Disposals++; }
        }
    }
}
