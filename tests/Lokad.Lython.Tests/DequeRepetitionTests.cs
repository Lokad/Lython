using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class DequeRepetitionTests
{
    private static readonly LythonSourceSpan Span = new(0,0,0,0);

    [Fact]
    public void RefusedRepetitionPreservesTheOriginalNodesAndValues()
    {
        var budget = new LythonMemoryBudget(640);
        using(var owner=budget.CreateScope())
        {
            var context=new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions { MemoryBudget=owner });
            var deque=new PyDeque(new object[] { BigInteger.One,new BigInteger(2),new BigInteger(3) },null,context.MemoryGovernor,Span);
            var first=Assert.IsType<LinkedList<object>>(deque.Iterate()).First;
            var baseline=budget.ReservedBytes;
            var error=Assert.Throws<LythonRuntimeException>(()=>deque.RepeatInPlace(2,Span,context));
            Assert.Equal("MemoryError",error.ExceptionType);
            Assert.Equal(new object[] { BigInteger.One,new BigInteger(2),new BigInteger(3) },deque.ToArray());
            Assert.Same(first,Assert.IsType<LinkedList<object>>(deque.Iterate()).First);
            Assert.Equal(baseline,budget.ReservedBytes);
            Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Fact]
    public void RepeatingOnceKeepsNodesWithoutAProportionalCopy()
    {
        var deque=new PyDeque(Enumerable.Repeat((object)BigInteger.One,25000));
        var first=Assert.IsType<LinkedList<object>>(deque.Iterate()).First;
        var before=GC.GetAllocatedBytesForCurrentThread();
        deque.RepeatInPlace(BigInteger.One,Span);
#if !DEBUG
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<65536);
#endif
        Assert.Same(first,Assert.IsType<LinkedList<object>>(deque.Iterate()).First);
        Assert.Equal(25000,deque.Count);
    }

    [Fact]
    public void BoundedRepetitionReusesOriginalNodesAndFundsOnlyGrowth()
    {
        var budget=new LythonMemoryBudget();
        using(var owner=budget.CreateScope())
        {
            var governor=new MemoryGovernor(owner);
            var deque=new PyDeque(new object[] { BigInteger.One,new BigInteger(2) },5,governor,Span);
            var original=Assert.IsType<LinkedList<object>>(deque.Iterate()).First;
            deque.RepeatInPlace(new BigInteger(long.MaxValue),Span);
            Assert.Equal(new object[] { new BigInteger(2),BigInteger.One,new BigInteger(2),BigInteger.One,new BigInteger(2) },deque.ToArray());
            Assert.Same(original,Assert.IsType<LinkedList<object>>(deque.Iterate()).First!.Next);
            Assert.Equal(128+5*128,budget.ReservedBytes);
            Assert.Equal(0,governor.CurrentReservedBytes);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
