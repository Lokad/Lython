using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG14: cache keys own their keyword-name and type-token strings beside the
/// governed backing, matching the documented key-ownership invariant, so
/// retained entries accumulate instead of riding invisible.
/// </summary>
public sealed class CacheKeyAccountingTests
{
    private static PyTuple BuildKey(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int mode, params CallArgumentValue[] arguments)
    {
        var keyModeType = typeof(LythonRuntime).GetNestedType("CacheKeyMode", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CacheKeyMode not found.");
        var method = typeof(LythonRuntime).GetMethod("BuildCacheKey", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuildCacheKey not found.");
        return Assert.IsType<PyTuple>(method.Invoke(null, [arguments, Enum.ToObject(keyModeType, mode), context, span]));
    }

    [Fact]
    public void KeywordNamesAreOwned()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var key = BuildKey(context, span, 0, CallArgumentValue.Keyword("k", new BigInteger(1)));
        Assert.Equal(3, key.Count);
        var name = Assert.IsType<PyString>(key[1]);
        Assert.Same(context.MemoryGovernor, name.OwnerMemoryGovernor);
        Assert.Equal("k", name.AsString());
        // Tuple backing (80) plus the keyword name (128 + 1).
        Assert.Equal(209L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void TypeTokensAreOwned()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var key = BuildKey(context, span, 1, CallArgumentValue.Positional(new BigInteger(1)));
        Assert.Equal(3, key.Count);
        var token = Assert.IsType<PyString>(key[2]);
        Assert.Same(context.MemoryGovernor, token.OwnerMemoryGovernor);
        Assert.Equal("int", token.AsString());
        // Tuple backing (80) plus the type token (128 + 3).
        Assert.Equal(211L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}