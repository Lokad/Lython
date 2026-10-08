using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ElementTreeAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static object Parse(string source, LythonRuntime.ExecutionContext context)
    {
        Assert.True(LythonRuntime.ElementTreeModule.Instance.TryGetMember("fromstring", out var member));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member).Invoke(
            [CallArgumentValue.Positional(PyString.FromString(source))], Span, context);
    }

    [Fact]
    public void MalformedGraphsRefundWithoutCollectingGuestValues()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var governor = context.MemoryGovernor;
        var pool = context.Services.State.CallTemporaries;
        var initial = governor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                Parse("<r a='value'>before<x>inside</x>tail<y a='one' b='two'>broken", context));
            Assert.Equal(PythonExceptionIdentity.Module("xml.etree.ElementTree", "ParseError"), error.Identity);
            Assert.Equal(0, pool.Count);
            Assert.Equal(initial + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(20000)]
    [InlineData(30000)]
    [InlineData(40000)]
    [InlineData(50000)]
    [InlineData(60000)]
    [InlineData(80000)]
    public void ParserAndGraphDenialsRefund(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var source = "<r>" + string.Concat(Enumerable.Repeat("<x a='value'>inside</x>tail", 50)) + "</r>";
        var pool = context.Services.State.CallTemporaries;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => Parse(source, context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, pool.Count);
            Assert.Equal(pool.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        if (cap >= 60000) Assert.True(pool.CommittedBackingBytes > 0);
    }

    [Fact]
    public void DepthAndDtdPolicyRemainControlledAndRefund()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        foreach (var source in new[]
        {
            "<!DOCTYPE r [<!ENTITY a 'expanded'>]><r>&a;</r>",
            "<!DOCTYPE r SYSTEM 'file:///ambient.xml'><r/>",
            string.Concat(Enumerable.Repeat("<r>", 1026)) + string.Concat(Enumerable.Repeat("</r>", 1026)),
        })
        {
            var error = Assert.Throws<LythonRuntimeException>(() => Parse(source, context));
            Assert.Equal(PythonExceptionIdentity.Module("xml.etree.ElementTree", "ParseError"), error.Identity);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.Services.State.CallTemporaries.Count);
        }
        var valid = Parse(string.Concat(Enumerable.Repeat("<r>", 1025)) +
            string.Concat(Enumerable.Repeat("</r>", 1025)), context);
        Assert.IsType<LythonRuntime.ElementTreeModule.Element>(valid);
    }

    [Fact]
    public void PreCanceledParsingDoesNotCommitOrReserve()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        var error = Assert.Throws<LythonRuntimeException>(() => Parse("<r/>", context));
        Assert.Equal("execution canceled", error.Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void WorkAndNodeCountLimitsRollbackPartiallyBuiltTrees()
    {
        foreach (var options in new[]
        {
            new LythonRunOptions { MaxExecutionSteps = 10 },
            new LythonRunOptions { MaxCollectionSize = 3 },
        })
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options);
            var error = Assert.Throws<LythonRuntimeException>(() =>
                Parse("<r><a key='one'>text</a><b/><c/><d/></r>", context));
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Contains("maximum", error.Message);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            var pool = context.Services.State.CallTemporaries;
            Assert.Equal(0, pool.Count);
            Assert.Equal(pool.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        }
    }

    [Fact]
    public void AChildAliasDoesNotRetainItsParent()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (parent, child) = ChildAlias(context);
        GC.Collect();
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.False(parent.IsAlive);
        Assert.True(context.Services.State.CallTemporaries.IsTracked(child));
        Assert.True(context.Services.State.CallTemporaries.IsTracked(child.Tag));
        Assert.Equal("text", ((PyString)child.Text).AsString());
        GC.KeepAlive(child);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Parent, LythonRuntime.ElementTreeModule.Element Child) ChildAlias(
        LythonRuntime.ExecutionContext context)
    {
        var root = (LythonRuntime.ElementTreeModule.Element)Parse("<r><x a='one'>text</x><y/></r>", context);
        return (new WeakReference(root), (LythonRuntime.ElementTreeModule.Element)root.GetIndex(0));
    }

    [Fact]
    public void TextAndMapAliasesKeepTheirOwnChargesAfterAllNodesDrop()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (root, child, attributes, text) = ScalarAliases(context);
        GC.Collect();
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.False(root.IsAlive);
        Assert.False(child.IsAlive);
        Assert.True(context.Services.State.CallTemporaries.IsTracked(attributes));
        Assert.True(context.Services.State.CallTemporaries.IsTracked(text));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= attributes.CommittedStorageBytes + text.CommittedOwnedBytes);
        Assert.Equal("text", text.AsString());
        Assert.True(attributes.TryGetValue(PyString.FromString("a"), out var value));
        Assert.Equal("one", Assert.IsType<PyString>(value).AsString());
        GC.KeepAlive(attributes);
        GC.KeepAlive(text);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Root, WeakReference Child, PyDict Attributes, PyString Text) ScalarAliases(
        LythonRuntime.ExecutionContext context)
    {
        var root = (LythonRuntime.ElementTreeModule.Element)Parse("<r><x a='one'>text</x></r>", context);
        var child = (LythonRuntime.ElementTreeModule.Element)root.GetIndex(0);
        return (new WeakReference(root), new WeakReference(child), child.Attributes, (PyString)child.Text);
    }

    [Fact]
    public void AnEmptyAttributeAliasRetainsItsFixedWrapperCoupon()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (root, attributes) = EmptyAttributeAlias(context);
        GC.Collect();
        var pool = context.Services.State.CallTemporaries;
        pool.Sweep(full: true);
        Assert.False(root.IsAlive);
        Assert.Equal(0, attributes.Count);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 256 + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes);
        GC.KeepAlive(attributes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Root, PyDict Attributes) EmptyAttributeAlias(LythonRuntime.ExecutionContext context)
    {
        var root = (LythonRuntime.ElementTreeModule.Element)Parse("<r/>", context);
        return (new WeakReference(root), root.Attributes);
    }
}
