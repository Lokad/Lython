using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvWriterowAllocationDiagnostic
{
    [Fact]
    public async Task Capture()
    {
        var records = new List<object>();
        foreach (var exposed in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        {
            var source = (exposed ? "method=writer.writerow\n" : "") + "for i in range(128):\n " + (exposed ? "method" : "writer.writerow") + "(row)";
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var sink = new Sink();
                    var writer = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), sink, context.MemoryGovernor, new(0, 0, 1, 1));
                    context.Variables["writer"] = writer;
                    context.Variables["row"] = new PyList([PyString.FromString("alpha", context.MemoryGovernor, null), PyString.FromString("beta", context.MemoryGovernor, null)], context.MemoryGovernor, null);
                    var thread = Environment.CurrentManagedThreadId;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    if (asynchronous)
                    {
                        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        Assert.True(pending.IsCompletedSuccessfully);
                        await pending;
                    }
                    else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(thread, Environment.CurrentManagedThreadId);
                    Assert.Equal(128, sink.Calls);
                    Assert.Equal(128 * 11, sink.Bytes);
                    Assert.Equal("alpha,beta\n", sink.Last!.AsString());
                    Assert.Same(context.MemoryGovernor, sink.Last.OwnerMemoryGovernor);
                    Assert.Empty(writer.Rows);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Null(context.CurrentExecutableFrame);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { exposed, asynchronous, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_CSV_WRITEROW_ALLOCATION")!, JsonSerializer.Serialize(records));
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
