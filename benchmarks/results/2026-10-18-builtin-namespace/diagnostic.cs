using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ModuleNamespaceAllocationDiagnostic(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MeasureWarmContextConstruction(bool child)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var host = new MockLythonHost();
        using var parentOwner = budget.CreateScope();
        var parent = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = parentOwner });
        var allocations = new List<long>();
        for (var index = 0; index < 34; index++)
        {
            using (var owner = budget.CreateScope())
            {
                var options = new LythonRunOptions { MemoryBudget = owner };
                var before = GC.GetAllocatedBytesForCurrentThread();
                var context = child ? new LythonRuntime.ExecutionContext(parent) : new LythonRuntime.ExecutionContext(host, options);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(103, context.State.BuiltinVariables.Count);
                foreach (var name in new[] { "int", "float", "object" })
                {
                    if (child) Assert.Same(parent.State.BuiltinVariables[name], context.State.BuiltinVariables[name]);
                    else Assert.NotSame(parent.State.BuiltinVariables[name], context.State.BuiltinVariables[name]);
                }
                Assert.Same(context.State.BuiltinVariables["OSError"], context.State.BuiltinVariables["IOError"]);
                if (index >= 2) allocations.Add(allocated);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        parentOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("MODULE_NAMESPACE_ALLOCATIONS:" + JsonSerializer.Serialize(new { child, allocations }));
    }
}
