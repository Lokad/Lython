using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StringFormattingLifetimeTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task FinalFundingFailurePreservesTargetAliasesAndReturnsSharedAllowance(bool asynchronous, bool cancel)
    {
        var syntax = LythonFrontend.Compile("global target\narm()\ntarget=format(value, '>8s')\nrecord()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var root = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString("é😀", context.MemoryGovernor, null);
            var original = PyString.FromString("kept", context.MemoryGovernor, null);
            context.Variables["value"] = input;
            context.Variables["target"] = original;
            var reached = false;
            var effects = new Callback(() => { });
            context.Variables["record"] = effects;
            context.Variables["arm"] = new Callback(() =>
            {
                // Eight scalar columns: six spaces and the original six UTF-8 bytes.
                var outputBytes = PyString.EstimateApproximateBytes(12);
                if (cancel) hook.AfterGrowth = bytes => { if (bytes == outputBytes) { reached = true; cancellation.Cancel(); } };
                else hook.RefuseGrowth = bytes => { if (bytes != outputBytes) return false; reached = true; return true; };
            });
            var inputFunding = root.ReservedBytes;
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context))
                : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.True(reached);
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Same(original, context.Variables["target"]);
            Assert.Equal(0, effects.Calls);
            Assert.Equal("é😀", input.AsString());
            Assert.Equal("kept", original.AsString());
            Assert.True(root.ReservedBytes >= inputFunding);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class Callback(Action action) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context) { Calls++; action(); return PyNone.Instance; }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context) => ValueTask.FromResult(Invoke(args, span, context));
    }

    private sealed class AdmissionHook(ILythonMemoryBudget parent) : ILythonMemoryBudget
    {
        internal Action<long>? AfterGrowth;
        internal Func<long, bool>? RefuseGrowth;
        public ILythonMemoryScope CreateScope() => parent.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (bytes > 0 && RefuseGrowth?.Invoke(bytes) == true) { reservation = null; return false; }
            if (!parent.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new ProbeReservation(this, admitted);
            AfterGrowth?.Invoke(bytes);
            return true;
        }
        private sealed class ProbeReservation(AdmissionHook hook, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var old = inner.Bytes;
                if (bytes > old && hook.RefuseGrowth?.Invoke(bytes - old) == true) return false;
                if (!inner.TryResize(bytes)) return false;
                if (bytes > old) hook.AfterGrowth?.Invoke(bytes - old);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
