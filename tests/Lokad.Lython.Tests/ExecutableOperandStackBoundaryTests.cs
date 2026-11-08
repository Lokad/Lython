using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableOperandStackBoundaryTests
{
    [Fact]
    public void UpperGuestOperandsRetireBeforeLowerPrivateOwnershipEnds()
    {
        using var stack = new StackView(4, lastOwnedTemporarySlot: 1);
        var retained = new object();
        var disposed = false;
        var guest = new OrdinaryDisposable();
        stack.Push(retained);
        stack.Push(new Temporary(() => disposed = true));
        stack.Push(guest);
        stack.Push(new object());
        stack.RemoveTail(2);
        Assert.Equal(2, stack.Count);
        Assert.False(disposed);
        Assert.False(guest.Disposed);
        Assert.Same(retained, stack.At(0));
        stack.RemoveTail(1);
        Assert.True(disposed);
        Assert.False(guest.Disposed);
        Assert.Same(retained, stack.Pop());
    }

    [Fact]
    public void RetiringAnOrdinaryDisposableValueDoesNotDisposeIt()
    {
        using var stack = new StackView(4);
        var value = new OrdinaryDisposable();
        stack.Push(value);
        stack.Push(new object());
        stack.RemoveTail(2);
        Assert.Equal(0, stack.Count);
        Assert.False(value.Disposed);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    public void CapacityAndReuseKeepEachLiveReference(int size)
    {
        using var stack = new StackView(size + 1);
        for (var pass = 0; pass < 2; pass++)
        {
            var values = Enumerable.Range(0, size).Select(_ => new object()).ToArray();
            for (var i = 0; i < size; i++)
            {
                stack.Push(values[i]);
                Assert.Equal(i + 1, stack.Count);
                Assert.Same(values[i], stack.Peek());
                for (var j = 0; j <= i; j++) Assert.Same(values[j], stack.At(j));
            }
            for (var i = size - 1; i >= 0; i--)
            {
                Assert.Same(values[i], stack.Pop());
                Assert.Equal(i, stack.Count);
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    public void UnwindDisposesLastInFirstOutBeforeClearingLiveEntries(int size)
    {
        using var stack = new StackView(size + 1);
        var retained = new object();
        stack.Push(retained);
        var disposed = new List<int>();
        var values = Enumerable.Range(1, size).Select(i => new Temporary(() =>
        {
            disposed.Add(i);
            Assert.Equal(size + 1, stack.Count);
            Assert.Same(retained, stack.At(0));
        })).ToArray();
        foreach (var value in values) stack.Push(value);
        stack.RemoveTail(0);
        Assert.Empty(disposed);
        stack.RemoveTail(size);
        Assert.Equal(Enumerable.Range(1, size).Reverse(), disposed);
        Assert.Equal(1, stack.Count);
        Assert.Same(retained, stack.Peek());
        stack.Push(new object());
        stack.RemoveTail(1);
        Assert.Equal(Enumerable.Range(1, size).Reverse(), disposed);
        Assert.Same(retained, stack.Pop());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    public void FailedDisposalKeepsOriginalEntriesForPropagation(int size)
    {
        using var stack = new StackView(size + 1);
        var denied = new InvalidOperationException("controlled disposal failure");
        var fail = true;
        var disposed = new List<int>();
        var values = Enumerable.Range(0, size).Select(i => new Temporary(() =>
        {
            disposed.Add(i);
            if (i == 1 && fail) throw denied;
        })).ToArray();
        foreach (var value in values) stack.Push(value);
        Assert.Same(denied, Assert.Throws<InvalidOperationException>(() => stack.RemoveTail(size)));
        Assert.Equal(Enumerable.Range(1, size - 1).Reverse(), disposed);
        Assert.Equal(size, stack.Count);
        for (var i = 0; i < size; i++) Assert.Same(values[i], stack.At(i));
        fail = false;
        stack.RemoveTail(size);
        Assert.Equal(Enumerable.Range(1, size - 1).Reverse().Concat(Enumerable.Range(0, size).Reverse()), disposed);
        Assert.Equal(0, stack.Count);
    }

    private sealed class Temporary(Action dispose) : ExecutableTemporaryValue
    {
        public override void Dispose() => dispose();
    }

    private sealed class OrdinaryDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    // Keep reference order and failed-disposal guarantees while supplying the
    // real owner required by operand-capacity admission.
    private sealed class StackView : IDisposable
    {
        private readonly LythonRuntime.ExecutionContext _context = new(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        private LythonRuntime.ExecutableValueStorage _stack;
        public StackView(int capacity, int? lastOwnedTemporarySlot = null)
            => _stack = new(capacity, 0, lastOwnedTemporarySlot ?? capacity - 1,
                _context, null, 0L, null, lookupOwner: null);
        public void Dispose()
        {
            RemoveTail(Count);
            _stack.ReleaseStorage();
            Assert.Equal(0, _context.MemoryGovernor.CurrentAccountedBytes);
        }
        public int Count => _stack.LiveExtent;
        public object At(int index) => _stack[index];
        public void Push(object value) => _stack.PublishValue(Count, Count + 1, value);
        public object Pop() => _stack.TakeValue(Count - 1, Count - 1);
        public object Peek() => At(Count - 1);
        public void RemoveTail(int count) => _stack.RetireTo(Count - count);
    }
}
