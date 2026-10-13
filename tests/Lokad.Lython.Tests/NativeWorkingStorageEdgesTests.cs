using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeWorkingStorageEdgesTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    [Fact]
    public void DeferredSplitWalkDoesNotAcquireOwnershipOrChangeFunding()
    {
        var context = Context();
        var first = PyString.FromString("first", context.MemoryGovernor);
        var second = PyString.FromString("second", context.MemoryGovernor);
        var list = new PyList(new object[] { first, second }, context.MemoryGovernor);
        Assert.True(list.TryDeferFreshSplitOwnership(context.State.CallTemporaries, null));
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var registrations = context.State.CallTemporaries.Count;
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 64);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([list]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(registrations, context.State.CallTemporaries.Count);
        Assert.Null(first.ReclamationEntry);
        Assert.Null(second.ReclamationEntry);
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, first));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, second));
        // Actual exposure still acquires precisely that part afterwards.
        Assert.Same(first, list[0]);
        Assert.NotNull(first.ReclamationEntry);
        Assert.Null(second.ReclamationEntry);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    public void PartialListStorageExposesItsActualChildren(int count)
    {
        var governor = new MemoryGovernor(100000);
        var payload = PyString.FromString("payload", governor);
        var storage = PyListStorage.Create(Enumerable.Repeat<object>(payload, count), governor, null);
        var committed = governor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(governor, 128);
        Assert.True(walk.TryTraverse([storage]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        storage.Clear();
        Assert.True(walk.TryTraverse([storage]));
        Assert.Single(walk.Visited.ToArray());
        governor.Release(storage.ReleaseCommittedBytes());
    }

    [Fact]
    public void ExpandedArgumentShellFundingCoversItsMeasuredConstruction()
    {
        var context = Context();
        var target = new Leaf();
        using (var warm = new CallExpansion.CallArgumentAccumulator(0, context, target)) { }
        var initial = context.MemoryGovernor.CurrentReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var buffer = new CallExpansion.CallArgumentAccumulator(0, context, target);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var admitted = context.MemoryGovernor.CurrentReservedBytes - initial;
        Assert.True(admitted >= allocated, $"admitted {admitted}, allocated {allocated}");
    }

    [Fact]
    public void SmallSortFundingCoversItsMeasuredWorkingStorage()
    {
        var context = Context();
        var high = new PyStableSort.Entry(PyString.FromString("high"), new BigInteger(2));
        var low = new PyStableSort.Entry(PyString.FromString("low"), BigInteger.One);
        Func<object, object, bool> compare = static (left, right) => (BigInteger)left < (BigInteger)right;
        using (var warm = new PyStableSort.Buffer(context.MemoryGovernor, null))
        {
            warm.Add(high, null);
            warm.Add(low, null);
            warm.Sort(false, compare, context, null);
        }
        var initial = context.MemoryGovernor.CurrentReservedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null);
        buffer.Add(high, null);
        buffer.Add(low, null);
        buffer.Sort(false, compare, context, null);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var admitted = context.MemoryGovernor.CurrentReservedBytes - initial;
        Assert.True(admitted >= allocated, $"admitted {admitted}, allocated {allocated}");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeferredCallSourceRemainsStoredDuringExpansionAndRetirementDropsOnlyItsReferences(bool asynchronous, bool retained)
    {
        var context = Context();
        var target = new Leaf();
        using var buffer = new CallExpansion.CallArgumentAccumulator(1, context, target, retained: retained, deferSingleStar: true);
        var payload = PyString.FromString("arg", context.MemoryGovernor);
        var source = new DeferredSource(buffer, context, payload);
        await buffer.AppendAsync(CallArgumentForm.StarredList, source, Span, asynchronous);
        using (var walk = new MemoryStorageWalk(context.MemoryGovernor, 128))
        {
            Assert.True(walk.TryTraverse([buffer]));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, source));
        }
        await buffer.FinishDeferredStarAsync(asynchronous);
        Assert.True(source.Checked);
        var escaped = buffer.ToArray();
        Assert.Same(payload, Assert.Single(escaped).Value);
        var before = GC.GetAllocatedBytesForCurrentThread();
        buffer.Dispose();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        using var retiredWalk = new MemoryStorageWalk(context.MemoryGovernor, 128);
        Assert.True(retiredWalk.TryTraverse([buffer]));
        Assert.DoesNotContain(retiredWalk.Visited.ToArray(), value => ReferenceEquals(value, context) || ReferenceEquals(value, payload) || ReferenceEquals(value, target));
        Assert.Same(payload, Assert.Single(escaped).Value);
        Assert.Throws<ObjectDisposedException>(() => buffer.Add(CallArgumentValue.Positional(payload), null));
        Assert.Throws<ObjectDisposedException>(() => buffer.ToArray());
    }

    [Fact]
    public void FailedRetainedBufferRegistrationRefundsTheUnpublishedStorage()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 320 });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() => new CallExpansion.CallArgumentAccumulator(0, context, new Leaf(), retained: true));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.CallTemporaries.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveSortVisitsMergeScratchAndRetiresItOnCompletion(bool asynchronous)
    {
        var context = Context();
        using var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null);
        var values = Enumerable.Range(0, 4).Select(i => PyString.FromString("value" + i)).ToArray();
        for (var i = 0; i < values.Length; i++) buffer.Add(new(values[i], new BigInteger(4 - i)), null);
        var comparer = new StoredComparer(buffer, context, values);
        if (asynchronous) await buffer.SortAsync(false, comparer.CompareAsync, context, null);
        else buffer.Sort(false, comparer.Compare, context, null);
        Assert.True(comparer.Checked > 0);
        Assert.True(comparer.ScratchWasNeeded);
        Assert.Equal(values.Reverse(), buffer.Cast<PyString>());
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 128);
        Assert.True(walk.TryTraverse([buffer]));
        Assert.DoesNotContain(walk.Visited.ToArray(), value => value is PyStableSort.Entry[] || ReferenceEquals(value, comparer) || ReferenceEquals(value, context));
        var before = GC.GetAllocatedBytesForCurrentThread();
        buffer.Dispose();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(walk.TryTraverse([buffer]));
        Assert.DoesNotContain(walk.Visited.ToArray(), value => value is PyString);
        Assert.Empty(buffer);
    }

    [Fact]
    public void UnknownComparerCapturePreventsCompletionAndFailureStillClearsActiveSortState()
    {
        var context = Context();
        using var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null);
        buffer.Add(new(PyString.FromString("a"), new BigInteger(2)), null);
        buffer.Add(new(PyString.FromString("b"), BigInteger.One), null);
        var failure = new InvalidOperationException("comparison failed");
        bool Compare(object left, object right)
        {
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 128);
            Assert.False(walk.TryTraverse([buffer]));
            Assert.Equal(MemoryStorageWalk.IncompleteReason.UnknownShape, walk.Incomplete);
            throw failure;
        }
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => buffer.Sort(false, Compare, context, null)));
        using var retired = new MemoryStorageWalk(context.MemoryGovernor, 128);
        Assert.True(retired.TryTraverse([buffer]));
        buffer.Dispose();
        Assert.Equal(retired.FundedBytes, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void EmptySortRecordsStillConsumeTheWorkBound()
    {
        var context = Context();
        using var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null);
        for (var i = 0; i < 1000; i++) buffer.Add(new(PyNone.Instance, PyNone.Instance), null);
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 16);
        Assert.False(walk.TryTraverse([buffer]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedCallKeepsTheActualArgumentBufferPublishedUntilInvocationCompletes(bool asynchronous)
    {
        var context = Context();
        var probe = new PreparedProbe();
        context.Variables["target"] = probe;
        var parsed = LythonFrontend.Compile("def f():\n    yield target(*(yield 'need'))\n");
        Assert.Empty(parsed.Diagnostics);
        var code = ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
        LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        var generator = Assert.IsType<LythonRuntime.PyGenerator>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["f"]).Invoke([], Span, context));
        Assert.True(generator.TryMoveNext(out var request));
        Assert.Equal("need", Assert.IsType<PyString>(request).AsString());
        Assert.True(generator.TryGetMember("send", out var send));
        var payload = PyString.FromString(new string('x', 16000), context.MemoryGovernor);
        var values = new PyList(new object[] { payload }, context.MemoryGovernor);
        var member = Assert.IsAssignableFrom<LythonRuntime.ICallable>(send);
        var result = asynchronous ? await member.InvokeAsync([CallArgumentValue.Positional(values)], Span, context)
            : member.Invoke([CallArgumentValue.Positional(values)], Span, context);
        Assert.Equal(BigInteger.One, result);
        Assert.True(probe.Checked);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LythonRuntime.ExecutionContext Context()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        context.Variables.Clear();
        return context;
    }

    private sealed class Leaf : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => PyNone.Instance;
    }

    private sealed class PreparedProbe : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        internal bool Checked;
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var payload = Assert.IsType<PyString>(Assert.Single(arguments).Value);
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 1024);
            Assert.True(walk.TryTraverse([context.CurrentExecutableFrame!]));
            Assert.Contains(walk.Visited.ToArray(), value => value is CallExpansion.CallArgumentAccumulator);
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
            Checked = true;
            return BigInteger.One;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class DeferredSource(CallExpansion.CallArgumentAccumulator buffer, LythonRuntime.ExecutionContext context, PyString payload)
        : IPyAsyncIterableValue, IMemoryStorageEdges
    {
        internal bool Checked;
        public void VisitStorageChildren(MemoryStorageWalk visitor) => visitor.Add(payload);
        private void Check()
        {
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 128);
            Assert.True(walk.TryTraverse([buffer]));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, this));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
            Checked = true;
        }
        public IEnumerable<object> Iterate()
        {
            Check();
            yield return payload;
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.Yield();
            Check();
            yield return payload;
        }
    }

    private sealed class StoredComparer(PyStableSort.Buffer buffer, LythonRuntime.ExecutionContext context, PyString[] expected) : IMemoryStorageEdges
    {
        internal int Checked;
        internal bool ScratchWasNeeded;
        public void VisitStorageChildren(MemoryStorageWalk visitor) { } // External observer references.
        public bool Compare(object left, object right)
        {
            var visible = buffer.ToArray();
            if (expected.Any(value => !visible.Any(item => ReferenceEquals(item, value)))) ScratchWasNeeded = true;
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 256);
            Assert.True(walk.TryTraverse([buffer]));
            Assert.Contains(walk.Visited.ToArray(), value => value is PyStableSort.Entry[]);
            foreach (var value in expected) Assert.Contains(walk.Visited.ToArray(), item => ReferenceEquals(item, value));
            Assert.Throws<InvalidOperationException>(buffer.Dispose);
            Assert.Throws<InvalidOperationException>(() => buffer.Sort(false, Compare, context, null));
            Checked++;
            return (BigInteger)left < (BigInteger)right;
        }
        public async ValueTask<bool> CompareAsync(object left, object right)
        {
            await Task.Yield();
            return Compare(left, right);
        }
    }
}
