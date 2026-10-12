using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DictWrapperAliasOriginalTests
{
    [Theory]
    [InlineData(false, false, "keys")]
    [InlineData(false, false, "values")]
    [InlineData(false, false, "items")]
    [InlineData(false, true, "keys")]
    [InlineData(false, true, "values")]
    [InlineData(false, true, "items")]
    [InlineData(true, false, "keys")]
    [InlineData(true, false, "values")]
    [InlineData(true, false, "items")]
    [InlineData(true, true, "keys")]
    [InlineData(true, true, "values")]
    [InlineData(true, true, "items")]
    public void EscapedViewOrIteratorRetainsItsChargedWrapper(bool counter, bool iterator, string member)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (alias, weak) = MakeAlias(context, counter, iterator, member);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.True(weak.TryGetTarget(out _), "Live mapping storage lost its charged wrapper owner.");
        GC.KeepAlive(alias);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DroppedAliasAndItsWrapperCycleAreCollected(bool counter, bool iterator)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var (alias, owner) = MakeDroppedAlias(context, counter, iterator);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(alias.TryGetTarget(out _));
        Assert.False(owner.TryGetTarget(out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<object> Alias, WeakReference<object> Owner) MakeDroppedAlias(
        LythonRuntime.ExecutionContext context, bool counter, bool iterator)
    {
        var (alias, owner) = MakeAlias(context, counter, iterator, "values");
        return (new WeakReference<object>(alias), owner);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Alias, WeakReference<object> Owner) MakeAlias(
        LythonRuntime.ExecutionContext context, bool counter, bool iterator, string member)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        object wrapper;
        object? callable;
        var key = PyString.FromString("key");
        if (counter)
        {
            var value = new PyCounter(context.MemoryGovernor, span);
            value.SetItem(key, new BigInteger(7));
            context.Services.State.CallTemporaries.TrackFreshMutable(value, value.CommittedStorageBytes, span);
            Assert.True(LythonRuntime.CounterMembers.TryGetMember(value, member, out callable));
            wrapper = value;
        }
        else
        {
            var value = new PyDefaultDict(PyNone.Instance, context.MemoryGovernor, span);
            value.SetItem(key, new BigInteger(7));
            context.Services.State.CallTemporaries.TrackFreshMutable(value, value.CommittedStorageBytes, span);
            Assert.True(LythonRuntime.DefaultDictMembers.TryGetMember(value, member, out callable));
            wrapper = value;
        }
        var view = Assert.IsAssignableFrom<LythonRuntime.ICallable>(callable).Invoke([], span, context);
        var alias = iterator ? ((IEnumerable<object>)view).GetEnumerator() : view;
        return (alias, new WeakReference<object>(wrapper));
    }
}
