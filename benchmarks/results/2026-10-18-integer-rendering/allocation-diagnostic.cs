using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class IntegerRenderingAllocationDiagnostic
{
    [Fact]
    public void CaptureCounters()
    {
        var rows = new List<object>();
        foreach (var value in new[] { new BigInteger(42), new BigInteger(123456), new BigInteger(long.MaxValue) })
        {
            object input = value;
            var expected = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(4 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var rendering = new PyRenderingContext(context);
                    var total = 0;
                    PyString? result = null;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        result = PyRendering.ToInterpolatedPyString(input, rendering);
                        total += result.Utf8Bytes.Length;
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(expected.Length * 128, total);
                    Assert.Equal(expected, result!.AsString());
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            rows.Add(new { value = expected, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_INTEGER_RENDER_COUNTER_ALLOCATION")!, JsonSerializer.Serialize(rows));
    }

    [Fact]
    public void Capture()
    {
        var rows = new List<object>();
        foreach (var mode in new[] { 0, 1, 2, 3 })
        foreach (var large in mode == 3 ? new[] { false } : new[] { false, true })
        {
            object value = mode == 3 ? 12.5 : large ? (object)(BigInteger.One << 80) : new BigInteger(-123456);
            var expected = mode == 3 ? "12.5" : large ? "1208925819614629174706176" : "-123456";
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(4 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var rendering = new PyRenderingContext(context);
                    var total = 0;
                    PyString? result = null;
                    var start = GC.GetAllocatedBytesForCurrentThread();
                    for (var i = 0; i < 128; i++)
                    {
                        result = mode switch
                        {
                            0 => PyRendering.ToInterpolatedPyString(value, rendering),
                            1 => PyRendering.ToPythonPyString(value, rendering),
                            _ => PyRendering.ToReprPyString(value, rendering),
                        };
                        total += result.Utf8Bytes.Length;
                    }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                    Assert.Equal(expected.Length * 128, total);
                    Assert.Equal(expected, result!.AsString());
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    if (sample >= 0) samples.Add(allocated);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            rows.Add(new { mode, large, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_INTEGER_RENDER_ALLOCATION")!, JsonSerializer.Serialize(rows));
    }
}
