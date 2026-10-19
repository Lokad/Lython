using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
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
    public void EscapedViewOrIteratorPreservesMappingValuesAndBatchFunding(bool counter, bool iterator, string member)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var (alias, _) = MakeAlias(context, counter, iterator, member);
        var funded = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(funded, context.MemoryGovernor.CurrentCommittedBytes);
        var cursor = iterator ? (IEnumerator<object>)alias : ((IEnumerable<object>)alias).GetEnumerator();
        using (cursor)
        {
            Assert.True(cursor.MoveNext());
            if (member == "keys") Assert.Equal("key", Assert.IsType<PyString>(cursor.Current).AsString());
            else if (member == "values") Assert.Equal(new BigInteger(7), cursor.Current);
            else
            {
                var pair = Assert.IsType<PyTuple>(cursor.Current);
                Assert.Equal("key", Assert.IsType<PyString>(pair[0]).AsString());
                Assert.Equal(new BigInteger(7), pair[1]);
            }
            Assert.False(cursor.MoveNext());
        }
        GC.KeepAlive(alias);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DroppedAliasAndItsWrapperCycleAreCollected(bool counter, bool iterator)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
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
            Assert.True(LythonRuntime.CounterMembers.TryGetMember(value, member, out callable));
            wrapper = value;
        }
        else
        {
            var value = new PyDefaultDict(PyNone.Instance, context.MemoryGovernor, span);
            value.SetItem(key, new BigInteger(7));
            Assert.True(LythonRuntime.DefaultDictMembers.TryGetMember(value, member, out callable));
            wrapper = value;
        }
        var view = Assert.IsAssignableFrom<LythonRuntime.ICallable>(callable).Invoke([], span, context);
        var alias = iterator ? ((IEnumerable<object>)view).GetEnumerator() : view;
        return (alias, new WeakReference<object>(wrapper));
    }
}
