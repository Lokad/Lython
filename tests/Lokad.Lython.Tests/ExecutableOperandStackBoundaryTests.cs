using System.Reflection;
using System.Runtime.ExceptionServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ExecutableOperandStackBoundaryTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    public void GrowthAndReuseKeepEachLiveReference(int size)
    {
        var stack = new StackView();
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
    public void UnwindDisposesInOrderBeforeClearingLiveEntries(int size)
    {
        var stack = new StackView();
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
        Assert.Equal(Enumerable.Range(1, size), disposed);
        Assert.Equal(1, stack.Count);
        Assert.Same(retained, stack.Peek());
        stack.Push(new object());
        stack.RemoveTail(1);
        Assert.Equal(Enumerable.Range(1, size), disposed);
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
        var stack = new StackView();
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
        Assert.Equal(new[] { 0, 1 }, disposed);
        Assert.Equal(size, stack.Count);
        for (var i = 0; i < size; i++) Assert.Same(values[i], stack.At(i));
        fail = false;
        stack.RemoveTail(size);
        Assert.Equal(new[] { 0, 1 }.Concat(Enumerable.Range(0, size)), disposed);
        Assert.Equal(0, stack.Count);
    }

    private sealed class Temporary(Action dispose) : IExecutableTemporaryValue
    {
        public void Dispose() => dispose();
    }

    // Exercise the existing private stack contract on the unmodified runtime
    // first, without requiring a particular storage representation or capacity.
    private sealed class StackView
    {
        private static readonly Type StackType = typeof(LythonRuntime).GetNestedType("ExecutableValueStack", BindingFlags.NonPublic)!;
        private readonly object _stack = Activator.CreateInstance(StackType, [8])!;
        public int Count => (int)StackType.GetProperty("Count")!.GetValue(_stack)!;
        public object At(int index) => StackType.GetProperty("Item")!.GetValue(_stack, [index])!;
        public void Push(object value) => Invoke("Push", value);
        public object Pop() => Invoke("Pop")!;
        public object Peek() => Invoke("Peek")!;
        public void RemoveTail(int count) => Invoke("RemoveTail", count);
        private object? Invoke(string method, params object[] arguments)
        {
            try { return StackType.GetMethod(method)!.Invoke(_stack, arguments); }
            catch (TargetInvocationException error) when (error.InnerException is { } inner)
            {
                ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }
    }
}
