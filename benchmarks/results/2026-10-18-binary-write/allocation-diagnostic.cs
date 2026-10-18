using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Binary = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.BinaryFileHandle;
using Text = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class BinaryWriteAllocationDiagnostic
{
    [Fact]
    public async Task Capture()
    {
        var records = new List<object>();
        foreach (var binary in new[] { true, false })
        foreach (var exposed in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        {
            var source = (exposed ? "method=file.write\n" : "") + "for i in range(128):\n " + (exposed ? "method" : "file.write") + "(value)";
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
            var samples = new List<long>();
            long steps = -1;
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var host = new MockLythonHost();
                    var context = new Context(host, new LythonRunOptions { MemoryBudget = owner });
                    var binaryFile = binary ? await Binary.OpenAsync("/out", LythonRuntime.TextFileOperation.Write, context, new(0, 0, 1, 1), false) : null;
                    var textFile = binary ? null : Text.ForWrite("/out", context);
                    context.Variables["file"] = binary ? binaryFile! : textFile!;
                    context.Variables["value"] = binary ? new PyBytes(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), context.MemoryGovernor)
                        : PyString.FromString("alpha", context.MemoryGovernor, null);
                    context.Limits.ExecutionStepCount = 0;
                    try
                    {
                        var thread = Environment.CurrentManagedThreadId;
                        var start = GC.GetAllocatedBytesForCurrentThread();
                        if (asynchronous)
                        {
                            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                            Assert.True(pending.IsCompletedSuccessfully);
                            await pending;
                        }
                        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                        Assert.Equal(thread, Environment.CurrentManagedThreadId);
                        Assert.Null(context.CurrentExecutableFrame);
                        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                        if (steps < 0) steps = context.Limits.ExecutionStepCount;
                        Assert.Equal(steps, context.Limits.ExecutionStepCount);
                        if (sample >= 0) samples.Add(allocated);
                    }
                    finally
                    {
                        binaryFile?.Exit();
                        textFile?.Exit();
                    }
                    if (binary) Assert.Equal(Enumerable.Range(0, 128 * 32).Select(i => (byte)(i % 32)), host.ReadBytes("/out"));
                    else Assert.Equal(string.Concat(Enumerable.Repeat("alpha", 128)), host.ReadText("/out"));
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            records.Add(new { binary, exposed, asynchronous, steps, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_BINARY_WRITE_ALLOCATION")!, JsonSerializer.Serialize(records));
    }
}
