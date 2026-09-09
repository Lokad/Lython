using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG14: partial bound-argument arrays, dispatch registrations and wrapper
/// metadata commit exactly; replacement and overwrites stay balanced.
/// </summary>
public sealed class FunctoolsRegistryAccountingTests
{
    private static object NewPartial(CallArgumentValue[] bound, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var type = typeof(LythonRuntime).GetNestedType("PyPartial", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PyPartial not found.");
        var ctor = type.GetConstructors().Single();
        return ctor.Invoke([null, bound, context.MemoryGovernor, span]);
    }

    private static object NewDispatcher(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var type = typeof(LythonRuntime).GetNestedType("PySingleDispatchDispatcher", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PySingleDispatchDispatcher not found.");
        var ctor = type.GetConstructors().Single();
        return ctor.Invoke([null, false, context.MemoryGovernor, span]);
    }

    private static void Register(object dispatcher, object typeSpec, LythonSourceSpan span)
    {
        var method = dispatcher.GetType().GetMethod("Register", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Register not found.");
        method.Invoke(dispatcher, [typeSpec, null, span]);
    }

    private static object NewLruWrapper(LythonRuntime.ExecutionContext context)
    {
        var wrapperType = typeof(LythonRuntime).GetNestedType("PyLruCacheWrapper", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PyLruCacheWrapper not found.");
        var keyModeType = typeof(LythonRuntime).GetNestedType("CacheKeyMode", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CacheKeyMode not found.");
        var ctor = wrapperType.GetConstructors().Single();
        return ctor.Invoke([null, 8, Enum.ToObject(keyModeType, 0), context.MemoryGovernor]);
    }

    [Fact]
    public void PartialBoundArgumentsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = NewPartial(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            context,
            span);
        Assert.Equal(32L + (32L * 3), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DispatchRegistrationCommitsAndReplaceReuses()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var dispatcher = NewDispatcher(context, span);
        var first = new PyType("T1", [], new Dictionary<string, object>());
        var second = new PyType("T2", [], new Dictionary<string, object>());
        Register(dispatcher, first, span);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        Register(dispatcher, second, span);
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Register(dispatcher, first, span);
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void WrapperMetadataOverwritesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var wrapper = (IPyMutableDynamicAttributes)NewLruWrapper(context);
        Assert.True(wrapper.TrySetMember("a", 1));
        Assert.True(wrapper.TrySetMember("b", 2));
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(wrapper.TrySetMember("a", 3));
        Assert.Equal(128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}