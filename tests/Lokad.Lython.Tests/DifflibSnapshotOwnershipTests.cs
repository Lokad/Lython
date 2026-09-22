using Lokad.Lython.Runtime;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N05 white-box comment (same as before)
public sealed class DifflibSnapshotOwnershipTests
{
    private static (LythonRuntime.ExecutionContext context, LythonSourceSpan span) NewContext()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        return (context, new LythonSourceSpan(0, 0, 0, 0));
    }

    [Fact]
    public void ViewsDoNotJoinMatcherCoupon()
    {
        var (context, span) = NewContext();
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, new List<object> { new BigInteger(1), new BigInteger(2) }, new List<object> { new BigInteger(1), new BigInteger(2) }, false, span, context);
        var beforeMatcher = matcher.CommittedStorageBytes;
        var beforeGovernor = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(matcher.TryGetMember("b2j", out var view));
        Assert.NotNull(view);
        Assert.Equal(beforeMatcher, matcher.CommittedStorageBytes);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > beforeGovernor);
        var governorAfterFirst = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(matcher.TryGetMember("b2j", out var again));
        Assert.Same(view, again);
        Assert.Equal(governorAfterFirst, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void FailedSetSeq2PreservesExactCharges()
    {
        var (context, span) = NewContext();
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, new List<object> { new BigInteger(1), new BigInteger(2) }, new List<object> { new BigInteger(3), new BigInteger(4) }, false, span, context);
        var beforeMatcher = matcher.CommittedStorageBytes;
        var beforeGovernor = context.MemoryGovernor.CurrentCommittedBytes;
        var ex = Assert.Throws<LythonRuntimeException>(() => matcher.SetSeq2(new List<object> { new List<object> { new BigInteger(1) } }, span, context));
        Assert.Equal("TypeError", ex.ExceptionType);
        Assert.Equal(beforeMatcher, matcher.CommittedStorageBytes);
        Assert.Equal(beforeGovernor, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(matcher.TryGetMember("b", out var b));
        Assert.NotNull(b);
    }

    [Fact]
    public void ConstructorFailureReleasesPartialCharges()
    {
        var (context, span) = NewContext();
        var beforeGovernor = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Throws<LythonRuntimeException>(() => new LythonRuntime.DifflibSequenceMatcherObject(null, new List<object> { new BigInteger(1) }, new List<object> { new List<object> { new BigInteger(1) } }, false, span, context));
        Assert.Equal(beforeGovernor, context.MemoryGovernor.CurrentCommittedBytes);
    }
}
