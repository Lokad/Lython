using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JsonStringEmissionAllocationDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void Capture()
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json.dumps").Script!)).EntryPoint;
        var rows = new List<object>();
        foreach (var kind in new[] { "ascii32", "ascii128", "ascii129", "astral32", "dictionary16", "ascii4097", "integer" })
        {
            var samples = new List<long>();
            for (var sample = -4; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(16 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                    var payload = kind switch
                    {
                        "ascii32" => new string('a', 32), "ascii128" => new string('a', 128),
                        "ascii129" => new string('a', 129), "ascii4097" => new string('a', 4097),
                        "astral32" => string.Concat(Enumerable.Repeat("😀", 32)), _ => "",
                    };
                    object input = kind == "integer" ? new BigInteger(123456) : PyString.FromString(payload, context.MemoryGovernor, Span);
                    var expected = kind == "integer" ? "123456" : kind == "astral32"
                        ? "\"" + string.Concat(Enumerable.Repeat("\\ud83d\\ude00", 32)) + "\"" : "\"" + payload + "\"";
                    if (kind == "dictionary16")
                    {
                        var dict = new PyDict(context.MemoryGovernor, Span);
                        for (var i = 0; i < 16; i++) dict.SetItem(PyString.FromString("k" + i, context.MemoryGovernor, Span), new BigInteger(123456));
                        input = dict;
                        expected = "{" + string.Join(", ", Enumerable.Range(0, 16).Select(i => "\"k" + i + "\": 123456")) + "}";
                    }
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
                    var total = 0;
                    PyString? last = null;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        last = Assert.IsType<PyString>(callable.Invoke(arguments, Span, context));
                        total += last.Utf8Bytes.Length;
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(expected.Length * 128, total);
                    Assert.Equal(expected, last!.AsString());
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            rows.Add(new { kind, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_JSON_STRING_ALLOCATION")!, JsonSerializer.Serialize(rows));
    }
}
