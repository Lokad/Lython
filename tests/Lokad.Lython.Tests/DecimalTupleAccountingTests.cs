using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: DecimalTuple values own their digit-tuple backing plus one wrapper slot;
/// the transient tuples inside formatting and comparison stay free.
// M05: adopted results additionally hold one 128 B pool entry each, plus tier
// growth where the shared pool crosses a backing boundary.
/// </summary>
public sealed class DecimalTupleAccountingTests
{
    [Fact]
    public void ConstructedTuplesCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var method = typeof(LythonRuntime).GetMethod("DecimalTupleCtor", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DecimalTupleCtor not found.");
        var digits = new PyTuple(new object[] { new BigInteger(1), new BigInteger(2), new BigInteger(3) });
        _ = method.Invoke(null, [new object[] { new BigInteger(0), digits, new BigInteger(-1) }, span, context]);
        // Plus three 64B coupons for the digits the governed digits tuple retains (N06).
        Assert.Equal(304L + 3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void AsTupleResultsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var member = LythonRuntime.DecimalMembers.TryGetMember(new PyDecimal(1.5m), "as_tuple", out var value)
            ? value
            : throw new InvalidOperationException("as_tuple member not found.");
        _ = ((LythonRuntime.ICallable)member).Invoke([], span, context);
        // Plus two 64B coupons for the digits the governed digits tuple retains (N06).
        Assert.Equal(288L + 2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
