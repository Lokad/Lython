using Lokad.Lython.Runtime;
using System.Numerics;
using System.Reflection;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DifflibSnapshotOwnershipTests
{
    private delegate object[] Materializer(object value, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, out long charge);
    private static readonly Materializer Materialize = typeof(LythonRuntime)
        .GetNestedType("DifflibModule", BindingFlags.NonPublic)!
        .GetMethod("MaterializeGovernedSequence", BindingFlags.Public | BindingFlags.Static)!
        .CreateDelegate<Materializer>();

    [Theory]
    [InlineData(2, 150, true)]
    [InlineData(5, 200, true)]
    [InlineData(2, 160, false)]
    public void MaterializationAdmitsGrowthAndCopyBeforeAllocating(int count, long available, bool refused)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var sequence = new Sequence(count, context);
        Assert.True(budget.TryReserve(limit - budget.ReservedBytes - available, out var padding));
        using (padding)
        {
            if (refused)
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Materialize(sequence, new(0, 0, 0, 0), context, out _));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            }
            else
            {
                var result = Materialize(sequence, new(0, 0, 0, 0), context, out var charge);
                Assert.Equal(count, result.Length);
                Assert.Equal(before + charge, context.MemoryGovernor.CurrentCommittedBytes);
                foreach (var item in result) Assert.Same(PyNone.Instance, item);
            }
            Assert.True(sequence.Disposed);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Sequence(int count, LythonRuntime.ExecutionContext context) : IPyIterableValue
    {
        internal bool Disposed;
        public IEnumerable<object> Iterate()
        {
            try
            {
                for (var i = 0; i < count; i++)
                {
                    yield return PyNone.Instance;
                    Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 32 + 16L * (i + 1));
                }
            }
            finally { Disposed = true; }
        }
    }

    private static (LythonRuntime.ExecutionContext context, LythonSourceSpan span) NewContext()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null,regexProvider:Utf8RegexProvider.Instance);
        return (context, new LythonSourceSpan(0, 0, 0, 0));
    }

    [Fact]
    public void ViewsRemainFundedIndependentlyOfMatcherStorage()
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

    [Theory]
    [InlineData("b2j")]
    [InlineData("bjunk")]
    [InlineData("bpopular")]
    public void SnapshotAdmitsOnceAndNeverRefundsUnrelatedStorage(string member)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null,
            new List<object> { new BigInteger(1), new BigInteger(2) },
            new List<object> { new BigInteger(1), new BigInteger(2) }, false, span, context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var charge = member == "b2j" ? 96 + 2 * (96 + 32 + 16) : 80;
        // One snapshot fits, two do not. The old second admission corrupted
        // already committed matcher storage on refusal.
        Assert.True(budget.TryReserve(limit - budget.ReservedBytes - charge, out var padding));
        object snapshot;
        using (padding)
        {
            Assert.True(matcher.TryGetMember(member, out var view));
            snapshot = view;
            Assert.Equal(before + charge, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(matcher.TryGetMember(member, out var again));
            Assert.Same(view, again);
            Assert.Equal(before + charge, context.MemoryGovernor.CurrentCommittedBytes);
        }
        var matcherBefore = matcher.CommittedStorageBytes;
        matcher.SetSeq2(new List<object> { new BigInteger(3) }, span, context);
        Assert.Equal(before + charge + matcher.CommittedStorageBytes - matcherBefore,
            context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(matcher.TryGetMember(member, out var replacement));
        Assert.NotSame(snapshot, replacement);
        if (member == "b2j")
        {
            Assert.Equal(2, Assert.IsType<PyDict>(snapshot).Count);
            Assert.Equal(1, Assert.IsType<PyDict>(replacement).Count);
        }
        else Assert.Equal(0, Assert.IsType<PySet>(snapshot).Count);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("b2j", 383)]
    [InlineData("bjunk", 79)]
    [InlineData("bpopular", 79)]
    public void RefusedSnapshotPreservesMatcherFunding(string member, long available)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null,
            new List<object> { new BigInteger(1), new BigInteger(2) },
            new List<object> { new BigInteger(1), new BigInteger(2) }, false, new(0, 0, 0, 0), context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(budget.TryReserve(limit - budget.ReservedBytes - available, out var padding));
        using (padding)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => matcher.TryGetMember(member, out _));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
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
