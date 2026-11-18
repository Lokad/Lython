using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class KnownDrainOwnershipTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 64)]
    [InlineData(true, 64)]
    public async Task SizeHintDoesNotChangeValuesOrReleaseLiveScratch(bool asynchronous, int hint)
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var baseline = budget.ReservedBytes;
        var source = new KnownSource(hint, 17);
        using (var lease = asynchronous
            ? await PyIteration.DrainLeasedAsync(source, new(0, 0, 0, 0), context)
            : PyIteration.DrainLeased(source, new(0, 0, 0, 0), context))
        {
            Assert.Equal(17, lease.Items.Count);
            Assert.All(lease.Items, value => Assert.Same(source.Value, value));
            Assert.Equal(1, source.Disposed);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedConsumerDoesNotAdmitTheWholeSource(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var baseline = budget.ReservedBytes;
        var source = new KnownSource(int.MaxValue, 17);
        using (var lease = asynchronous
            ? await PyIteration.DrainLeasedAsync(source, new(0, 0, 0, 0), context, 3)
            : PyIteration.DrainLeased(source, new(0, 0, 0, 0), context, 3))
        {
            Assert.Equal(3, lease.Items.Count);
            Assert.Equal(3, source.Yielded);
            Assert.Equal(1, source.Disposed);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedInitialCapacityDoesNotStartEnumeration(bool asynchronous)
    {
        var budget = new LythonMemoryBudget();
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        var baseline = budget.ReservedBytes;
        var source = new KnownSource(int.MaxValue, 17);
        // Refuse the substantial estimate without allocating its backing.
        hook.RefuseGrowth = _ => true;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            using var lease = asynchronous
                ? await PyIteration.DrainLeasedAsync(source, new(0, 0, 0, 0), context)
                : PyIteration.DrainLeased(source, new(0, 0, 0, 0), context);
        });
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(0, source.Yielded);
        Assert.Equal(0, source.Disposed);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionReentryDoesNotFreezeTheMutableSource(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        var source = new PyList(8, context.MemoryGovernor, new(0, 0, 0, 0));
        var alias = new object();
        source.Add(alias);
        source.Add(alias);
        var baseline = budget.ReservedBytes;
        var changed = false;
        hook.AfterGrowth = _ =>
        {
            hook.AfterGrowth = null;
            changed = true;
            source.Add(alias);
        };
        using (var lease = asynchronous
            ? await PyIteration.MaterializeLeasedAsync(source, new(0, 0, 0, 0), context)
            : PyIteration.MaterializeLeased(source, new(0, 0, 0, 0), context))
        {
            Assert.True(changed);
            Assert.Equal(3, lease.Items.Count);
            Assert.All(lease.Items, value => Assert.Same(alias, value));
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private sealed class KnownSource(int hint, int length) : IReadOnlyCollection<object>, IAsyncEnumerable<object>
    {
        public int Count => hint;
        public object Value { get; } = new();
        public int Yielded { get; private set; }
        public int Disposed { get; private set; }
        public IEnumerator<object> GetEnumerator()
        {
            try { for (var index = 0; index < length; index++) { Yielded++; yield return Value; } }
            finally { Disposed++; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public async IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            try
            {
                for (var index = 0; index < length; index++)
                {
                    await Task.Yield();
                    Yielded++;
                    yield return Value;
                }
            }
            finally { Disposed++; }
        }
    }
}
