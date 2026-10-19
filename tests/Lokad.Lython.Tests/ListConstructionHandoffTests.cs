using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ListConstructionHandoffTests
{
    // Complete records frozen on19cd6654 before runtime changes.
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly ExecutableCodeObject Code = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return list(source)").Script!)).EntryPoint;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var shape in new[] { "known-empty", "known-wide", "empty", "pair", "wide", "move", "cleanup", "both", "limit" })
            yield return [asynchronous, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ConstructionPreservesEffectsAndCleanupOrder(bool asynchronous, string shape)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string outcome;
        string[] events;
        using (var owner = budget.CreateScope())
        {
            var (context, trace) = Prepare(owner, shape);
            try
            {
                var list = Assert.IsType<PyList>(await Run(context, asynchronous));
                Assert.DoesNotContain(shape, new[] { "move", "cleanup", "both", "limit" });
                var count = shape.Contains("empty", StringComparison.Ordinal) ? 0 : shape.Contains("wide", StringComparison.Ordinal) ? 65 : 2;
                Assert.Equal(Enumerable.Range(0, count).Select(i => (object)new BigInteger(i)), list);
                outcome = "list:" + list.Count;
            }
            catch (Exception error)
            {
                Assert.Contains(shape, new[] { "move", "cleanup", "both", "limit" });
                outcome = Describe(error);
                if (shape is "cleanup" or "both") Assert.Equal("cleanup", error.Message);
                if (shape == "move") Assert.Equal("move", error.Message);
            }
            events = trace.ToArray();
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        if (shape.StartsWith("known", StringComparison.Ordinal)) Assert.Empty(events);
        else
        {
            var count = shape == "empty" ? 0 : shape == "wide" ? 65 : 2;
            var expected = new[] { "open" }.Concat(Enumerable.Range(0, count).Select(i => "pull:" + i));
            if (shape != "limit") expected = expected.Concat(["end"]);
            Assert.Equal(expected.Concat(["close"]), events);
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("cancel")]
    public async Task PendingCleanupKeepsPrivateBackingFundedUntilTheHandoff(string completion)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var value = PyString.FromString(new string('x', 4096), context.MemoryGovernor, Span);
            var source = new PendingCleanup(value);
            var list = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["list"]);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
            var before = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var pending = list.InvokeAsync(arguments, Span, context).AsTask();
            try
            {
                await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > before);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
                if (completion == "cancel") source.Release.SetCanceled();
                else source.Release.SetResult(completion == "error");
                if (completion == "success")
                {
                    var result = Assert.IsType<PyList>(await pending);
                    Assert.Same(value, Assert.Single(result));
                    Assert.True(budget.ReservedBytes > before);
                    Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
                }
                else
                {
                    if (completion == "error") await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
                    else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                    Assert.Equal(before, budget.ReservedBytes);
                    Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                }
                Assert.Equal(1, source.CleanupCalls);
                Assert.Equal(new string('x', 4096), value.AsString());
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                source.Release.TrySetResult(false);
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (InvalidOperationException) when (completion == "error") { }
                catch (OperationCanceledException) when (completion == "cancel") { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (Context, List<string>) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner,             MaxCollectionSize = shape == "limit" ? 1 : 100,
        });
        var events = new List<string>();
        var count = shape.Contains("empty", StringComparison.Ordinal) ? 0 : shape.Contains("wide", StringComparison.Ordinal) ? 65 : 2;
        context.Variables["source"] = shape.StartsWith("known", StringComparison.Ordinal)
            ? new PyTuple(Enumerable.Range(0, count).Select(i => (object)new BigInteger(i)).ToArray(), context.MemoryGovernor, Span)
            : new Source(events, count, shape is "move" or "both", shape is "cleanup" or "both");
        context.Variables["list"] = context.State.BuiltinVariables["list"];
        return (context, events);
    }

    private static ValueTask<object?> Run(Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null));

    private static string Describe(Exception error)
        => error is LythonRuntimeException python
            ? $"{python.ExceptionType}:{python.Message}:{python.Span?.Start},{python.Span?.Length},{python.Span?.Line},{python.Span?.Column}:{python.Attribution?.Reason}"
            : error.GetType().Name + ":" + error.Message;

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Source(List<string> events, int count, bool moveFailure, bool cleanupFailure) : IPyAsyncIterableValue
    {
        public IEnumerable<object> Iterate()
        {
            events.Add("open");
            try
            {
                for (var i = 0; i < count; i++) { events.Add("pull:" + i); yield return new BigInteger(i); }
                events.Add("end");
                if (moveFailure) throw new InvalidOperationException("move");
            }
            finally
            {
                events.Add("close");
                if (cleanupFailure) throw new InvalidOperationException("cleanup");
            }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.CompletedTask;
            foreach (var value in Iterate()) yield return value;
        }
    }

    private sealed class PendingCleanup(object value) : IPyAsyncIterableValue
    {
        internal int CleanupCalls;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<object> Iterate() { yield return value; }
        public IAsyncEnumerable<object> IterateAsync() => new Sequence(this, value);

        private sealed class Sequence(PendingCleanup owner, object value) : IAsyncEnumerable<object>, IAsyncEnumerator<object>
        {
            private bool _yielded;
            public object Current => value;
            public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
            public ValueTask<bool> MoveNextAsync()
            {
                var next = !_yielded;
                _yielded = true;
                return ValueTask.FromResult(next);
            }
            public async ValueTask DisposeAsync()
            {
                owner.CleanupCalls++;
                owner.Entered.SetResult();
                if (await owner.Release.Task.ConfigureAwait(false)) throw new InvalidOperationException("cleanup");
            }
        }
    }
}
