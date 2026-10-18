using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JsonIntegerEmissionAllocationDiagnostic
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    [Fact]
    public void Capture()
    {
        var rows = new List<object>();
        foreach (var route in new[] { "dumps", "encode", "iterator" })
        foreach (var key in new[] { false, true })
        foreach (var kind in route == "dumps" ? new[] { "42", "123456", "long", "large", "float" } : new[] { "123456" })
        {
            var target = route switch { "dumps" => "json.dumps", "encode" => "json.JSONEncoder().encode", _ => "json.JSONEncoder().iterencode" };
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn " + target).Script!)).EntryPoint;
            object input = kind switch
            {
                "42" => new BigInteger(42), "123456" => new BigInteger(123456),
                "long" => new BigInteger(long.MaxValue), "large" => BigInteger.One << 80, _ => (object)1.5,
            };
            var text = kind switch { "long" => "9223372036854775807", "large" => "1208925819614629174706176", "float" => "1.5", _ => kind };
            var expected = key ? "{\"" + text + "\": null}" : text;
            var samples = new List<long>();
            for (var sample = -4; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(16 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                    object value = input;
                    if (key) { var dict = new PyDict(context.MemoryGovernor, null); dict.SetItem(input, PyNone.Instance); value = dict; }
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(value)];
                    var total = 0;
                    PyString? last = null;
                    var lastChunks = new List<PyString>(4);
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        var result = callable.Invoke(arguments, Span, context);
                        if (result is LythonRuntime.JsonEncodeIterator iterator)
                        {
                            while (iterator.TryMoveNext(out var chunk))
                            {
                                var rendered = (PyString)chunk;
                                total += rendered.Utf8Bytes.Length;
                                if (i == 127) lastChunks.Add(rendered);
                            }
                        }
                        else { last = (PyString)result; total += last.Utf8Bytes.Length; }
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(expected.Length * 128, total);
                    Assert.Equal(expected, last is null ? string.Concat(lastChunks.Select(x => x.AsString())) : last.AsString());
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            rows.Add(new { route, key, kind, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_JSON_INTEGER_ALLOCATION")!, JsonSerializer.Serialize(rows));
    }
}
