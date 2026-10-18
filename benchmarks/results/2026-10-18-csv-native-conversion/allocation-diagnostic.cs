using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Cell = Lokad.Lython.Runtime.LythonRuntime.CsvCell;

namespace Lokad.Lython.Tests;

// Preparation only: copy into the test assembly after the preceding delivery
// qualifies. Isolates conversion from publication before any runtime proposal.
[Collection("AllocationSensitive")]
public sealed class NativeCsvConversionAllocationDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public async Task Capture()
    {
        var convertAsync = typeof(LythonRuntime.CsvWriterMembers)
            .GetMethod("ToCsvRowAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<object, LythonSourceSpan, Context, ValueTask<(Cell[] Cells, long ConvertedBytes)>>>();
        var records = new List<object>();
        foreach (var phase in new[] { "conversion", "publication", "complete-row" })
        foreach (var asynchronous in new[] { false, true })
        {
            var samples = new List<long>();
            var stepCounts = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 100000 });
                    var sink = new Sink();
                    var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, PyString.FromString("\""),
                        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
                    var writer = new LythonRuntime.CsvWriterObject(options, sink, context.MemoryGovernor, Span);
                    var input = new PyList([PyString.FromString("alpha", context.MemoryGovernor, Span), PyString.FromString("beta", context.MemoryGovernor, Span)], context.MemoryGovernor, Span);
                    var ready = LythonRuntime.CsvWriterMembers.ToCsvRow(input, Span, context, out var convertedBytes);
                    Assert.Equal(0, convertedBytes);
                    var committed = context.MemoryGovernor.CurrentCommittedBytes;
                    var steps = context.Limits.ExecutionStepCount;
                    Cell[]? last = null;
                    var total = 0;
                    var thread = Environment.CurrentManagedThreadId;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        if (phase == "conversion")
                        {
                            if (asynchronous)
                            {
                                var pending = convertAsync(input, Span, context);
                                Assert.True(pending.IsCompletedSuccessfully);
                                (last, convertedBytes) = await pending;
                            }
                            else last = LythonRuntime.CsvWriterMembers.ToCsvRow(input, Span, context, out convertedBytes);
                            total += last.Length;
                        }
                        else if (phase == "complete-row" && asynchronous)
                        {
                            var pending = LythonRuntime.CsvWriterMembers.WriteRowFromValueAsync(writer, input, Span, context);
                            Assert.True(pending.IsCompletedSuccessfully);
                            await pending;
                        }
                        else if (phase == "complete-row")
                            LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer, input, Span, context);
                        else if (asynchronous)
                        {
                            var pending = LythonRuntime.CsvWriterMembers.WriteRowAsync(writer, ready, Span, context, convertedBytes);
                            Assert.True(pending.IsCompletedSuccessfully);
                            await pending;
                        }
                        else LythonRuntime.CsvWriterMembers.WriteRow(writer, ready, Span, context, convertedBytes);
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(thread, Environment.CurrentManagedThreadId);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (phase == "conversion")
                    {
                        Assert.Equal(256, total);
                        Assert.Same(input[0], last![0].Text);
                        Assert.Equal(0, convertedBytes);
                        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                        Assert.Equal(0, sink.Calls);
                    }
                    else
                    {
                        Assert.Equal(128, sink.Calls);
                        Assert.Equal(128 * 11, sink.Bytes);
                        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
                    }
                    Assert.Empty(writer.Rows);
                    if (sample >= 0)
                    {
                        samples.Add(allocated);
                        stepCounts.Add(context.Limits.ExecutionStepCount - steps);
                    }
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { phase, asynchronous, samples, stepCounts });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_NATIVE_CSV_ALLOCATION")!, JsonSerializer.Serialize(records));
    }

    private sealed class Sink : LythonRuntime.ICallable
    {
        internal int Calls;
        internal int Bytes;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Bytes += ((PyString)arguments[0].Value).Utf8Bytes.Length;
            return PyNone.Instance;
        }
    }
}
