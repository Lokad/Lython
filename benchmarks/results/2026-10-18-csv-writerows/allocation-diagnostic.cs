using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvWriterowsAllocationDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public async Task Capture()
    {
        var factoryCode = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(
            "def cells():\n yield 'alpha'\n yield 'beta'\nreturn cells").Script!)).EntryPoint;
        var records = new List<object>();
        foreach (var generatorRows in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        {
            var samples = new List<long>();
            var steps = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 100000 });
                    var sink = new Sink();
                    var writer = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), sink, context.MemoryGovernor, Span);
                    Assert.True(LythonRuntime.CsvWriterMembers.TryGetMember(writer, "writerows", out var raw));
                    var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(raw);
                    object[] values;
                    if (generatorRows)
                    {
                        var factory = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(factoryCode, context, default, null));
                        values = Enumerable.Range(0, 128).Select(_ => factory.Invoke([], Span, context)).ToArray();
                    }
                    else
                    {
                        var row = new PyList([PyString.FromString("alpha", context.MemoryGovernor, Span), PyString.FromString("beta", context.MemoryGovernor, Span)], context.MemoryGovernor, Span);
                        values = Enumerable.Repeat<object>(row, 128).ToArray();
                    }
                    var rows = new PyList(values, context.MemoryGovernor, Span);
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(rows)];
                    var beforeSteps = context.Limits.ExecutionStepCount;
                    var thread = Environment.CurrentManagedThreadId;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    object result;
                    if (asynchronous)
                    {
                        var pending = callable.InvokeAsync(arguments, Span, context);
                        Assert.True(pending.IsCompletedSuccessfully);
                        result = await pending;
                    }
                    else result = callable.Invoke(arguments, Span, context);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(thread, Environment.CurrentManagedThreadId);
                    Assert.Same(PyNone.Instance, result);
                    Assert.Equal(128, sink.Calls);
                    Assert.Equal(128 * 11, sink.Bytes);
                    Assert.Equal("alpha,beta\n", sink.Last!.AsString());
                    Assert.Same(context.MemoryGovernor, sink.Last.OwnerMemoryGovernor);
                    Assert.Empty(writer.Rows);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Null(context.CurrentExecutableFrame);
                    if (sample >= 0)
                    {
                        samples.Add(allocated);
                        steps.Add(context.Limits.ExecutionStepCount - beforeSteps);
                    }
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { generatorRows, asynchronous, samples, steps });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_CSV_WRITEROWS_ALLOCATION")!, JsonSerializer.Serialize(records));
    }

    private sealed class Sink : LythonRuntime.ICallable
    {
        internal int Calls;
        internal int Bytes;
        internal PyString? Last;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Last = (PyString)arguments[0].Value;
            Calls++;
            Bytes += Last.Utf8Bytes.Length;
            return PyNone.Instance;
        }
    }
}
