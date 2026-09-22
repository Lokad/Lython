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
        // N08: BuildCacheKey reports the explicit key charge alongside the tuple.
        var invokeArgs = new object?[] { arguments, Enum.ToObject(keyModeType, mode), context, span, 0L };
        return Assert.IsType<PyTuple>(method.Invoke(null, invokeArgs));
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
        // Tuple backing (80) plus the keyword name (128 + 1), plus one 64B coupon
        // for the adopted int value the key tuple retains (N06).
        Assert.Equal(209L + 64L, context.MemoryGovernor.CurrentCommittedBytes - before);
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
        // Tuple backing (80) plus the type token (128 + 3), plus one 64B coupon
        // for the adopted int value the key tuple retains (N06).
        Assert.Equal(211L + 64L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    // Two kennels homing a nested Widget each: both CLR kinds share the Name
    // "Widget" while staying distinct runtime types.
    private sealed class WidgetKennelA
    {
        public sealed class Widget : IPyHashableValue
        {
            public int GetPyHashCode() => 42;

            public override bool Equals(object? obj)
                => obj is Widget || obj is WidgetKennelB.Widget;

            public override int GetHashCode() => 42;
        }
    }

    private sealed class WidgetKennelB
    {
        public sealed class Widget : IPyHashableValue
        {
            public int GetPyHashCode() => 42;

            public override bool Equals(object? obj)
                => obj is Widget || obj is WidgetKennelA.Widget;

            public override int GetHashCode() => 42;
        }
    }

    [Fact]
    public void DistinctHostKindsWithSharedNameStayDistinct()
    {
        // N16: typed-key identity uses runtime type references, never display
        // names. The widgets compare equal by value on purpose, so only the type
        // part can tell the keys apart: display-name tags would collide.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        Assert.Equal("Widget", typeof(WidgetKennelA.Widget).Name);
        Assert.Equal(typeof(WidgetKennelA.Widget).Name, typeof(WidgetKennelB.Widget).Name);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var keyA = BuildKey(context, span, 1, CallArgumentValue.Positional(new WidgetKennelA.Widget()));
        var keyB = BuildKey(context, span, 1, CallArgumentValue.Positional(new WidgetKennelB.Widget()));
        Assert.Equal(3, keyA.Count);
        Assert.Equal(3, keyB.Count);
        Assert.NotEqual(keyA[2], keyB[2]);
        // Tuple backing (80) plus one 64B identity token per key; the widgets are
        // not adoptable scalars, so no coupons ride along.
        Assert.Equal(2 * 144L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
