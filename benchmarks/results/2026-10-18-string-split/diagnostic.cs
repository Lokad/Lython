using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Temporary native allocation/control and exact work-boundary capture.
[Collection("AllocationSensitive")]
public sealed class StringSplitDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, "separator")]
    [InlineData(true, "separator")]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "omitted")]
    [InlineData(true, "omitted")]
    [InlineData(false, "exposed")]
    [InlineData(true, "exposed")]
    public async Task Allocation(bool asynchronous, string route)
    {
        var invocation = route switch
        {
            "separator" => "checksum+=len(text.split(','))",
            "none" => "checksum+=len(text.split(None))",
            "omitted" => "checksum+=len(text.split())",
            _ => "method=text.split\n checksum+=len(method(','))",
        };
        var code = Compile("checksum=0\nfor text in values:\n " + invocation + "\nreturn checksum");
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var separator = route is "separator" or "exposed" ? "," : " ";
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
            Assert.Equal(new BigInteger(384), Assert.IsType<BigInteger>(result));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (sample >= 0) samples.Add(allocated);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("SPLIT_ALLOCATION:" + JsonSerializer.Serialize(new { asynchronous, route, callsPerSample = 128, samples }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var records = new List<object>();
        foreach (var separator in new[] { "comma", "none", "empty", "invalid" })
        for (var fuel = 1; fuel <= 11; fuel++)
        {
            var budget = new LythonMemoryBudget();
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = fuel });
            context.Variables["text"] = PyString.FromString("café,😀", context.MemoryGovernor, null);
            context.Variables["separator"] = separator switch
            {
                "comma" => PyString.FromString(",", context.MemoryGovernor, null),
                "none" => PyNone.Instance,
                "empty" => PyString.FromString("", context.MemoryGovernor, null),
                _ => BigInteger.One,
            };
            string? result = null, exception = null, message = null, reason = null;
            LythonSourceSpan? span = null;
            try
            {
                var value = Assert.IsType<PyList>(await Execute(Compile("return text.split(separator)"), context, asynchronous));
                result = string.Join("|", value.Cast<PyString>().Select(s => s.AsString()));
            }
            catch (LythonRuntimeException error)
            {
                exception = error.ExceptionType; message = error.Message; span = error.Span; reason = error.Attribution?.Reason.ToString();
            }
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { separator, fuel, result, exception, message, reason, span, steps = context.Limits.ExecutionStepCount });
        }
        output.WriteLine("SPLIT_WORK:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
}
