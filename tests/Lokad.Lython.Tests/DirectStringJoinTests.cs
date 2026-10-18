using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectStringJoinTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DistinctReceiversProduceOwnedValuesWithoutBoundMethodCaches(bool asynchronous, bool generator)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        context.Variables["left"] = PyString.FromString("|", context.MemoryGovernor, null);
        context.Variables["right"] = PyString.FromString("::", context.MemoryGovernor, null);
        context.Variables["parts"] = new PyList([PyString.FromString("café"), PyString.FromString("😀")], context.MemoryGovernor, null);
        var record = new Record();
        context.Variables["record"] = record;
        var code = Compile("for sep in [left,right]:\n result=sep.join(" + (generator ? "part for part in parts" : "parts") + ")\n record()\nreturn result");
        _ = await Execute(code, context, asynchronous);
        Assert.Equal(new[] { "café|😀", "café::😀" }, record.Values.Select(v => v.AsString()));
        Assert.All(record.Values, value => Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor));
        var caches = (Array)record.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(record.Frame)!;
        Assert.All(caches.Cast<object?>(), Assert.Null);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultAdmissionRefusesWithLiveOperandsAndPreservesInputs(bool asynchronous, bool shared)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(shared ? limit : 4 * limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, MaxExecutionMemoryBytes = shared ? null : limit });
        var first = PyString.FromString("abcd", context.MemoryGovernor, null);
        var last = PyString.FromString("efgh", context.MemoryGovernor, null);
        var parts = new PyList([first, last], context.MemoryGovernor, null);
        context.Variables["sep"] = PyString.FromString("|", context.MemoryGovernor, null);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (var pad = new Pad(parts, budget, limit, shared))
        {
            context.Variables["fill"] = pad;
            object? published = null;
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                published = await Execute(Compile("return sep.join(fill())"), context, asynchronous));
            Assert.Null(published);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit, failure.Attribution?.Reason);
            Assert.Equal(1, failure.Span?.Line);
            Assert.Equal(PyString.EstimateApproximateBytes(9), context.MemoryGovernor.LastDeniedReservationBytes);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(1, pad.Calls);
            Assert.Same(first, parts[0]);
            Assert.Same(last, parts[1]);
        }
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    private sealed class Record : LythonRuntime.ICallable
    {
        internal List<PyString> Values = [];
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = context.CurrentExecutableFrame;
            Assert.True(Frame!.TryResolveOwnLocal("result", out var result));
            Values.Add(Assert.IsType<PyString>(result));
            return PyNone.Instance;
        }
    }
    private sealed class Pad(PyList parts, LythonMemoryBudget budget, long limit, bool shared) : LythonRuntime.ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _temporary;
        private ILythonMemoryReservation? _shared;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            var available = 64 + 16 + PyString.EstimateApproximateBytes(9) - 1;
            if (shared) Assert.True(budget.TryReserve(limit - budget.ReservedBytes - available, out _shared));
            else _temporary = context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes - available, span);
            return parts;
        }
        public void Dispose() { _temporary?.Dispose(); _shared?.Dispose(); }
    }
}
