using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionSortTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(128)]
    public void CancellationDuringNativeSortPreservesPythonFailureAndRefundsScratch(int size)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token, MemoryBudget = new LythonMemoryBudget(1024) },regexProvider:Utf8RegexProvider.Instance);
        var values = Enumerable.Range(0, size).Reverse().ToList();
        var failure = Assert.Throws<LythonRuntimeException>(() => ExecutionSort.Sort(values, context, null,
            (left, right) => { cancellation.Cancel(); return left.CompareTo(right); }));
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Contains("execution canceled", failure.Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void PreCanceledSortDoesNotInvokeTheComparer()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var called = false;
        Assert.Throws<LythonRuntimeException>(() => ExecutionSort.Sort(new List<int> { 2, 1 }, context, null,
            (left, right) => { called = true; return left.CompareTo(right); }));
        Assert.False(called);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedSortScratchDoesNotInvokeTheComparer()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(128) },regexProvider:Utf8RegexProvider.Instance);
        var called = false;
        var failure = Assert.Throws<LythonRuntimeException>(() => ExecutionSort.Sort(new List<int> { 2, 1 }, context, null,
            (left, right) => { called = true; return left.CompareTo(right); }));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.False(called);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ComparerPythonFailureKeepsItsOriginalIdentity()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null,regexProvider:Utf8RegexProvider.Instance);
        var expected = RuntimeErrors.Value("comparison failed", null);
        var actual = Assert.Throws<LythonRuntimeException>(() => ExecutionSort.Sort(new List<int> { 2, 1 }, context, null,
            (left, right) => throw expected));
        Assert.Same(expected, actual);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DefaultAndExplicitComparersPreserveOrdering()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null,regexProvider:Utf8RegexProvider.Instance);
        var values = new List<int> { 3, 1, 2 };
        ExecutionSort.Sort(values, context, null);
        Assert.Equal(new[] { 1, 2, 3 }, values);
        ExecutionSort.Sort(values, context, null, (left, right) => right.CompareTo(left));
        Assert.Equal(new[] { 3, 2, 1 }, values);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
