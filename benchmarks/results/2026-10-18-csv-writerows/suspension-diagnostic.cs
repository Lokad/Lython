using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvWriterowsSuspensionDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public async Task Capture()
    {
        var factoryCode = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(
            "def cells():\n yield 'alpha'\n yield 'beta'\nreturn cells").Script!)).EntryPoint;
        var records = new List<object>();
        foreach (var generatorRow in new[] { false, true })
        {
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var sink = new Sink();
                    var writer = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), sink, context.MemoryGovernor, Span);
                    Assert.True(LythonRuntime.CsvWriterMembers.TryGetMember(writer, "writerows", out var raw));
                    var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(raw);
                    object row;
                    if (generatorRow)
                    {
                        var factory = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(factoryCode, context, default, null));
                        row = factory.Invoke([], Span, context);
                    }
                    else row = new PyList([PyString.FromString("alpha", context.MemoryGovernor, Span), PyString.FromString("beta", context.MemoryGovernor, Span)], context.MemoryGovernor, Span);
                    var rows = new PyList([row], context.MemoryGovernor, Span);
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(rows)];
                    var thread = Environment.CurrentManagedThreadId;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    var pending = callable.InvokeAsync(arguments, Span, context);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    var task = pending.AsTask();
                    try
                    {
                        Assert.Equal(thread, Environment.CurrentManagedThreadId);
                        Assert.False(task.IsCompleted);
                        Assert.Equal(1, sink.Calls);
                        Assert.Same(context.MemoryGovernor, sink.Last!.OwnerMemoryGovernor);
                        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                        Assert.True(budget.ReservedBytes > 0);
                        sink.Release.SetResult(PyNone.Instance);
                        Assert.Same(PyNone.Instance, await task.WaitAsync(TimeSpan.FromSeconds(5)));
                    }
                    finally
                    {
                        sink.Release.TrySetResult(PyNone.Instance);
                        await task.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    Assert.Equal("alpha,beta\n", sink.Last.AsString());
                    Assert.Empty(writer.Rows);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { generatorRow, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_CSV_SUSPENSION_ALLOCATION")!, JsonSerializer.Serialize(records));
    }

    private sealed class Sink : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource<object> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal PyString? Last;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Async diagnostic only.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Last = (PyString)arguments[0].Value;
            return new ValueTask<object>(Release.Task);
        }
    }
}
