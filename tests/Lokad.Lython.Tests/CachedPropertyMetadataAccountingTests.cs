using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG14: cached_property descriptors own their metadata table like function
/// metadata tables; overwrites stay free. The factory carries the governor,
/// so there is no ungoverned case.
/// </summary>
public sealed class CachedPropertyMetadataAccountingTests
{
    private sealed class StubCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            _ = arguments;
            _ = span;
            _ = context;
            return PyNone.Instance;
        }
    }

    private static IPyMutableDynamicAttributes CreateDescriptor(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var factoryType = typeof(LythonRuntime).GetNestedType("CachedPropertyFactory", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CachedPropertyFactory not found.");
        var instance = factoryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("CachedPropertyFactory.Instance not found.");
        var result = factoryType.GetMethod("Invoke")?.Invoke(instance, [new[] { CallArgumentValue.Positional(new StubCallable()) }, span, context])
            ?? throw new InvalidOperationException("cached_property factory returned null.");
        return Assert.IsAssignableFrom<IPyMutableDynamicAttributes>(result);
    }

    [Fact]
    public void MetadataSlotsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var descriptor = CreateDescriptor(context, span);
        Assert.True(descriptor.TrySetMember("a", PyNone.Instance));
        Assert.True(descriptor.TrySetMember("a", PyNone.Instance));
        Assert.True(descriptor.TrySetMember("b", PyNone.Instance));
        Assert.True(descriptor.TrySetMember("c", PyNone.Instance));
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}