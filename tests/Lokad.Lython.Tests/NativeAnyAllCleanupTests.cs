using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeAnyAllCleanupTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var all in new[] { false, true })
        {
            foreach (var route in new[] { "short", "exhaust", "fault" })
            foreach (var cleanupFails in new[] { false, true })
                yield return [all, route, cleanupFails];
            yield return [all, "cancel", false];
            yield return [all, "admission", false];
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task AggregateAwaitsFundedNativeCleanupExactlyOnce(bool all, string route, bool cleanupFails)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024,
            CancellationToken = cancellation.Token,
        });
        var source = new PausedSource(context.MemoryGovernor, cancellation, all, route);
        if (cleanupFails) source.CleanupFailure = new InvalidOperationException("native cleanup failed");
        context.Variables["source"] = source;
        var syntax = LythonFrontend.Compile("return " + (all ? "all" : "any") + "(source)").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var baseline = budget.ReservedBytes;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        Exception? failure = null;
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
            source.Release.TrySetResult();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        if (cleanupFails)
        {
            var error = Assert.IsType<LythonRuntimeException>(failure);
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal("native cleanup failed", error.Message);
        }
        else if (route == "fault") Assert.Equal("ValueError", Assert.IsType<LythonRuntimeException>(failure).ExceptionType);
        else if (route == "cancel") Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        else if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        else
        {
            Assert.Null(failure);
            Assert.Equal(route == "short" ? !all : all, Assert.IsType<bool>(await pending));
            Assert.Equal(route == "short" ? 1 : 3, source.Moves);
        }
        Assert.Equal(1, source.Disposals);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PausedSource(MemoryGovernor governor, CancellationTokenSource cancellation, bool all, string route)
        : IPyAsyncIterableValue, IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        internal readonly TaskCompletionSource Disposing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? CleanupFailure;
        internal int Disposals;
        internal int Moves;
        public object Current => route == "short" ? !all : all;
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
            if (route == "fault") throw new LythonRuntimeException("ValueError", "pull failed", Span);
            if (route == "admission") governor.Reserve(governor.MaxAccountedBytes!.Value + 1, Span);
            if (route == "cancel") cancellation.Cancel();
            return new(route != "exhaust" || Moves <= 2);
        }
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            Disposing.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); }
            finally { _storage?.Dispose(); _storage = null; }
            if (CleanupFailure is not null) throw CleanupFailure;
        }
    }
}
