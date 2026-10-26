using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Canonical partial state, dispatch registrations and wrapper metadata retain
/// their storage; repeated access/replacement does not invent new backing.
/// </summary>
public sealed class FunctoolsRegistryAccountingTests
{
    private static object NewPartial(CallArgumentValue[] bound, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return LythonRuntime.PartialFactory.Instance.Invoke(
            [CallArgumentValue.Positional(LythonRuntime.PartialFactory.Instance), .. bound], span, context);
    }

    private sealed class StubCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context) => PyNone.Instance;
    }

    private static object NewLruWrapper(LythonRuntime.ExecutionContext context)
    {
        var wrapperType = typeof(LythonRuntime).GetNestedType("PyLruCacheWrapper", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PyLruCacheWrapper not found.");
        var keyModeType = typeof(LythonRuntime).GetNestedType("CacheKeyMode", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CacheKeyMode not found.");
        var ctor = wrapperType.GetConstructors().Single();
        return ctor.Invoke([new StubCallable(), 8, Enum.ToObject(keyModeType, 0), context.MemoryGovernor]);
    }

    [Fact]
    public void PartialStateIsCanonicalAndFunded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var partial = (IPyContextualDynamicAttributes)NewPartial(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            context,
            span);
        Assert.True(partial.TryGetMember("args", context, span, out var args));
        Assert.True(partial.TryGetMember("keywords", context, span, out var keywords));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(before > 0);
        Assert.True(partial.TryGetMember("args", context, span, out var again));
        Assert.Same(args, again);
        Assert.True(partial.TryGetMember("keywords", context, span, out again));
        Assert.Same(keywords, again);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void WrapperMetadataOverwritesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var wrapper = (IPyMutableDynamicAttributes)NewLruWrapper(context);
        Assert.True(wrapper.TrySetMember("a", 1));
        Assert.True(wrapper.TrySetMember("b", 2));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(wrapper.TrySetMember("a", 3));
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
