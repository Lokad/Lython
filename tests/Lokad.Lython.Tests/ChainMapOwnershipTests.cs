using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ChainMapOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void NewChildAdmitsItsOwnTableBesideRetainedInput()
    {
        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var operationOwner = operation.CreateScope())
        {
            var inputGovernor = new MemoryGovernor(inputOwner);
            var map = new PyDict(inputGovernor, Span);
            var chain = new PyChainMap(Enumerable.Repeat<IChainMapSource>(map, 4096));
            var inputBaseline = inputs.ReservedBytes;
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = operationOwner });
            Assert.True(chain.TryGetMember("new_child", out var member));
            var error = Assert.Throws<LythonRuntimeException>(() => ((LythonRuntime.ICallable)member).Invoke([], Span, context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(4096, chain.Maps.Count());
            Assert.Equal(inputBaseline, inputs.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }

    [Fact]
    public void CountingAndMappingConsumersReturnPrivateScratch()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var map = new PyDict(context.MemoryGovernor, Span);
            for (var i = 0; i < 128; i++) map.SetItem(new BigInteger(i), BigInteger.One);
            var chain = new PyChainMap([map, map], context, Span);
            var baseline = budget.ReservedBytes;
            for (var pass = 0; pass < 3; pass++)
            {
                Assert.Equal(128, chain.Count);
                Assert.Equal(128, LythonRuntime.EnumerateMappingItems(chain, context, Span).Count());
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ViewCountAdmitsItsMergeWhileBorrowingAnExistingMap()
    {
        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var operationOwner = operation.CreateScope())
        {
            var map = new PyDict(new MemoryGovernor(inputOwner), Span);
            for (var i = 0; i < 1000; i++) map.SetItem(new BigInteger(i), BigInteger.One);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = operationOwner });
            var chain = new PyChainMap([map], context, Span);
            var baseline = operation.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => new ChainMapKeysView(chain).Count);
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(baseline, operation.ReservedBytes);
            Assert.Equal(1000, map.Count);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }
}
