using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CopyOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false), InlineData(true)]
    public void OnlyTheCopiedBackingSurvivesPrivateWorkingStorage(bool deep)
    {
        var budget = new LythonMemoryBudget(65536);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var module = LythonRuntime.ResolveBuiltinModule("copy", context)!;
            Assert.True(module.TryGetCachedMember(deep ? "deepcopy" : "copy", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var source = new PyList([BigInteger.One, new BigInteger(2)], context.MemoryGovernor, Span);
            // A deep memo legitimately uses the source's stable Python id.
            // Warm it before observing the operation's private working storage.
            if (deep) _ = context.State.GetObjectId(source);
            var baseline = context.MemoryGovernor.CurrentCommittedBytes;
            var result = Assert.IsType<PyList>(callable.Invoke([CallArgumentValue.Positional(source)], Span, context));
            Assert.NotSame(source, result);
            Assert.Equal(source.Count, result.Count);
            for (var i = 0; i < source.Count; i++) Assert.Same(source[i], result[i]);
            Assert.Equal(result.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes - baseline);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void AtomicCopyDoesNotRetainGraphStorage()
    {
        var budget = new LythonMemoryBudget(65536);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var module = LythonRuntime.ResolveBuiltinModule("copy", context)!;
            Assert.True(module.TryGetCachedMember("deepcopy", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            object source = BigInteger.One;
            var baseline = budget.ReservedBytes;
            Assert.Same(source, callable.Invoke([CallArgumentValue.Positional(source)], Span, context));
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
