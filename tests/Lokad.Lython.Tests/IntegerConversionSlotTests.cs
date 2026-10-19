using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class IntegerConversionSlotTests
{
    [Theory]
    [InlineData("__int__", false)]
    [InlineData("__int__", true)]
    [InlineData("__index__", false)]
    [InlineData("__index__", true)]
    public async Task OrdinaryConversionPreservesResultsAndCleanup(string slot, bool asynchronous)
    {
        var preamble = Compile($"class Value:\n def {slot}(self): return 7\nvalue=Value()");
        var code = Compile("return int(value)");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = await Prepare(owner);
            Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(await Execute(code, context, asynchronous)));
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var body = slot == "__int__" ? "40,1,2,28" : "42,1,2,30";

        async ValueTask<Context> Prepare(ILythonMemoryBudget owner)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
            await Execute(preamble, context, asynchronous);
            return context;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInsideConversionUnwindsBeforeTheResult(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var cancel = new Cancel(cancellation);
        context.Variables["cancel"] = cancel;
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await Execute(Compile("class Value:\n def __int__(self): return cancel()\nreturn int(Value())"), context, asynchronous));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(1, cancel.Calls);
        Unwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static async ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

    private static void Unwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Cancel(CancellationTokenSource cancellation) : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            cancellation.Cancel();
            return new BigInteger(7);
        }
    }
}
