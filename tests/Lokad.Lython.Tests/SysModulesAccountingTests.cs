using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.Tests;
public sealed class SysModulesAccountingTests
{
    [Fact]
    public void RegistryKeysHaveAnOwnerAndRepeatedReadsDoNotAccumulate()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var registry = context.State.ImportedModules;
            registry.SetStringItem("helper", PyNone.Instance, context.MemoryGovernor, null);
            var key = Assert.IsType<PyString>(Assert.Single(registry).Key);
            Assert.Same(context.MemoryGovernor, key.OwnerMemoryGovernor);
            var before = budget.ReservedBytes;
            for (var i = 0; i < 100; i++)
                Assert.True(registry.TryGetStringValue("helper", context.MemoryGovernor, null, context, out _));
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
