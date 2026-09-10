using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: kwargs-derived keys in fresh collections own their string payload
/// beside the already-governed backing, matching the binder kwargs-key rule.
/// </summary>
public sealed class CollectionKwargsKeyAccountingTests
{
    private static object InvokeFactory(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, params CallArgumentValue[] arguments)
    {
        var method = typeof(LythonRuntime).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => m.Name == name
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType == typeof(CallArgumentValue[]));
        return method.Invoke(null, [arguments, span, context])!;
    }

    private static PyString SingleKey(PyDict dict, MemoryGovernor governor)
    {
        var key = dict.Keys.OfType<PyString>().Single();
        Assert.Same(governor, key.OwnerMemoryGovernor);
        return key;
    }

    [Fact]
    public void CounterKwargsKeysCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var counter = Assert.IsType<PyCounter>(InvokeFactory("Counter", context, span, CallArgumentValue.Keyword("k", new BigInteger(1))));
        var pair = counter.Single();
        var key = Assert.IsType<PyString>(pair.Key);
        Assert.Same(context.MemoryGovernor, key.OwnerMemoryGovernor);
        Assert.Equal("k", key.AsString());
        // Shell (64) plus the governed backing (192) and key (128 + 1).
        Assert.Equal(385L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DefaultDictKwargsKeysCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var dict = Assert.IsType<PyDefaultDict>(InvokeFactory("DefaultDict", context, span, CallArgumentValue.Keyword("k", new BigInteger(1))));
        var key = Assert.IsType<PyString>(dict.Single().Key);
        Assert.Same(context.MemoryGovernor, key.OwnerMemoryGovernor);
        Assert.Equal("k", key.AsString());
        // Shell (64) plus the governed backing (192) and key (128 + 1).
        Assert.Equal(385L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void OrderedDictKwargsKeysCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var dict = Assert.IsType<PyDict>(InvokeFactory("OrderedDict", context, span, CallArgumentValue.Keyword("k", new BigInteger(1))));
        Assert.Equal("k", SingleKey(dict, context.MemoryGovernor).AsString());
        // Governed backing (192) and key (128 + 1); no extra shell.
        Assert.Equal(321L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}