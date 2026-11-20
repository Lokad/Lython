using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class SequenceConstructionOwnershipTests
{
    [Fact]
    public void RefusedFinalStoragePreservesOtherOwnersAndAllocatesNoTrimArray()
    {
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        var governor = new MemoryGovernor(budget);
        ILythonMemoryReservation? padding = null;
        long beforeRefusal = 0;
        IEnumerable<object> Values()
        {
            for (var i = 0; i < 8193; i++) yield return PyNone.Instance;
            Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out padding));
            beforeRefusal = GC.GetAllocatedBytesForCurrentThread();
        }
        try
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => new PyTuple(Values(), governor));
            Assert.Equal("MemoryError", failure.ExceptionType);
            // An 8,193-element reference array alone exceeds this allowance.
            Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - beforeRefusal, 0, 64 * 1024);
            Assert.Equal(0, governor.CurrentAccountedBytes);
            Assert.NotNull(padding);
            Assert.True(budget.ReservedBytes > 0);
            padding.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null)));
        }
        finally { padding?.Dispose(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AForeignGeneratorCannotBypassTheConsumersCancellation(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var sourceOwner = budget.CreateScope();
        using var consumerOwner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var source = new Context(new MockLythonHost(), new() { MemoryBudget = sourceOwner });
        var consumer = new Context(new MockLythonHost(), new() { MemoryBudget = consumerOwner, CancellationToken = cancellation.Token });
        var probe = new Probe(cancellation);
        source.Variables["inspect"] = probe;
        var script = LythonFrontend.Compile("def values():\n yield inspect('before')\n yield inspect('cancel')\n yield inspect('after')").Script!;
        var code = Assert.Single(ExecutableScript.Compile(script).EntryPoint.FunctionBindings()).CodeObject!;
        var span = new LythonSourceSpan(1, 1, 1, 1);
        var generator = LythonRuntime.PyGenerator.Create(code, source, span);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.StringMembers.JoinAsync(PyString.Empty, generator, span, consumer);
            else LythonRuntime.StringMembers.Join(PyString.Empty, generator, span, consumer);
        });
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Equal(["before", "cancel"], probe.Events);
        Assert.Equal(0, consumer.MemoryGovernor.CurrentAccountedBytes);
        // The independent source owner remains usable; consumer cleanup must
        // neither resume nor close the retained source behind its caller.
        var next = asynchronous ? await generator.TryMoveNextAsync() : generator.TryMoveNext();
        Assert.True(next.HasValue);
        Assert.Equal("after", Assert.IsType<PyString>(next.Value).AsString());
        Assert.False((asynchronous ? await generator.TryMoveNextAsync() : generator.TryMoveNext()).HasValue);
        sourceOwner.Dispose();
        consumerOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Probe(CancellationTokenSource cancellation) : ICallable
    {
        internal readonly List<string> Events = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var value = Assert.IsType<PyString>(arguments[0].Value);
            var name = value.AsString();
            Events.Add(name);
            if (name == "cancel") cancellation.Cancel();
            return value;
        }
    }
}
