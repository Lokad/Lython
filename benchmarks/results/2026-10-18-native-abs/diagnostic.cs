using System.Globalization;
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
public sealed class AbsoluteCallDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, "integer")]
    [InlineData(true, "integer")]
    [InlineData(false, "float")]
    [InlineData(true, "float")]
    [InlineData(false, "boolean")]
    [InlineData(true, "boolean")]
    [InlineData(false, "expanded")]
    [InlineData(true, "expanded")]
    [InlineData(false, "protocol")]
    [InlineData(true, "protocol")]
    public async Task Allocation(bool asynchronous, string route)
    {
        var code = Compile("total=0\nfor value in values:\n total+=" + (route == "expanded" ? "abs(*[value])" : "abs(value)") + "\nreturn total");
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        object protocol = new PyInstance(new PyType("Number", [], new Dictionary<string, object> { ["__abs__"] = new ConstantAbs() }));
        context.Variables["values"] = new PyList(Enumerable.Range(0, 128).Select(i => route switch
        {
            "float" => (object)-(double)(i + 1),
            "boolean" => i % 2 == 0,
            "protocol" => protocol,
            _ => new BigInteger(-i - 1),
        }), context.MemoryGovernor, null);
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
            var number = result is BigInteger integer ? (double)integer : Assert.IsType<double>(result);
            Assert.Equal(route == "boolean" ? 64d : route == "protocol" ? 896d : 8256d, number);
            CheckCleanup(context);
            if (sample >= 0) samples.Add(allocated);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("ABS_ALLOCATION:" + JsonSerializer.Serialize(new { asynchronous, route, callsPerSample = 128, samples }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Work(bool asynchronous)
    {
        var code = Compile("return abs(value)");
        var records = new List<object>();
        foreach (var kind in new[] { "negative", "positive", "float", "boolean", "text", "protocol" })
        for (var fuel = 1; fuel <= 11; fuel++)
        {
            var budget = new LythonMemoryBudget();
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = fuel });
            object input = kind switch
            {
                "negative" => new BigInteger(-7),
                "positive" => new BigInteger(7),
                "float" => -0.0d,
                "boolean" => true,
                "text" => PyString.FromString("bad"),
                _ => new PyInstance(new PyType("Number", [], new Dictionary<string, object> { ["__abs__"] = new ConstantAbs() })),
            };
            context.Variables["value"] = input;
            string? result = null, exception = null, message = null, reason = null;
            LythonSourceSpan? span = null;
            bool? identity = null;
            try
            {
                var value = await Execute(code, context, asynchronous);
                result = value is double number ? number.ToString("R", CultureInfo.InvariantCulture) + ":" + BitConverter.DoubleToInt64Bits(number)
                    : value is BigInteger integer ? integer.ToString(CultureInfo.InvariantCulture) : value?.ToString();
                identity = ReferenceEquals(input, value);
            }
            catch (LythonRuntimeException error)
            {
                exception = error.ExceptionType; message = error.Message; span = error.Span; reason = error.Attribution?.Reason.ToString();
            }
            CheckCleanup(context);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { kind, fuel, result, identity, exception, message, reason, span, steps = context.Limits.ExecutionStepCount });
        }
        output.WriteLine("ABS_WORK:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    private sealed class ConstantAbs : LythonRuntime.ICallable
    {
        private static readonly object Value = new BigInteger(7);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) => Value;
    }
    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
}
