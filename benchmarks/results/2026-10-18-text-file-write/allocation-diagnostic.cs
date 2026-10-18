using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextWriteAllocationDiagnostic
{
    [Fact]
    public async Task Capture()
    {
        var rows = new List<object>();
        foreach (var exposed in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        {
            var source = (exposed ? "call=file.write\n" : "") + "for i in range(128):\n " + (exposed ? "call" : "file.write") + "(value)";
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
            var samples = new List<long>();
            for (var sample = -8; sample < 8; sample++)
            {
                var budget = new LythonMemoryBudget(8 * 1024 * 1024);
                using (var owner = budget.CreateScope())
                {
                    var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                    var file = Handle.ForWrite("/out", context);
                    context.Variables["file"] = file;
                    context.Variables["value"] = PyString.FromString("a\n😀", context.MemoryGovernor, null);
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
                    Assert.Equal(128 * 6, file.Tell());
                    if (sample >= 0) samples.Add(allocated);
                    file.Exit();
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            rows.Add(new { exposed, asynchronous, samples });
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("LYTHON_TEXT_WRITE_ALLOCATION")!, JsonSerializer.Serialize(rows));
    }
}
