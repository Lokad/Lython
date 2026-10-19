using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class ExecutableStackAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrowthRefusesBeforeInvokingTheArgumentConsumer(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var fill = new FillBudget(budget);
        var sink = new Sink(64);
        context.Variables["fill"] = fill;
        context.Variables["sink"] = sink;
        var code = Compile("fill()\nsink(" + string.Join(",", Enumerable.Range(0, 64)) + ")");
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(2, failure.Span?.Line);
        Assert.Equal(0, sink.Calls);
        Assert.True(context.MemoryGovernor.LastDeniedReservationBytes > 0);
        Assert.Equal(fill.Bytes, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrownBufferRemainsFundedUntilItsConsumerFinishes(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var baseline = budget.ReservedBytes;
        var sink = new Sink(1024, pause: true);
        context.Variables["sink"] = sink;
        var code = Compile("sink(" + string.Join(",", Enumerable.Range(0, 1024)) + ")");
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
            try
            {
                await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0);
            }
            finally
            {
                sink.Release.TrySetResult();
                _ = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.Equal(1, sink.Calls);
        Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task YieldRetainsBufferAndCloseRefundsOnlyThatStorage()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var code = Compile("result=([" + string.Join(",", Enumerable.Range(0, 1024)) + "] for i in source)");
        var operation = Assert.Single(code.Constants.OfType<ExecutableCreateGeneratorExpression>());
        var generator = await LythonRuntime.PyGenerator.CreateExpressionAsync(operation.CodeObject, context,
            new PyTuple([BigInteger.Zero]), operation.OuterSpan, operation.OuterSpan, true);
        var initial = Snapshot(generator);
        var item = await generator.TryMoveNextAsync();
        Assert.True(item.HasValue);
        var retained = Assert.IsType<PyList>(item.Value);
        var grown = Snapshot(generator);
        Assert.True(grown > initial);
        var held = budget.ReservedBytes;
        Assert.True(generator.TryGetMember("close", out var close));
        _ = await Assert.IsAssignableFrom<ICallable>(close).InvokeAsync([], operation.OuterSpan, context);
        var remaining = Snapshot(generator);
        Assert.True(remaining < grown);
        Assert.Equal(held - (grown - remaining), budget.ReservedBytes);
        Assert.Equal(1024, retained.Count);
        Assert.Equal(new BigInteger(1023), retained[1023]);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static long Snapshot(LythonRuntime.PyGenerator generator)
    {
        Assert.True(((IPyOwnershipSnapshot)generator).TrySnapshotOwnership(out var bytes));
        return bytes;
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(frontend.Script);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
    }

    private sealed class FillBudget(LythonMemoryBudget budget) : ICallable
    {
        internal long Bytes;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var governor = context.MemoryGovernor;
            // Leave the documented transient-value headroom. A replacement
            // operand buffer exceeds it, while existing scalar observations fit.
            Bytes = budget.LimitBytes!.Value - governor.CurrentAccountedBytes - 64;
            governor.Reserve(Bytes, span);
            governor.Commit(Bytes);
            return PyNone.Instance;
        }
    }

    private sealed class Sink(int count, bool pause = false) : ICallable
    {
        internal int Calls;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Assert.Equal(count, arguments.Length);
            for (var i = 0; i < count; i++) Assert.Equal(new BigInteger(i), arguments[i].Value);
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (pause)
            {
                Entered.TrySetResult();
                await Release.Task.ConfigureAwait(false);
            }
            return Invoke(arguments, span, context);
        }
    }
}
