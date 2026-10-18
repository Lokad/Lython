using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class SingleFstringDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Shapes = ["text", "number", "conversion", "custom", "dynamic", "nested", "multi-control", "empty-control", "lowered-control"];
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var result = new TheoryData<string, bool>();
            foreach (var shape in Shapes) foreach (var asynchronous in new[] { false, true }) result.Add(shape, asynchronous);
            return result;
        }
    }
    private static string Source(string shape) => shape switch
    {
        "conversion" => "return f'{value!s}'", "dynamic" => "return f'{value:{width}}'",
        "nested" => "return f'{f\"{value}\"}'", "multi-control" => "return f'A{value}Z'",
        "empty-control" => "return f''", _ => "return f'{value}'"
    };
    private static string Expected(string shape) => shape switch
    {
        "number" => "-12345", "dynamic" => "    12", "multi-control" => "Acaf\u00e9 \U0001f600Z",
        "empty-control" => "", _ => "caf\u00e9 \U0001f600"
    };
    private static Context Prepare(string shape, ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner, MaxExecutionSteps = 10000000,
            Globals = new Dictionary<string, object?> { ["value"] = shape == "number" ? new BigInteger(-12345) : shape == "dynamic" ? new BigInteger(12) : "caf\u00e9 \U0001f600", ["width"] = new BigInteger(6) }
        });
        if (shape == "custom")
            _ = LythonRuntime.ExecuteExecutableCodeObject(Compile("global value,rendered\nrendered=value\nclass Formatter:\n def __format__(self,spec):return rendered\nvalue=Formatter()"), context, default, null);
        return context;
    }
    private static LoweredScript Lower(string source) => LoweredScript.Lower(LythonFrontend.Compile(source).Script!);
    private static ExecutableCodeObject Compile(string source) => ExecutableScript.Compile(Lower(source)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Allocation(string shape, bool asynchronous)
    {
        var code = Compile(Source(shape)); var joins = code.Constants.OfType<ExecutableJoinFormattedParts>().Select(j => j.Count).ToArray();
        var lowered = shape == "lowered-control" ? Assert.IsType<LoweredReturnStatement>(Assert.Single(Lower(Source(shape)).Statements)).Expression : null;
        var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope(); var context = Prepare(shape, owner);
        var samples = new List<long>(); var steps = new List<long>(); var committed = new List<long>(); var aliases = new List<bool>();
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId; var work = context.Limits.ExecutionStepCount; var funding = context.MemoryGovernor.CurrentCommittedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread(); object? value = null;
            for (var call = 0; call < 8; call++)
            {
                if (lowered is null) { var pending = Run(code, context, asynchronous); Assert.True(pending.IsCompletedSuccessfully); value = await pending; }
                else if (asynchronous) { var pending = LythonRuntime.EvaluateLoweredExpressionAsync(lowered, context); Assert.True(pending.IsCompletedSuccessfully); value = await pending; }
                else value = LythonRuntime.EvaluateLoweredExpression(lowered, context);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId); Check(context);
            Assert.Equal(Expected(shape), Assert.IsType<PyString>(value).AsString());
            if (sample >= 0)
            {
                samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); committed.Add(context.MemoryGovernor.CurrentCommittedBytes - funding);
                aliases.Add(ReferenceEquals(value, context.Variables[shape == "custom" ? "rendered" : "value"]));
            }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("SINGLE_FSTRING_ALLOCATION:" + JsonSerializer.Serialize(new { shape, asynchronous, samples, steps, committed, aliases, joins, callsPerSample = 8, outputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Expected(shape)))) }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Fuel(bool asynchronous)
    {
        var records = new List<object>();
        foreach (var shape in new[] { "text", "number", "conversion", "custom", "dynamic", "nested", "multi-control", "empty-control" })
        foreach (var fuel in Enumerable.Range(0, 21).Append(128))
        {
            var code = Compile(Source(shape)); var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope(); var context = Prepare(shape, owner);
            context.Limits.ExecutionStepCount = 10000000 - fuel; object? value = null; LythonRuntimeException? error = null;
            try { value = await Run(code, context, asynchronous); } catch (LythonRuntimeException ex) { error = ex; }
            Check(context);
            records.Add(new { shape, fuel, value = value is PyString text ? text.AsString() : null, failure = error is null ? null : new { error.ExceptionType, error.Message, reason = error.Attribution?.Reason.ToString(), error.Span }, steps = context.Limits.ExecutionStepCount - (10000000 - fuel) });
            owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        }
        output.WriteLine("SINGLE_FSTRING_FUEL:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }
}
