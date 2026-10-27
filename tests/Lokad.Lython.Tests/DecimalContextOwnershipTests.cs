using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DecimalContextOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void RefusedChildCopyLeavesTheExactParentAndMappingUntouched()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var parent = context.DecimalContext;
            parent.Flags.SetItem("flag", true);
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out var pin));
            using (pin)
            {
                var before = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => new PyDecimalLocalContext(context, null, Span));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(parent, context.DecimalContext);
                Assert.Equal(true, parent.Flags.GetItem("flag"));
                Assert.Equal(before, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ChildAliasesStayFundedAndNativeExitRestoresTheParentAfterCancellation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var parent = context.DecimalContext;
            parent.Flags.SetItem("flag", true);
            var before = budget.ReservedBytes;
            var manager = new PyDecimalLocalContext(context, null, Span);
            var child = Assert.IsType<PyDecimalContext>(manager.Enter());
            var alias = child.Flags;
            Assert.NotSame(parent.Flags, alias);
            Assert.True(budget.ReservedBytes > before);
            var funded = budget.ReservedBytes;
            cancellation.Cancel();
            Assert.False(manager.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance));
            Assert.Same(parent, context.DecimalContext);
            Assert.Equal(true, alias.GetItem("flag"));
            Assert.Equal(funded, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
