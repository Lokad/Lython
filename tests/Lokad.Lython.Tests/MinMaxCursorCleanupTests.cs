using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MinMaxCursorCleanupTests
{
    public static IEnumerable<object[]> Routes()
    {
        var routes = new (string Route, bool CleanupFails)[]
        {
            ("exhaust", false), ("exhaust", true),
            ("pull-fault", false), ("pull-fault", true),
            ("compare-fault", false), ("compare-fault", true),
            ("cancel", false), ("admission", false), ("admission", true),
        };
        foreach (var operation in new[] { "min", "max" })
        foreach (var route in routes)
            yield return [operation, route.Route, route.CleanupFails];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ExtremaAwaitFundedCleanupExactlyOnce(string operation, string route, bool cleanupFails)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var source = new PausedSource(budget, context.MemoryGovernor, cancellation, route, cleanupFails);
        context.Variables["source"] = source;
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return " + operation + "(source)").Script!)).EntryPoint;
        var baseline = budget.ReservedBytes;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        Exception? failure;
        try
        {
            var first = await Task.WhenAny(source.Disposing.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(source.Disposing.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, source.Disposals);
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 4096);
            Assert.True(budget.ReservedBytes >= baseline + 4096);
        }
        finally
        {
            if (!source.Disposing.Task.IsCompleted) cancellation.Cancel();
            source.Release.TrySetResult();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route == "admission") Assert.Same(source.Refusal, context.MemoryGovernor.AdmissionFailure);
        if (cleanupFails)
        {
            var error = Assert.IsType<LythonRuntimeException>(failure);
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal("native cleanup failed", error.Message);
        }
        else if (route == "pull-fault") Assert.Equal("ValueError", Assert.IsType<LythonRuntimeException>(failure).ExceptionType);
        else if (route == "compare-fault") Assert.Equal("TypeError", Assert.IsType<LythonRuntimeException>(failure).ExceptionType);
        else if (route == "cancel") Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        else if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        else
        {
            Assert.Null(failure);
            Assert.Equal(new BigInteger(operation == "min" ? 2 : 3), await pending);
            Assert.Equal(3, source.Moves);
        }
        Assert.Equal(1, source.Disposals);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PausedSource(LythonMemoryBudget budget, MemoryGovernor governor, CancellationTokenSource cancellation, string route, bool cleanupFails)
        : IPyAsyncIterableValue, IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        private readonly PyString _invalid = PyString.FromString("bad", governor, Span);
        internal readonly TaskCompletionSource Disposing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Disposals;
        internal int Moves;
        internal LythonRuntimeException? Refusal;
        public object Current => route == "compare-fault" && Moves > 1 ? _invalid : new BigInteger(4 - Moves);
        public IEnumerable<object> Iterate() => [];
        public IAsyncEnumerable<object> IterateAsync() => this;
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _storage = governor.ReserveTemporary(4096, Span);
            return this;
        }
        public ValueTask<bool> MoveNextAsync()
        {
            Moves++;
            if (route == "pull-fault") throw new LythonRuntimeException("ValueError", "pull failed", Span);
            if (route == "admission")
            {
                try { governor.Reserve(budget.LimitBytes!.Value + 1, Span); }
                catch (LythonRuntimeException error) { Refusal = error; throw; }
            }
            if (route == "cancel") cancellation.Cancel();
            return new(route != "exhaust" || Moves <= 2);
        }
        public async ValueTask DisposeAsync()
        {
            Disposals++; Disposing.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); }
            finally { _storage?.Dispose(); _storage = null; }
            if (cleanupFails) throw new LythonRuntimeException("RuntimeError", "native cleanup failed", Span);
        }
    }
}
