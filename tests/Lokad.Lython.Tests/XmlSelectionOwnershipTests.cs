using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class XmlSelectionOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(9, 2, 1, 10);

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
    public void FirstMatchNeedsNoFullMatchingFrontier()
    {
        const long limit = 8 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var root = Parse("<r>" + string.Concat(Enumerable.Repeat("<x a='kept'>text</x>", 1024)) + "</r>", context);
                var find = Method(root, "find");
                var baseline = budget.ReservedBytes;
                Assert.True(budget.TryReserve(limit - baseline - 4096, out var pressure));
                using (pressure)
                {
                    var funded = budget.ReservedBytes;
                    var first = find.Invoke([CallArgumentValue.Positional(PyString.FromString("x"))], Span, context);
                    Assert.Same(root.GetIndex(0), first);
                    Assert.Equal(funded, budget.ReservedBytes);
                    Assert.Equal(1024, root.Length);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void SelectedListAndAliasesRemainFundedAfterScratchCleanup()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var root = Parse("<r><g><x a='kept'>é😀</x><x>other</x></g></r>", context);
                var baseline = budget.ReservedBytes;
                var list = Assert.IsType<PyList>(Method(root, "findall").Invoke(
                    [CallArgumentValue.Positional(PyString.FromString("g/x"))], Span, context));
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.True(budget.ReservedBytes >= baseline + list.CommittedStorageBytes);
                var first = Assert.IsType<LythonRuntime.ElementTreeModule.Element>(list[0]);
                Assert.Same(((LythonRuntime.ElementTreeModule.Element)root.GetIndex(0)).GetIndex(0), first);
                Assert.Equal("é😀", Assert.IsType<PyString>(first.Text).AsString());
                Assert.True(first.Attributes.TryGetValue(PyString.FromString("a"), out var value));
                Assert.Equal("kept", Assert.IsType<PyString>(value).AsString());
                Assert.Equal(2, list.Count);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraversalGrowthCancellationOrRefusalUnwindsWithoutChangingTheTree(bool refuse)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
            try
            {
                var root = Parse(string.Concat(Enumerable.Repeat("<x>", 96)) + "<leaf>kept</leaf>" +
                    string.Concat(Enumerable.Repeat("</x>", 96)), context);
                var find = Method(root, "find");
                var baseline = budget.ReservedBytes;
                var encountered = false;
                // Actual private traversal capacity grows after input/graph construction.
                // Do not count execution checks or require a particular frame number.
                if (refuse) hook.RefuseGrowth = bytes => encountered |= bytes >= 1024;
                else hook.AfterGrowth = bytes => { if (bytes >= 1024) { encountered = true; cancellation.Cancel(); } };
                var error = Assert.Throws<LythonRuntimeException>(() => find.Invoke(
                    [CallArgumentValue.Positional(PyString.FromString(".//missing"))], Span, context));
                Assert.True(encountered);
                Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(1, root.Length);
                Assert.Equal("x", Assert.IsType<LythonRuntime.ElementTreeModule.Element>(root.GetIndex(0)).Tag.AsString());
                if (refuse)
                {
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                    Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => context.CheckExecution(Span)));
                }
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
