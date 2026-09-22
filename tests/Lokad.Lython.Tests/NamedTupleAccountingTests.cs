using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: guest-constructed namedtuple instances commit backing storage at the
/// tuple slot rate (32 plus 16 per field); engine-owned instances without a
/// governor stay free.
/// </summary>
public sealed class NamedTupleAccountingTests
{
    [Fact]
    public void GovernedInstanceCommitsBackingExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var type = new PyNamedTupleType("P", ["x", "y"]);
        var instance = new PyNamedTupleObject(
            type,
            [new BigInteger(1), new BigInteger(2)],
            context.MemoryGovernor,
            span);
        Assert.Equal(2, instance.Count);
        // Plus two 64B coupons for the adopted int fields (N06).
        Assert.Equal(32L + (16L * 2) + 2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedCouponRefundsBacking()
    {
        // A denied construction coupon refunds the orphaned backing: nothing
        // stays committed and the record never publishes.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var type = new PyNamedTupleType("P", ["x", "y"]);
        var items = new object[] { new BigInteger(1), new BigInteger(2) };
        var budget = 32L + (16L * 2) + 64L - 1;
        var tight = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.Throws<LythonRuntimeException>(() => new PyNamedTupleObject(type, items, tight.MemoryGovernor, span));
        Assert.Equal(0, tight.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void UngovernedInstanceStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var type = new PyNamedTupleType("P", ["x", "y"]);
        _ = new PyNamedTupleObject(type, [new BigInteger(1), new BigInteger(2)]);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}