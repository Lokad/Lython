using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeCsvRowConversionTests(ITestOutputHelper output)
{
    // Frozen on bca120a4 before changing native async conversion selection.
    private const string FrozenGuards = """{"empty-tuple":{"shape":"empty-tuple","steps":12,"outcome":"saved","writes":["\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:\n"]},"empty-list":{"shape":"empty-list","steps":12,"outcome":"saved","writes":["\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:\n"]},"list":{"shape":"list","steps":14,"outcome":"saved","writes":["a,a\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:a,a\n"]},"bad-list":{"shape":"bad-list","steps":74,"outcome":"TypeError:CSV rows must contain scalar values.","writes":[],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:"]},"bad-tuple":{"shape":"bad-tuple","steps":74,"outcome":"TypeError:CSV rows must contain scalar values.","writes":[],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:"]},"tuple":{"shape":"tuple","steps":14,"outcome":"saved","writes":["a,a\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:a,a\n"]},"wide-tuple":{"shape":"wide-tuple","steps":78,"outcome":"saved","writes":["a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a\n"]},"wide-list":{"shape":"wide-list","steps":78,"outcome":"saved","writes":["a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a\n"],"checkpoints":["-:","7,6,1,8:","7,15,1,8:","23,3,1,24:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","7,20,1,8:","0,27,1,1:a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a,a\n"]}}""";
    private const int MaximumSteps = 10000;
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly ExecutableCodeObject Code = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return writer.writerow(row)").Script!)).EntryPoint;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var shape in new[] { "list", "tuple", "empty-list", "empty-tuple", "wide-list", "wide-tuple", "bad-list", "bad-tuple" })
            yield return [asynchronous, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeRowsPreserveEveryCheckpointAndPublication(bool asynchronous, string shape)
    {
        long steps;
        string outcome;
        string[] writes;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, sink) = Prepare(owner, shape);
            try
            {
                Assert.Same(sink.ReturnValue, await Run(context, asynchronous));
                outcome = "saved";
                Assert.Single(sink.Writes);
                var expected = shape.StartsWith("empty", StringComparison.Ordinal) ? "\n"
                    : string.Join(',', Enumerable.Repeat("a", shape.StartsWith("wide", StringComparison.Ordinal) ? 65 : 2)) + "\n";
                Assert.Equal(expected, sink.Writes[0]);
            }
            catch (LythonRuntimeException error)
            {
                Assert.StartsWith("bad", shape);
                Assert.Equal("TypeError", error.ExceptionType);
                Assert.Equal("CSV rows must contain scalar values.", error.Message);
                Assert.Empty(sink.Writes);
                outcome = error.ExceptionType + ":" + error.Message;
            }
            steps = context.Limits.ExecutionStepCount;
            writes = sink.Writes.ToArray();
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var (context, sink) = Prepare(owner, shape);
                context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(context, asynchronous));
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                Assert.Equal(MaximumSteps + 1, context.Limits.ExecutionStepCount);
                var location = error.Span is { } p ? $"{p.Start},{p.Length},{p.Line},{p.Column}" : "-";
                checkpoints.Add(location + ":" + string.Join('|', sink.Writes));
                AssertUnwound(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        using var frozen = JsonDocument.Parse(FrozenGuards);
        var guard = frozen.RootElement.GetProperty(shape);
        Assert.Equal(guard.GetProperty("steps").GetInt64(), steps);
        Assert.Equal(guard.GetProperty("outcome").GetString(), outcome);
        Assert.Equal(guard.GetProperty("writes").EnumerateArray().Select(x => x.GetString()), writes);
        Assert.Equal(guard.GetProperty("checkpoints").EnumerateArray().Select(x => x.GetString()), checkpoints);
        output.WriteLine("NATIVE_CSV_GUARDS:" + JsonSerializer.Serialize(new { asynchronous, shape, steps, outcome, writes, checkpoints }));
    }

    private static (Context, Sink) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var sink = new Sink(context.MemoryGovernor);
        var writer = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), sink, context.MemoryGovernor, Span);
        var text = PyString.FromString("a", context.MemoryGovernor, Span);
        var count = shape.StartsWith("empty", StringComparison.Ordinal) ? 0
            : shape.StartsWith("wide", StringComparison.Ordinal) || shape.StartsWith("bad", StringComparison.Ordinal) ? 65 : 2;
        var cells = Enumerable.Repeat<object>(text, count).ToArray();
        if (shape.StartsWith("bad", StringComparison.Ordinal)) cells[^1] = new object();
        context.Variables["row"] = shape.EndsWith("tuple", StringComparison.Ordinal)
            ? new PyTuple(cells, context.MemoryGovernor, Span) : new PyList(cells, context.MemoryGovernor, Span);
        context.Variables["writer"] = writer;
        return (context, sink);
    }

    private static ValueTask<object?> Run(Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Sink(MemoryGovernor governor) : LythonRuntime.ICallable
    {
        internal readonly PyString ReturnValue = PyString.FromString("saved", governor, Span);
        internal readonly List<string> Writes = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var text = (PyString)arguments[0].Value;
            Assert.Same(governor, text.OwnerMemoryGovernor);
            Writes.Add(text.AsString());
            return ReturnValue;
        }
    }
}
