using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CounterRankingAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SortingAdmitsBackingBeforeMaterializationAndCleansUp(bool render)
    {
        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var operationOwner = operation.CreateScope())
        {
            var counter = new PyCounter(new MemoryGovernor(inputOwner));
            for (var i = 0; i < 25000; i++) counter.SetItem(new BigInteger(i), new BigInteger(i));
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = operationOwner });
            var inputBaseline = inputs.ReservedBytes;
            Assert.True(LythonRuntime.CounterMembers.TryGetMember(counter, "most_common", out var member));
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (render) counter.RenderPython(new PyRenderingContext(context));
                else ((LythonRuntime.ICallable)member).Invoke([], new LythonSourceSpan(0, 0, 0, 0), context);
            });
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<262144);
#endif
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(25000, counter.Count);
            Assert.Equal(inputBaseline, inputs.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, operation.ReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }
}
