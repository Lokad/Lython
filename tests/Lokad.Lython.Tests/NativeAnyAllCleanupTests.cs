using System.Collections;
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
    public async Task AggregateDisposesFundedCursorExactlyOnce(bool all, string route, bool cleanupFails)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner,             CancellationToken = cancellation.Token,
        },regexProvider:Utf8RegexProvider.Instance);
        var source = new OwnedSource(budget, context.MemoryGovernor, cancellation, all, route);
        if (cleanupFails) source.CleanupFailure = new InvalidOperationException("native cleanup failed");
        context.Variables["source"] = source;
        var syntax = LythonFrontend.Compile("return " + (all ? "all" : "any") + "(source)",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        var failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
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

    private sealed class OwnedSource(LythonMemoryBudget budget, MemoryGovernor governor, CancellationTokenSource cancellation, bool all, string route)
        : IPyIterableValue, IEnumerable<object>, IEnumerator<object>
    {
        private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        internal Exception? CleanupFailure;
        internal int Disposals;
        internal int Moves;
        public object Current => route == "short" ? !all : all;
        object IEnumerator.Current => Current;
        public IEnumerable<object> Iterate() => this;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerator<object> GetEnumerator()
        {
            _storage = governor.ReserveTemporary(4096, Span);
            return this;
        }
        public void Reset() => throw new NotSupportedException();
        public bool MoveNext()
        {
            Moves++;
            if (route == "fault") throw new LythonRuntimeException("ValueError", "pull failed", Span);
            if (route == "admission") governor.Reserve(budget.LimitBytes!.Value + 1, Span);
            if (route == "cancel") cancellation.Cancel();
            return route != "exhaust" || Moves <= 2;
        }
        public void Dispose()
        {
            Disposals++;
            var storage = _storage;
            _storage = null;
            using (storage)
            {
                Assert.NotNull(storage);
                Assert.True(governor.CurrentReservedBytes >= 4096);
                Assert.True(budget.ReservedBytes >= 4096);
            }
            if (CleanupFailure is not null) throw CleanupFailure;
        }
    }
}
