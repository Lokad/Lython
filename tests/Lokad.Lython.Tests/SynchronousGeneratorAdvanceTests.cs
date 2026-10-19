using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class SynchronousGeneratorAdvanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task YieldRestoresCallerStateAndEachPullObservesTheCurrentException(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var probe = new Probe();
        context.Variables["inspect"] = probe;
        var code = GeneratorCode(" yield inspect()\n yield inspect()");
        var generator = LythonRuntime.PyGenerator.Create(code, context, new(1, 1, 1, 1));
        for (var i = 0; i < 2; i++)
        {
            var exception = new PyException("ValueError", $"caller {i}", PyNone.Instance);
            probe.ExpectedException = exception;
            context.Services.SetCurrentException(exception);
            var result = await Move(generator, asynchronous);
            Assert.True(result.HasValue);
            Assert.Equal(new BigInteger(i + 1), result.Value);
            Assert.Same(exception, context.Services.CurrentException);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        }
        Assert.False((await Move(generator, asynchronous)).HasValue);
        Assert.Equal(2, probe.Calls);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "admission")]
    [InlineData(true, "admission")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "function")]
    [InlineData(true, "function")]
    [InlineData(false, "interpreter")]
    [InlineData(true, "interpreter")]
    public async Task RefusedReentryDoesNotAdvanceAndReleasesNativeStorage(bool asynchronous, string refusal)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner,           MaxRecursionDepth = 4, CancellationToken = cancellation.Token });
        var source = new Source(context.MemoryGovernor);
        context.Variables["source"] = source;
        var generator = LythonRuntime.PyGenerator.Create(GeneratorCode(" for item in source:yield item"), context, new(1, 1, 1, 1));
        Assert.True((await Move(generator, asynchronous)).HasValue);
        Assert.Equal(1, source.Moves);
        Assert.Equal(0, source.Disposals);
        switch (refusal)
        {
            case "admission":
                var governor = context.MemoryGovernor;
                var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
                governor.Reserve(fill, null);
                governor.Commit(fill);
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null));
                break;
            case "cancel": cancellation.Cancel(); break;
            case "function": context.Limits.CurrentRecursionDepth = 4; break;
            case "interpreter": context.Limits.CurrentInterpreterDepth = LythonRuntime.ExecutionLimits.MaxInterpreterDepth; break;
        }
        var held = budget.ReservedBytes;
        var recursionDepth = context.Limits.CurrentRecursionDepth;
        var interpreterDepth = context.Limits.CurrentInterpreterDepth;
        var callerException = new PyException("ValueError", "caller", PyNone.Instance);
        context.Services.SetCurrentException(callerException);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Move(generator, asynchronous));
        if (refusal == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        else Assert.Equal(refusal switch
        {
            "cancel" => LythonFailureReason.Cancelled,
            "function" => LythonFailureReason.RecursionDepthLimit,
            _ => LythonFailureReason.InterpreterStackLimit,
        }, failure.Attribution?.Reason);
        Assert.Equal(1, source.Moves);
        Assert.Equal(1, source.Disposals);
        Assert.True(budget.ReservedBytes <= held - 4096);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Same(callerException, context.Services.CurrentException);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(recursionDepth, context.Limits.CurrentRecursionDepth);
        Assert.Equal(interpreterDepth, context.Limits.CurrentInterpreterDepth);
        Assert.False((await Move(generator, asynchronous)).HasValue);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async ValueTask<PyIterationResult> Move(LythonRuntime.PyGenerator generator, bool asynchronous)
        => asynchronous ? await generator.TryMoveNextAsync()
            : generator.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;

    private static ExecutableCodeObject GeneratorCode(string body)
    {
        var script = LythonFrontend.Compile("def values():\n" + body).Script!;
        return Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(script)).EntryPoint.Functions).CodeObject!;
    }

    private sealed class Probe : ICallable
    {
        internal int Calls;
        internal PyException? ExpectedException;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Same(ExpectedException, context.Services.CurrentException);
            Assert.NotNull(context.CurrentExecutableFrame);
            Assert.Equal(1, context.Limits.CurrentRecursionDepth);
            Assert.Equal(2, context.Limits.CurrentInterpreterDepth);
            return new BigInteger(++Calls);
        }
    }

    private sealed class Source(MemoryGovernor governor) : IEnumerable<object>
    {
        internal int Moves, Disposals;
        public IEnumerator<object> GetEnumerator()
        {
            using var storage = governor.ReserveTemporary(4096, null);
            try { while (true) { Moves++; yield return new BigInteger(7); } }
            finally { Disposals++; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
