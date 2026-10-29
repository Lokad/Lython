using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DifflibOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void BorrowedSequencesAndSameObjectSettersNeedNoNewStorage()
    {
        var budget = new LythonMemoryBudget(1048576);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var a = new PyList([BigInteger.One], context.MemoryGovernor, Span);
        var b = new PyList([BigInteger.One], context.MemoryGovernor, Span);
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, a, b, false, Span, context);
        Assert.True(matcher.TryGetMember("b2j", out var index));
        var before = budget.ReservedBytes;
        Assert.True(budget.TryReserve(1048576 - before, out var padding));
        using (padding)
        {
            matcher.SetSeq1(a, Span, context);
            matcher.SetSeq2(b, Span, context);
            Assert.True(matcher.TryGetMember("b2j", out var again));
            Assert.Same(index, again);
            Assert.Equal(1.0, matcher.RealQuickRatio());
        }
        Assert.Equal(before, budget.ReservedBytes);
        a[0] = new BigInteger(2);
        Assert.Equal(0.0, matcher.Ratio(Span, context));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void IndexConstructionRefusesBeforeReadingOrPublishingNewInput()
    {
        var budget = new LythonMemoryBudget(1048576);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var old = new List<object> { BigInteger.One };
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, old, old, false, Span, context);
        var candidate = new ObservedSequence();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(budget.TryReserve(1048576 - budget.ReservedBytes, out var padding));
        using (padding)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => matcher.SetSeq2(candidate, Span, context));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(0, candidate.Reads);
            Assert.True(matcher.TryGetMember("b", out var unchanged));
            Assert.Same(old, unchanged);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void GroupedOutputAndOldCanonicalAliasesStayFunded()
    {
        var budget = new LythonMemoryBudget(1048576);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null,
            new List<object> { BigInteger.One, new BigInteger(2) },
            new List<object> { BigInteger.One, new BigInteger(3) }, false, Span, context);
        var codes = matcher.BuildOpcodes(Span, context);
        Assert.True(matcher.TryGetMember("b2j", out var oldIndex));
        var before = budget.ReservedBytes;
        PyList group;
        using (var groups = matcher.BuildGroupedOpcodes(3, Span, context).GetEnumerator())
        {
            Assert.True(groups.MoveNext());
            group = Assert.IsType<PyList>(groups.Current);
            Assert.True(budget.ReservedBytes > before);
        }
        var retained = budget.ReservedBytes;
        matcher.SetSeq2(new List<object> { new BigInteger(4) }, Span, context);
        Assert.True(budget.ReservedBytes >= retained);
        Assert.Equal(2, Assert.IsType<PyDict>(oldIndex).Count);
        Assert.Equal(2, codes.Count);
        Assert.Equal(2, group.Count);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void InvalidReplacementRollsBackPrivateStorageAndPreservesInput()
    {
        var budget = new LythonMemoryBudget(1048576);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var old = new List<object> { BigInteger.One };
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, old, old, false, Span, context);
        var before = budget.ReservedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => matcher.SetSeq2(
            new List<object> { new List<object>() }, Span, context));
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.Equal(before, budget.ReservedBytes);
        Assert.True(matcher.TryGetMember("b", out var unchanged));
        Assert.Same(old, unchanged);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ObservedSequence : IReadOnlyList<object>
    {
        public int Reads;
        public int Count => 8;
        public object this[int index] { get { Reads++; return new BigInteger(index); } }
        public IEnumerator<object> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
