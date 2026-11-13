using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinSlotOwnerTests
{
    // Checked independently with isolated CPython 3.13.
    private const string Source = """
        names = ['__init__','__getattribute__','__setattr__','__delattr__',
                 '__format__','__dir__','__eq__','__ne__','__lt__','__le__',
                 '__gt__','__ge__','__str__','__repr__','__hash__']
        for name in names:
            original = getattr(object, name)
            assert original.__objclass__ is object, name + ': original owner'
            class Defined:
                slot = original
                static = staticmethod(original)
                classed = classmethod(original)
                attribute = property(original)
            class Assigned: pass
            Assigned.slot = original
            Assigned.static = staticmethod(original)
            Assigned.classed = classmethod(original)
            Assigned.attribute = property(original)
            assert original.__objclass__ is object, name + ': alias changed owner'
            assert Defined.slot is original
            assert Assigned.slot is original
            assert Defined.slot.__objclass__ is object
            assert Assigned.slot.__objclass__ is object
            assert Defined.static is original
            assert Assigned.static is original
        class Value:
            __init__ = object.__init__
            __repr__ = object.__repr__
        instance = Value()
        assert isinstance(instance, Value)
        assert instance.__repr__.__objclass__ is object
        print(len(names))
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AliasesPreserveBuiltinSlotDefinitionAndBudgetReuse(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("15\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
