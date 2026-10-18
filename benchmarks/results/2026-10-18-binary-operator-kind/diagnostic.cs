using System.Numerics;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class BinaryKindDiagnostic(ITestOutputHelper output)
{
    public static TheoryData<string, bool> WarmCases
    {
        get
        {
            var result = new TheoryData<string, bool>();
            foreach (var shape in Shapes)
            foreach (var asynchronous in new[] { false, true }) result.Add(shape, asynchronous);
            return result;
        }
    }
    private static readonly string[] Shapes = ["binary", "mixed", "comparison", "helper", "augmented-control", "unary-control"];
    private static string Source(string shape) => shape switch
    {
        "binary" => "total=0\nfor n in range(64):total=total+n\nreturn total",
        "mixed" => "total=0\nfor n in range(64):total=(total+n)*1.0\nreturn total",
        "comparison" => "total=0\nfor n in range(64):\n if 0<=n<32:total=total+1\nreturn total",
        "helper" => "def add(a,b):return a+b\nreturn add",
        "augmented-control" => "total=0\nfor n in range(64):total+=n\nreturn total",
        "unary-control" => "total=0\nfor n in range(64):total+=-n\nreturn total",
        _ => throw new InvalidOperationException()
    };

    [Theory]
    [MemberData(nameof(WarmCases))]
    public async Task Warm(string shape, bool asynchronous)
    {
        var code = Compile(Source(shape)); var fingerprint = Fingerprint(code);
        var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000 });
        LythonRuntime.ICallable? helper = shape == "helper" ? Assert.IsAssignableFrom<LythonRuntime.ICallable>(await Run(code, context, asynchronous)) : null;
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(new BigInteger(5)), CallArgumentValue.Positional(new BigInteger(7))];
        var samples = new List<long>(); var steps = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var work = context.Limits.ExecutionStepCount; var thread = Environment.CurrentManagedThreadId;
            var before = GC.GetAllocatedBytesForCurrentThread(); object? value = null;
            for (var call = 0; call < 8; call++)
            {
                if (helper is null)
                {
                    var pending = Run(code, context, asynchronous);
                    Assert.True(pending.IsCompletedSuccessfully); value = await pending;
                }
                else if (asynchronous)
                {
                    var pending = helper.InvokeAsync(arguments, new(0, 1, 1, 1), context);
                    Assert.True(pending.IsCompletedSuccessfully); value = await pending;
                }
                else value = helper.Invoke(arguments, new(0, 1, 1, 1), context);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            CheckValue(shape, value); Check(context);
            if (sample >= 0) { samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("BINARY_KIND_WARM:" + JsonSerializer.Serialize(new { shape, asynchronous, callsPerSample = 8, samples, steps, fingerprint }));
    }

    [Theory]
    [InlineData("binary")] [InlineData("mixed")] [InlineData("comparison")]
    [InlineData("helper")] [InlineData("augmented-control")] [InlineData("unary-control")]
    public void Compilation(string shape)
    {
        var source = Source(shape); var expected = Fingerprint(Compile(source)); var samples = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); ExecutableCodeObject? last = null;
            for (var call = 0; call < 8; call++) last = Compile(source);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(expected, Fingerprint(last!)); if (sample >= 0) samples.Add(bytes);
        }
        output.WriteLine("BINARY_KIND_COMPILE:" + JsonSerializer.Serialize(new { shape, callsPerSample = 8, samples, fingerprint = expected }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Boundaries(bool asynchronous)
    {
        var records = new List<object>();
        foreach (var shape in new[] { "binary", "mixed", "comparison", "augmented-control", "unary-control", "item-error" })
        foreach (var fuel in new[] { 1, 4, 8, 16, 32, 64, 128, 1024 })
        {
            var budget = new LythonMemoryBudget(); using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000 });
            context.Limits.ExecutionStepCount = 10000000 - fuel;
            object? value = null; string? exception = null, message = null, reason = null; LythonSourceSpan? span = null;
            try { value = await Run(Compile(shape == "item-error" ? "value=1\nreturn value+'bad'" : Source(shape)), context, asynchronous); }
            catch (LythonRuntimeException error) { exception = error.ExceptionType; message = error.Message; reason = error.Attribution?.Reason.ToString(); span = error.Span; }
            Check(context); owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
            records.Add(new { shape, fuel, value = value?.ToString(), exception, message, reason, span, steps = context.Limits.ExecutionStepCount - (10000000 - fuel) });
        }
        output.WriteLine("BINARY_KIND_BOUNDARIES:" + JsonSerializer.Serialize(new { asynchronous, records }));
    }

    private static ExecutableCodeObject Compile(string source) => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context) : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    private static string Fingerprint(ExecutableCodeObject code)
    {
        var text = new StringBuilder();
        foreach (var block in code.Blocks)
        foreach (var op in block.Instructions)
            text.Append(op.OpCode).Append(':').Append(op.OpCode == ExecutableOpCode.Binary ? op.BinaryOperator.ToString() : "-")
                .Append('@').Append(op.Span.Start).Append(',').Append(op.Span.Length).Append(';');
        foreach (var function in code.Functions) if (function.CodeObject is { } body) text.Append('[').Append(Fingerprint(body)).Append(']');
        return text.ToString();
    }
    private static void CheckValue(string shape, object? value)
    {
        if (shape == "mixed") Assert.Equal(2016d, Assert.IsType<double>(value));
        else Assert.Equal(new BigInteger(shape switch { "comparison" => 32, "helper" => 12, "unary-control" => -2016, _ => 2016 }), value);
    }
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
