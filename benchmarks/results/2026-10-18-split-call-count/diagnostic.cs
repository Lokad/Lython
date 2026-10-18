using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class StringSplitCountDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, "separator-count")]
    [InlineData(true, "separator-count")]
    [InlineData(false, "none-count")]
    [InlineData(true, "none-count")]
    [InlineData(false, "single")]
    [InlineData(true, "single")]
    [InlineData(false, "omitted")]
    [InlineData(true, "omitted")]
    [InlineData(false, "exposed")]
    [InlineData(true, "exposed")]
    public async Task Allocation(bool asynchronous, string route)
    {
        var invocation = route switch
        {
            "separator-count" => "checksum+=len(text.split(',',1))",
            "none-count" => "checksum+=len(text.split(None,1))",
            "single" => "checksum+=len(text.split(','))",
            "omitted" => "checksum+=len(text.split())",
            _ => "method=text.split\n checksum+=len(method(',',1))",
        };
        var code = Compile("checksum=0\nfor text in values:\n " + invocation + "\nreturn checksum");
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var separator = route is "none-count" or "omitted" ? " " : ",";
        context.Variables["values"] = new PyList(Enumerable.Range(0, 128).Select(i =>
            (object)PyString.FromString("café" + separator + "😀" + separator + i, context.MemoryGovernor, null)), context.MemoryGovernor, null);
        var samples = new List<long>();
        for (var sample = -4; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var pending = Execute(code, context, asynchronous);
            Assert.True(pending.IsCompletedSuccessfully);
            var result = await pending;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Assert.Equal(new BigInteger(route is "single" or "omitted" ? 384 : 256), Assert.IsType<BigInteger>(result));
            CheckCleanup(context);
            if (sample >= 0) samples.Add(allocated);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("SPLIT_COUNT_ALLOCATION:" + JsonSerializer.Serialize(new { asynchronous, route, callsPerSample = 128, samples }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var code = Compile("return text.split(separator,count)");
        var records = new List<object>();
        foreach (var kind in new[] { "comma", "none-zero", "empty", "bad-count-before-separator", "large", "overflow", "protocol", "bad-protocol" })
        for (var fuel = 1; fuel <= 15; fuel++)
        {
            var budget = new LythonMemoryBudget();
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = fuel });
            var text = PyString.FromString("café,😀,tail", context.MemoryGovernor, null);
            context.Variables["text"] = text;
            context.Variables["separator"] = kind switch
            {
                "none-zero" => PyNone.Instance,
                "empty" => PyString.Empty,
                "bad-count-before-separator" => BigInteger.One,
                _ => PyString.FromString(","),
            };
            var callback = new IndexCallback(kind == "bad-protocol");
            context.Variables["count"] = kind switch
            {
                "none-zero" => BigInteger.Zero,
                "large" => new BigInteger(long.MaxValue),
                "overflow" => BigInteger.One << 100,
                "bad-count-before-separator" => PyString.FromString("bad"),
                "protocol" or "bad-protocol" => new PyInstance(new PyType("Count", [], new Dictionary<string, object> { ["__index__"] = callback })),
                _ => BigInteger.One,
            };
            string? result = null, exception = null, message = null, reason = null;
            LythonSourceSpan? span = null;
            bool[]? identities = null;
            try
            {
                var value = Assert.IsType<PyList>(await Execute(code, context, asynchronous));
                result = string.Join("|", value.Cast<PyString>().Select(s => s.AsString()));
                identities = value.Select(part => ReferenceEquals(part, text)).ToArray();
            }
            catch (LythonRuntimeException error)
            {
                exception = error.ExceptionType; message = error.Message; span = error.Span; reason = error.Attribution?.Reason.ToString();
            }
            CheckCleanup(context);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { kind, fuel, result, identities, exception, message, reason, span, calls = callback.Calls, steps = context.Limits.ExecutionStepCount });
        }
        output.WriteLine("SPLIT_COUNT_WORK:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    private sealed class IndexCallback(bool invalid) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            return invalid ? PyString.FromString("bad") : BigInteger.One;
        }
    }
    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
}
