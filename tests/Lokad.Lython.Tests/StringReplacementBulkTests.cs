using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

public sealed class StringReplacementBulkTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Fact]
    public void BoundedEmptyPatternDoesNotAllocateScalarStringsThroughTheSuffix()
    {
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var text = string.Concat(Enumerable.Repeat("éλ😀", 256));
            var value = PyString.FromString(text, context.MemoryGovernor, Span);
            Assert.True(PyMemberAccess.TryResolve(value, "replace", context, Span, out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            CallArgumentValue[] args = [CallArgumentValue.Positional(PyString.Empty),
                CallArgumentValue.Positional(PyString.FromString("|")), CallArgumentValue.Positional(BigInteger.One)];
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = Assert.IsType<PyString>(callable.Invoke(args, Span, context));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal("|" + text, result.AsString());
            Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
            if (measured) output.WriteLine($"BOUNDED_REPLACE_ALLOCATION:{allocated}");
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured) Assert.True(allocated < 24576, $"Bounded replacement allocated {allocated} bytes.");
        }
    }

    [Fact]
    public void CountZeroPreservesTheOriginalValueAndFunding()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var value = PyString.FromString("éλ😀", context.MemoryGovernor, Span);
        Assert.True(PyMemberAccess.TryResolve(value, "replace", context, Span, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var baseline = budget.ReservedBytes;
        var result = callable.Invoke([CallArgumentValue.Positional(PyString.Empty),
            CallArgumentValue.Positional(PyString.FromString("|")), CallArgumentValue.Positional(BigInteger.Zero)], Span, context);
        Assert.Same(value, result);
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedReplacementReleasesPrivateScratchAndKeepsInputAliasesFunded()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var value = PyString.FromString("ab", context.MemoryGovernor, Span);
        var replacement = PyString.FromString(new string('x', 65536), context.MemoryGovernor, Span);
        Assert.True(PyMemberAccess.TryResolve(value, "replace", context, Span, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 8192, out var padding));
        using var blocker = padding;
        var baseline = budget.ReservedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(
            [CallArgumentValue.Positional(PyString.FromString("a")), CallArgumentValue.Positional(replacement)], Span, context));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal("ab", value.AsString());
        Assert.Equal(65536, replacement.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        blocker?.Dispose();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void OversizedBoundedCapacityUsesTheExistingStringSizeGuardBeforeAllocation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var value = PyString.FromString(new string('a', 65536), context.MemoryGovernor, Span);
        var replacement = PyString.FromString(new string('b', 65536), context.MemoryGovernor, Span);
        Assert.True(PyMemberAccess.TryResolve(value, "replace", context, Span, out var member));
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        var baseline = budget.ReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(
            [CallArgumentValue.Positional(PyString.Empty), CallArgumentValue.Positional(replacement),
             CallArgumentValue.Positional(new BigInteger(32768))], Span, context));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Equal("string allocation is too large.", error.Message);
        Assert.True(allocated < 32768, $"Oversized capacity allocated {allocated} bytes.");
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
