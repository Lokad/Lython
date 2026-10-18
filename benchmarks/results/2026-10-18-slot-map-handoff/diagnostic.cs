using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class SlotMapDiagnostic(ITestOutputHelper output)
{
    private static readonly string[] Pure = ["helper", "closure", "lambda", "generator", "genexpr"];
    private static readonly string[] All = ["csv", "text", "json", "file", "helper", "closure", "lambda", "generator", "genexpr", "empty-control"];
    public static TheoryData<string, bool> CompilationCases => Cases(All);
    public static TheoryData<string, bool> WarmCases => Cases(Pure);
    private static TheoryData<string, bool> Cases(string[] shapes)
    {
        var cases = new TheoryData<string, bool>();
        foreach (var shape in shapes) foreach (var mode in new[] { false, true }) cases.Add(shape, mode);
        return cases;
    }
    private static string Source(string shape) => shape switch
    {
        "helper" => "def add(a,b):return a+b\nreturn add(5,7)",
        "closure" => "def make(offset):\n def read(value):return value+offset\n return read\nf=make(7)\nreturn f(5)",
        "lambda" => "def make(offset):return lambda value:value+offset\nf=make(7)\nreturn f(5)",
        "generator" => "def values(offset):\n yield offset\n yield offset+1\nreturn sum(values(7))",
        "genexpr" => "def total(offset):return sum(i+offset for i in range(3))\nreturn total(3)",
        "empty-control" => "pass",
        _ => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", shape switch
        { "csv" => "csv-summary-helpers.py", "text" => "text-summary-helpers.py", "json" => "json-summary-helpers.py", "file" => "file-manifest-helpers.py", _ => throw new InvalidOperationException() }))
    };
    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    [Theory]
    [MemberData(nameof(CompilationCases))]
    public void Compilation(string shape, bool publicBoundary)
    {
        var source = Source(shape); var code = Compile(source); var fingerprint = Fingerprint(code);
        var maps = Maps(code).ToArray(); var samples = new List<long>();
        var budget = new LythonMemoryBudget(16 * 1024 * 1024); var engine = new LythonEngine();
        for (var sample = -2; sample < 8; sample++)
        {
            ExecutableCodeObject? last = null; var before = GC.GetAllocatedBytesForCurrentThread();
            for (var call = 0; call < 8; call++)
            {
                if (publicBoundary)
                {
                    using var compiled = engine.Compile(source, budget);
                    Assert.True(compiled.IsValid);
                }
                else last = Compile(source);
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, budget.ReservedBytes);
            if (!publicBoundary) Assert.Equal(fingerprint, Fingerprint(last!));
            if (sample >= 0) samples.Add(bytes);
        }
        output.WriteLine("SLOT_MAP_COMPILE:" + JsonSerializer.Serialize(new { shape, publicBoundary, callsPerSample = 8, sourceLength = source.Length, sourceSha256 = Hash(source), fingerprint, maps, samples, compileAllowance = RuntimeMemoryEstimates.EstimateCompilationBytes(source.Length) }));
    }

    [Theory]
    [MemberData(nameof(WarmCases))]
    public async Task Warm(string shape, bool asynchronous)
    {
        var code = Compile(Source(shape)); var fingerprint = Fingerprint(code);
        var budget = new LythonMemoryBudget(16 * 1024 * 1024); using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 10000000 });
        var samples = new List<long>(); var steps = new List<long>();
        for (var sample = -2; sample < 8; sample++)
        {
            var thread = Environment.CurrentManagedThreadId; var work = context.Limits.ExecutionStepCount;
            var before = GC.GetAllocatedBytesForCurrentThread(); object? value = null;
            for (var call = 0; call < 8; call++)
            {
                var pending = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : new ValueTask<object?>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                Assert.True(pending.IsCompletedSuccessfully); value = await pending;
            }
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            Assert.Equal(new BigInteger(shape == "generator" ? 15 : 12), value);
            Assert.Null(context.CurrentExecutableFrame); Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth); Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (sample >= 0) { samples.Add(bytes); steps.Add(context.Limits.ExecutionStepCount - work); }
        }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("SLOT_MAP_WARM:" + JsonSerializer.Serialize(new { shape, asynchronous, callsPerSample = 8, samples, steps, fingerprint }));
    }

    private sealed record Map(string path, string kind, int count, int capacity);
    private static IEnumerable<Map> Maps(ExecutableCodeObject code, string path = "module")
    {
        yield return Describe(code.LocalNameToSlot, "local"); yield return Describe(code.ClosureNameToSlot, "closure");
        var index = 0;
        foreach (var child in Children(code)) foreach (var map in Maps(child, path + "/" + index++ + ":" + child.Name)) yield return map;
        Map Describe(IReadOnlyDictionary<string, int> values, string kind)
        {
            var dictionary = Assert.IsType<Dictionary<string, int>>(values);
            return new(path, kind, dictionary.Count, dictionary.EnsureCapacity(0));
        }
    }
    private static IEnumerable<ExecutableCodeObject> Children(ExecutableCodeObject code)
    {
        foreach (var function in code.Functions) if (function.CodeObject is { } child) yield return child;
        foreach (var value in code.Constants)
            if (value is ExecutableCreateLambda { CodeObject: { } lambda }) yield return lambda;
            else if (value is ExecutableCreateGeneratorExpression generator) yield return generator.CodeObject;
    }
    private static string Fingerprint(ExecutableCodeObject code)
    {
        var text = new StringBuilder();
        text.Append(code.Name).Append('/').Append(code.SimpleReturn).Append('/').Append(code.RequiresLocalVariableMirroring);
        foreach (var pair in code.LocalNameToSlot) text.Append("L:").Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        foreach (var pair in code.ClosureNameToSlot) text.Append("C:").Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        foreach (var slot in code.CapturedLocalSlots) text.Append("capture:").Append(slot).Append(';');
        foreach (var block in code.Blocks) foreach (var op in block.Instructions)
            text.Append(op.OpCode).Append(':').Append(op.OpCode == ExecutableOpCode.Binary ? op.BinaryOperator.ToString() : "-").Append('@').Append(op.Span.Start).Append(',').Append(op.Span.Length).Append(';');
        foreach (var child in Children(code)) text.Append('[').Append(Fingerprint(child)).Append(']');
        return Hash(text.ToString());
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
