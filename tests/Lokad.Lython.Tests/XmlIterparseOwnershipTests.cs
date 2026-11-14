using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class XmlIterparseOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(7, 1, 1, 4);

    private static IPyIteratorValue Open(LythonRuntime.ExecutionContext context, bool start = false)
    {
        Assert.True(LythonRuntime.ElementTreeModule.Instance.TryGetMember("iterparse", out var function));
        return Assert.IsAssignableFrom<IPyIteratorValue>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(function).Invoke(
            [CallArgumentValue.Positional(PyString.FromString("input.xml")),
             CallArgumentValue.Positional(new PyTuple(new object[] { PyString.FromString(start ? "start" : "end") }))], Span, context));
    }

    private static void Close(IPyIteratorValue iterator, LythonRuntime.ExecutionContext context)
    {
        Assert.True(Assert.IsAssignableFrom<IPyDynamicAttributes>(iterator).TryGetMember("close", out var close));
        Assert.IsAssignableFrom<LythonRuntime.ICallable>(close).Invoke([], Span, context);
    }

    [Fact]
    public void ClosingAStartedParserReturnsScratchButKeepsPublishedTreeStorageFunded()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var host = new MockLythonHost(); host.SeedFile("/input.xml", "<r a='kept'><x>inside</x></r>");
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = owner });
            try
            {
                var iterator = Open(context, true);
                Assert.True(iterator.TryMoveNext(out var pair));
                var root = Assert.IsType<LythonRuntime.ElementTreeModule.Element>(Assert.IsType<PyTuple>(pair).GetIndex(1));
                var attributes = root.Attributes;
                var key = PyString.FromString("a");
                Assert.True(attributes.TryGetValue(key, out var value));
                var text = Assert.IsType<PyString>(value);
                var withParser = budget.ReservedBytes;
                Close(iterator, context);
                Assert.True(budget.ReservedBytes < withParser);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= attributes.CommittedStorageBytes + text.CommittedOwnedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal("kept", text.AsString());
                Assert.Same(attributes, root.Attributes);
                Assert.Equal(1, host.ClosedFileCount);
                Assert.False(iterator.TryMoveNext(out _));
                var closed = budget.ReservedBytes;
                Close(iterator, context);
                Assert.Equal(closed, budget.ReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedParserCapacityDoesNotReadOrLeaveWorkingReservations()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var host = new MockLythonHost(); host.SeedFile("/input.xml", "<r/>");
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = hook });
            try
            {
                var iterator = Open(context);
                hook.RefuseGrowth = _ => true;
                var failure = Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext(out _));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(0, host.RangedReadCount);
                Assert.Equal(1, host.ClosedFileCount);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void MalformedInputRollsBackBeforePublicationWithoutRefundingTheIteratorShell()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var host = new MockLythonHost(); host.SeedFile("/input.xml", "<r a='kept'><x>unfinished");
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = owner });
            try
            {
                var before = context.MemoryGovernor.CurrentCommittedBytes;
                var iterator = Open(context);
                var shell = context.MemoryGovernor.CurrentCommittedBytes;
                Assert.True(shell > before);
                var error = Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext(out _));
                Assert.Equal(PythonExceptionIdentity.Module("xml.etree.ElementTree", "ParseError"), error.Identity);
                Assert.Equal(shell, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(1, host.ClosedFileCount);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
