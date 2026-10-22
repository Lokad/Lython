using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.Tests;

public sealed class NamespaceAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    [Fact]
    public void LargeDecodedAttributeNameIsAdmittedBeforeMutation()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var instance = new PyInstance(new PyType("C", [], new()), context.MemoryGovernor, Span);
            var view = instance.GetNamespace(context, Span);
            var key = PyString.FromString(new string('x', 8192));
            var baseline = budget.ReservedBytes;
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => view.SetItem(key, PyNone.Instance)).ExceptionType);
            Assert.Equal(0, instance.OwnAttributeCount);
            Assert.Empty(view);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
    [Fact]
    public void ViewInsertionRefusesBeforeAttributeMutation()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var instance = new PyInstance(new PyType("C", [], new()), context.MemoryGovernor, Span);
            var view = instance.GetNamespace(context, Span);
            Assert.True(owner.TryReserve(4096 - budget.ReservedBytes - 1, out var pin));
            using (pin)
            {
                var baseline = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => view.SetStringItem("value", PyNone.Instance, context.MemoryGovernor, Span));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(0, instance.OwnAttributeCount);
                Assert.Empty(view);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
    [Fact]
    public void AttributeDeletionRetainsCapacityAndReinsertionReusesIt()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var instance = new PyInstance(new PyType("C", [], new()), context.MemoryGovernor, Span);
            instance.SetAttribute("value", PyNone.Instance);
            var view = instance.GetNamespace(context, Span);
            var key = Assert.Single(view.Keys);
            var baseline = budget.ReservedBytes;
            for (var i = 0; i < 1000; i++)
            {
                Assert.True(instance.RemoveAttribute("value"));
                instance.SetAttribute("value", PyNone.Instance);
                Assert.Same(key, Assert.Single(view.Keys));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
