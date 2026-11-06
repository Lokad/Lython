using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeDictionaryMutationTests
{
    [Fact]
    public void CollidingNonreflexiveKeysKeepIdentityAndOrderThroughMutation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var keys = Enumerable.Range(0, 64).Select(_ => (object)double.NaN).ToArray();
            Assert.Single(keys.Select(PyValueComparer.Instance.GetHashCode).Distinct());
            var dictionary = new PyDict(context.MemoryGovernor);
            for (var index = 0; index < keys.Length; index++) dictionary.SetItem(keys[index], new BigInteger(index));
            dictionary.SetItem(keys[17], "updated");
            Assert.Equal(keys.Length, dictionary.Count);
            Assert.Equal("updated", dictionary.GetItem(keys[17]));
            Assert.False(dictionary.ContainsKey((object)double.NaN));

            var removed = keys.Where((_, index) => index % 3 == 0).ToArray();
            foreach (var key in removed) Assert.True(dictionary.Remove(key));
            var expected = keys.Where((_, index) => index % 3 != 0).ToList();
            foreach (var key in removed.Reverse())
            {
                dictionary.SetItem(key, "reinserted");
                expected.Add(key);
            }
            var actual = dictionary.Keys.ToArray();
            Assert.Equal(expected.Count, actual.Length);
            for (var index = 0; index < actual.Length; index++) Assert.Same(expected[index], actual[index]);
            var copy = new PyDict(dictionary);
            dictionary.Clear();
            Assert.Empty(dictionary);
            Assert.Equal(keys.Length, copy.Count);
            Assert.Equal("updated", copy.GetItem(keys[17]));
            foreach (var key in removed) Assert.Equal("reinserted", copy.GetItem(key));
            for (var index = actual.Length - 1; index >= 0; index--)
            {
                Assert.True(copy.Remove(actual[index]));
                Assert.Equal(index, copy.Count);
                if (index > 0) Assert.True(copy.ContainsKey(actual[0]));
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CollisionScanObservesCancellationRequestedDuringHashing()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var dictionary = new PyDict(context.MemoryGovernor);
            for (var index = 0; index < 128; index++) dictionary.SetItem((object)double.NaN, BigInteger.One);
            var baseline = budget.ReservedBytes;
            var span = new LythonSourceSpan(0, 0, 1, 1);
            using (PyStructuralGuard.PushAmbient(context, span))
            {
                var key = new CancelDuringHash(cancellation, PyValueComparer.Instance.GetHashCode(double.NaN));
                var error = Assert.Throws<LythonRuntimeException>(() => dictionary.TryGetValue(key, out _));
                Assert.Equal("execution canceled", error.Message);
            }
            Assert.Equal(128, dictionary.Count);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class CancelDuringHash(CancellationTokenSource cancellation, int hash) : IPyHashableValue
    {
        public int GetPyHashCode()
        {
            cancellation.Cancel();
            return hash;
        }
    }

    [Fact]
    public void ExplicitQueryKeepsItsContextAndRestoresOuterProvenanceOnCancellation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var outer = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var dictionary = new PyDict(context.MemoryGovernor);
            for (var index = 0; index < 128; index++) dictionary.SetItem((object)double.NaN, BigInteger.One);
            var span = new LythonSourceSpan(7, 3, 7, 12);
            var outerSpan = new LythonSourceSpan(2, 1, 2, 4);
            var baseline = budget.ReservedBytes;
            using (PyStructuralGuard.PushAmbient(outer, outerSpan))
            {
                var key = new ContextualHash(context, span, cancellation,
                    PyValueComparer.Instance.GetHashCode(double.NaN));
                var error = Assert.Throws<LythonRuntimeException>(() => dictionary.TryGetValue(key, out _, context, span));
                Assert.Equal("execution canceled", error.Message);
                Assert.Equal(span, error.Span);
                Assert.Same(outer, PyStructuralGuard.AmbientContext);
                Assert.Equal(outerSpan, PyStructuralGuard.AmbientSpan);
            }
            Assert.Equal(128, dictionary.Count);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ContextualHash(LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        CancellationTokenSource cancellation, int hash) : IPyHashableValue
    {
        public int GetPyHashCode()
        {
            Assert.Same(context, PyStructuralGuard.AmbientContext);
            Assert.Equal(span, PyStructuralGuard.AmbientSpan);
            cancellation.Cancel();
            return hash;
        }
    }
}
