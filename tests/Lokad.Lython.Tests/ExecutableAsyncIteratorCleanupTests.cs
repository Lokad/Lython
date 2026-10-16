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
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var iterable = new CancellingSource(context.MemoryGovernor, cancellation);
        context.Variables["source"] = iterable;
        var syntax = LythonFrontend.Compile("for item in source:pass").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
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
    public async Task TerminalRoutesAwaitNativeIteratorCleanup(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var source = new PausedSource(route == "exhaust");
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
            var fill = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(fill, new(1, 1, 1, 1));
            governor.Commit(fill);
        }
        var held = budget.ReservedBytes;
        var pending = AdvanceOrClose().AsTask();
        try
        {
            var first = await Task.WhenAny(source.Disposing.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(source.Disposing.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(1, source.Disposals);
        }
        finally
        {
            source.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route is "fault" or "admission" or "cancel") { }
        }
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
    public async Task CaughtInnerFailureAwaitsDisposalAndKeepsOuterIterationAlive()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var outer = new PausedSource();
        outer.Release.TrySetResult();
        var inner = new PausedSource();
        var generator = Create(" for item in source:\n  try:\n   for unused in inner:yield 1//0\n  except ZeroDivisionError:yield item", context, outer, inner);
        var pending = generator.TryMoveNextAsync().AsTask();
        try
        {
            await inner.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, outer.Disposals);
        }
        finally
        {
            inner.Release.TrySetResult();
            _ = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var yielded = await pending;
        Assert.True(yielded.HasValue);
        Assert.Equal(new BigInteger(7), yielded.Value);
        Assert.Equal(0, outer.Disposals);
        Assert.False((await generator.TryMoveNextAsync()).HasValue);
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(1, outer.Disposals);
        Assert.True(((IPyOwnershipSnapshot)generator).TrySnapshotOwnership(out var retainedBytes));
        Assert.Equal(retainedBytes, context.MemoryGovernor.CurrentAccountedBytes);
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
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var outer = new PausedSource();
        var inner = new PausedSource();
        if (cleanupFails) inner.CleanupFailure = new InvalidOperationException("native cleanup failed");
        var generator = Create(" for item in source:\n  for unused in inner:yield 1//0", context, outer, inner);
        var pending = generator.TryMoveNextAsync().AsTask();
        try
        {
            await inner.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, outer.Disposals);
            inner.Release.TrySetResult();
            await outer.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            inner.Release.TrySetResult();
            outer.Release.TrySetResult();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        var error = await Record.ExceptionAsync(() => pending);
        if (cleanupFails) Assert.Same(inner.CleanupFailure, error);
        else Assert.Equal("ZeroDivisionError", Assert.IsType<LythonRuntimeException>(error).ExceptionType);
        Assert.Equal(1, inner.Disposals);
        Assert.Equal(1, outer.Disposals);
        Assert.True(((IPyOwnershipSnapshot)generator).TrySnapshotOwnership(out var retainedBytes));
        Assert.Equal(retainedBytes, context.MemoryGovernor.CurrentAccountedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRuntime.PyGenerator Create(string body, Context context, params object[] sources)
    {
        var frontend = LythonFrontend.Compile("def scan(source):\n" + body);
        Assert.NotNull(frontend.Script);
        var definition = Assert.Single(LoweredScript.Lower(frontend.Script).TopLevelFunctions);
        var frame = new Context(context, ScopeDirectiveFactsCollector.ForFunction(definition.Syntax))
        { FunctionName = "scan", FirstPositionalParameterName = "source" };
        frame.Variables["source"] = sources[0];
        if (sources.Length > 1) frame.Variables["inner"] = sources[1];
        return LythonRuntime.PyGenerator.Create(definition.GeneratorCode!, frame, definition.Span);
    }

    private sealed class PausedSource(bool empty = false) : IPyAsyncIterableValue, IEnumerable<object>, IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Disposals { get; private set; }
        public Action? OnMove { get; set; }
        public Exception? CleanupFailure { get; set; }
        private int _moves;
        public object Current => new BigInteger(7);
        public IEnumerable<object> Iterate() => this;
        public IEnumerator<object> GetEnumerator() => throw new InvalidOperationException("Unexpected synchronous traversal.");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public IAsyncEnumerable<object> IterateAsync() => this;
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        public ValueTask<bool> MoveNextAsync()
        {
            OnMove?.Invoke();
            return new(!empty && _moves++ == 0);
        }
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            Disposing.TrySetResult();
            await Release.Task.ConfigureAwait(false);
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
