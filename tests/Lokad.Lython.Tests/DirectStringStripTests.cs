using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DirectStringStripTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinctStringsDoNotConstructBoundMethodCaches(bool asynchronous)
    {
        var (code, context) = Prepare("for text in [left,right]:\n    result=text.strip()\n    record()\n");
        var left = PyString.FromString("alpha", context.MemoryGovernor, null);
        var right = PyString.FromString(" café😀 ", context.MemoryGovernor, null);
        var record = new Record();
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        context.Variables["record"] = record;
        Assert.Equal(0, Assert.Single(code.EntryPoint.CallSites, site => site.HasReceiver).ArgumentCount);
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
            Assert.True(pending.IsCompleted);
            await pending;
        }
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        Assert.Equal(2, record.Values.Count);
        Assert.Same(left, record.Values[0]);
        Assert.Equal("café😀", Assert.IsType<PyString>(record.Values[1]).AsString());
        var caches = (Array)record.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(record.Frame)!;
        Assert.All(caches.Cast<object?>(), value => Assert.Null(value));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryCallsPreserveResultsAndCleanup(bool asynchronous)
    {
        {
            var (code, context) = Prepare("return text.strip()\n", new LythonRunOptions { });
            var text = PyString.FromString("word", context.MemoryGovernor, null);
            context.Variables["text"] = text;
                Assert.Same(text, asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedTextNeedsNoNewStorageAndChangedTextPreflights(bool asynchronous)
    {
        const long limit = 64 * 1024;
        foreach (var changed in new[] { false, true })
        {
            var (code, context) = Prepare("fill()\nreturn text.strip()\n", new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
            var text = PyString.FromString(changed ? " word " : "word", context.MemoryGovernor, null);
            context.Variables["text"] = text;
            var baseline = context.MemoryGovernor.CurrentAccountedBytes;
            using (var padding = new BudgetPad(limit, text))
            {
                context.Variables["fill"] = padding;
                if (changed)
                {
                    var error = asynchronous
                        ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                        : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
                    Assert.Equal("MemoryError", error.ExceptionType);
                    Assert.Equal(" word ", text.AsString());
                }
                else Assert.Same(text, asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
            }
            Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, LythonRunOptions? options = null)
    {
        var frontend = LythonFrontend.Compile(source);
        return (ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())), new(new MockLythonHost(), options ?? new()));
    }

    private sealed class BudgetPad(long limit, PyString result) : LythonRuntime.ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _reservation;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            // Apply pressure after the VM buffer is admitted, isolating strip's
            // allocation preflight from invocation working-storage admission.
            _reservation = context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes, span);
            return result;
        }
        public void Dispose() => _reservation?.Dispose();
    }

    private sealed class Record : LythonRuntime.ICallable
    {
        internal List<object> Values { get; } = [];
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = context.CurrentExecutableFrame;
            Assert.True(Frame!.TryResolveOwnLocal("result", out var value));
            Values.Add(value);
            return PyNone.Instance;
        }
    }
}
