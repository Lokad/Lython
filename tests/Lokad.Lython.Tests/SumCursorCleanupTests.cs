using System.Collections;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SumCursorCleanupTests
{
    public static TheoryData<string, bool> Routes => new()
    {
        { "exhaust", false }, { "exhaust", true },
        { "pull-fault", false }, { "pull-fault", true },
        { "add-fault", false }, { "add-fault", true },
        { "cancel", false }, { "admission", false }, { "admission", true },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task SumDisposesFundedCursorExactlyOnce(string route, bool cleanupFails)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024); using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var source = new OwnedSource(budget, context.MemoryGovernor, cancellation, route, cleanupFails);
        context.Variables["source"] = source;
        var code = ExecutableScript.Compile(LythonFrontend.Compile("return sum(source)",regexProvider:Utf8RegexProvider.Instance).Script!).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        var failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        if (route == "admission") Assert.Same(source.Refusal, context.MemoryGovernor.AdmissionFailure);
        if (cleanupFails)
        {
            var error = Assert.IsType<LythonRuntimeException>(failure);
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal("native cleanup failed", error.Message);
        }
        else if (route == "pull-fault") Assert.Equal("ValueError", Assert.IsType<LythonRuntimeException>(failure).ExceptionType);
        else if (route == "add-fault") Assert.Equal("TypeError", Assert.IsType<LythonRuntimeException>(failure).ExceptionType);
        else if (route == "cancel") Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        else if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        else { Assert.Null(failure); Assert.Equal(new BigInteger(2), await pending); Assert.Equal(3, source.Moves); }
        Assert.Equal(1, source.Disposals);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class OwnedSource(LythonMemoryBudget budget, MemoryGovernor governor, CancellationTokenSource cancellation, string route, bool cleanupFails)
        : IPyIterableValue, IEnumerable<object>, IEnumerator<object>
    {
        private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        private readonly PyString _invalid = PyString.FromString("bad", governor, Span);
        internal int Disposals;
        internal int Moves;
        internal LythonRuntimeException? Refusal;
        public object Current => route == "add-fault" ? _invalid : BigInteger.One;
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
            if (route == "pull-fault") throw new LythonRuntimeException("ValueError", "pull failed", Span);
            if (route == "admission")
            {
                try { governor.Reserve(budget.LimitBytes!.Value + 1, Span); }
                catch (LythonRuntimeException error) { Refusal = error; throw; }
            }
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
            if (cleanupFails) throw new InvalidOperationException("native cleanup failed");
        }
    }
}
