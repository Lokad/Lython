using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class XmlTraversalOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(11, 2, 1, 10);

    private static LythonRuntime.ElementTreeModule.Element Parse(string text, LythonRuntime.ExecutionContext context)
    {
        Assert.True(LythonRuntime.ElementTreeModule.Instance.TryGetMember("fromstring", out var parse));
        return Assert.IsType<LythonRuntime.ElementTreeModule.Element>(
            Assert.IsAssignableFrom<LythonRuntime.ICallable>(parse).Invoke([CallArgumentValue.Positional(PyString.FromString(text))], Span, context));
    }

    private static LythonRuntime.ICallable Method(LythonRuntime.ElementTreeModule.Element root, string name)
    {
        Assert.True(root.TryGetMember(name, out var member));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
    }

    [Fact]
    public void WideTraversalNeedsOnlyItsFundedDepthStackAndReleasesItAtExhaustion()
    {
        const long limit = 8 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var root = Parse("<r>" + string.Concat(Enumerable.Repeat("<x>kept</x>", 1024)) + "</r>", context);
                Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 4096, out var pressure));
                using (pressure)
                {
                    var iterator = Assert.IsAssignableFrom<IPyIteratorValue>(Method(root, "iter").Invoke([], Span, context));
                    var retained = budget.ReservedBytes;
                    Assert.True(iterator.TryMoveNext(out var first));
                    Assert.Same(root, first);
                    foreach (var child in root.Children)
                    {
                        Assert.True(iterator.TryMoveNext(out var actual));
                        Assert.Same(child, actual);
                        Assert.Equal(retained, budget.ReservedBytes);
                    }
                    Assert.False(iterator.TryMoveNext(out _));
                    Assert.True(budget.ReservedBytes < retained);
                    var exhausted = budget.ReservedBytes;
                    Assert.False(iterator.TryMoveNext(out _));
                    Assert.Equal(exhausted, budget.ReservedBytes);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Equal("kept", Assert.IsType<PyString>(((LythonRuntime.ElementTreeModule.Element)root.GetIndex(0)).Text).AsString());
                    var failure = Assert.Throws<LythonRuntimeException>(() => Method(root, "findall").Invoke(
                        [CallArgumentValue.Positional(PyString.FromString(".//x"))], Span, context));
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Equal(exhausted, budget.ReservedBytes);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Equal(1024, root.Length);
                }
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("iter")]
    [InlineData("clear")]
    public void RefusedConstructionOrReplacementPreservesTheFundedTree(string operation)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var root = Parse("<r a='kept'>before<x>inside</x>after</r>", context);
                var attributes = root.Attributes; var text = root.Text; var child = root.GetIndex(0);
                Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out var pressure));
                using (pressure)
                {
                    var baseline = budget.ReservedBytes;
                    var failure = Assert.Throws<LythonRuntimeException>(() => Method(root, operation).Invoke([], Span, context));
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Same(attributes, root.Attributes);
                    Assert.Same(text, root.Text);
                    Assert.Same(child, root.GetIndex(0));
                    Assert.Equal(1, root.Length);
                    Assert.Equal(baseline, budget.ReservedBytes);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                }
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ClearReplacesAttributesWithoutRefundingRetainedPayloadAliases()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var attrs = string.Join(" ", Enumerable.Range(0, 64).Select(i => $"a{i}='value{i}'"));
                var root = Parse($"<r {attrs}>before<x>inside</x>after</r>", context);
                var attributes = root.Attributes; var text = root.Text;
                var child = Assert.IsType<LythonRuntime.ElementTreeModule.Element>(root.GetIndex(0));
                var baseline = budget.ReservedBytes;
                var children = root.Children.CommittedStorageBytes;
                Assert.IsType<PyNone>(Method(root, "clear").Invoke([], Span, context));
                Assert.NotSame(attributes, root.Attributes);
                Assert.Equal(64, attributes.Count);
                Assert.Equal(0, root.Attributes.Count);
                Assert.Equal("before", Assert.IsType<PyString>(text).AsString());
                Assert.Equal("inside", Assert.IsType<PyString>(child.Text).AsString());
                Assert.Equal("after", Assert.IsType<PyString>(child.Tail).AsString());
                Assert.True(budget.ReservedBytes >= baseline - children);
                Assert.Equal(0, root.Length);
                Assert.IsType<PyNone>(root.Text);
                Assert.IsType<PyNone>(root.Tail);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringReplacementAdmissionLeavesTheTreeUnchanged()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
            try
            {
                var root = Parse("<r a='kept'>before<x/></r>", context);
                var attrs = root.Attributes; var text = root.Text; var child = root.GetIndex(0);
                var baseline = budget.ReservedBytes;
                hook.AfterGrowth = _ => cancellation.Cancel();
                var failure = Assert.Throws<LythonRuntimeException>(() => Method(root, "clear").Invoke([], Span, context));
                Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                Assert.Same(attrs, root.Attributes);
                Assert.Same(text, root.Text);
                Assert.Same(child, root.GetIndex(0));
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
