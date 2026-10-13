using System.Collections;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class MemoryStorageWalkTests
{
    [Fact]
    public void CyclesAndAliasesVisitStoredValuesWithoutAllocatingOrRefunding()
    {
        var governor = new MemoryGovernor(64000);
        var payload = PyString.FromString(new string('x', 16000), governor);
        var list = new PyList([payload, payload]);
        var tuple = new PyTuple([list, payload]);
        list.Add(tuple);
        var dict = new PyDict();
        dict.SetItem(PyString.FromString("cycle"), list);
        dict.SetItem(PyString.FromString("self"), dict);
        var set = new PySet([payload]);
        var deque = new PyDeque(); deque.Append(tuple);
        var root = new object[] { dict, set, deque };
        var guestCharge = governor.CurrentCommittedBytes;
        using (var walk = new MemoryStorageWalk(governor, 64))
        {
            Assert.Equal(walk.FundedBytes, governor.CurrentReservedBytes);
            Assert.True(walk.TryTraverse(root));
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(walk.TryTraverse(root));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
            Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, list)));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, tuple));
            Assert.Equal(guestCharge, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(guestCharge, governor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void DictionaryStorageVisitsKeysAndValuesWithoutGuestHashOrEquality(int count)
    {
        var dictionary = new PyDict();
        var payload = PyString.FromString("payload");
        for (var i = 0; i < count; i++) dictionary.SetItem(new BigInteger(i), payload);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 64);
        Assert.True(walk.TryTraverse([dictionary]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        Assert.Equal(2, walk.Visited.Length); // Numeric slots need no identity tracking.
    }

    [Fact]
    public void DelegateReceiverAndFrontendCaptureReachTheSamePayload()
    {
        var payload = PyString.FromString("payload");
        var list = new PyList([payload]);
        var reversed = new PyReversedIterator(list.Length, list.GetIndex);
        var parsed = LythonFrontend.Compile("1");
        var syntax = Assert.IsType<ExpressionStatementSyntax>(parsed.Script.RequireNotNull().Statements[0]).Expression;
        var captured = new LoweredCapturedExpression(syntax, list);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 16);
        Assert.True(walk.TryTraverse([reversed, captured]));
        Assert.Equal(5, walk.Visited.Length); // Includes the actual list backing owner.
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
    }

    [Fact]
    public void UnknownShapesPreventCompletionWithoutCallingTheirProtocols()
    {
        var hostile = new Hostile();
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 8);
        Assert.False(walk.TryTraverse([new PyList([hostile])]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.UnknownShape, walk.Incomplete);
        Assert.Equal(typeof(Hostile), walk.UnsupportedType);
        Assert.False(walk.TryTraverse([new object()]));
    }

    [Fact]
    public void CapacityFailureHasNoGrowthAndWorkspaceCanBeReused()
    {
        var governor = new MemoryGovernor(10000);
        using var walk = new MemoryStorageWalk(governor, 1);
        var funding = governor.CurrentReservedBytes;
        Assert.False(walk.TryTraverse([new PyList([PyString.FromString("x")])]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
        Assert.Equal(funding, governor.CurrentReservedBytes);
        Assert.True(walk.TryTraverse([PyString.FromString("y")]));
        Assert.Equal(1, walk.Visited.Length);
    }

    [Fact]
    public void RepeatedAliasesAndScalarEdgesCannotCauseUnboundedScanning()
    {
        var payload = PyString.FromString("payload");
        var aliases = Enumerable.Repeat<object>(payload, 10000).ToArray();
        var scalars = Enumerable.Repeat<object>(BigInteger.One, 10000).ToArray();
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 16);
        Assert.False(walk.TryTraverse([aliases]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
        Assert.False(walk.TryTraverse([scalars]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
        Assert.True(walk.TryTraverse([payload]));
    }

    [Fact]
    public void EmptyCsvHistoryRowsStillConsumeTheWorkBudget()
    {
        var governor = new MemoryGovernor(64000);
        var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, null,
            LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
        var writer = new LythonRuntime.CsvWriterObject(options, null, governor, new(0, 0, 0, 0));
        for (var i = 0; i < 1000; i++) writer.AppendHistory([], 32, new(0, 0, 0, 0));
        var committed = governor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(governor, 16);
        Assert.False(walk.TryTraverse([writer]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void CachedUnicodeRunesAreStoredChildrenWithoutBuildingAnotherCache()
    {
        var text = PyString.FromString("é😀");
        var runes = text.GetRunes();
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 32);
        Assert.True(walk.TryTraverse([text]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, runes));
        foreach (var rune in runes)
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, rune));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([text]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void PrimedReaderReachesItsActualDecodedWindowAndWithdrawsItOnRelease()
    {
        var host = new MockLythonHost();
        host.SeedFile("/file", "café 😀\n");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState("/file", context,
            LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.TranslateUniversal, 32);
        try
        {
            reader.Prime();
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 32);
            Assert.True(walk.TryTraverse([reader]));
            var window = Assert.Single(walk.Visited.ToArray().OfType<PyString>());
            Assert.Equal("café 😀\n", window.AsString());
            reader.Release();
            Assert.True(walk.TryTraverse([reader]));
            Assert.DoesNotContain(walk.Visited.ToArray(), value => ReferenceEquals(value, window));
        }
        finally { reader.Release(); }
    }

    [Fact]
    public void ViewsExceptionCyclesAndDescriptorDocumentationReachStoredAliases()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var payload = PyString.FromString("payload");
        var dictionary = new PyDict();
        dictionary.SetItem(PyString.FromString("key"), payload);
        var exception = new PyException("ValueError", "control", payload) { CustomDict = dictionary };
        exception.Cause = exception;
        exception.Context = new PyException("TypeError", "control", new PyTuple([payload, exception]));
        var property = PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, payload,
            context, new(0, 0, 0, 0));
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 128);
        Assert.True(walk.TryTraverse([new LythonRuntime.DictKeysView(dictionary),
            new LythonRuntime.DictValuesView(dictionary), new LythonRuntime.DictItemsView(dictionary),
            exception, property]));
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, dictionary)));
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, exception.Context));
    }

    [Fact]
    public void TypeTraversalIncludesTheRetainedBasesCollectionAfterConstruction()
    {
        var bases = new List<PyType>();
        var type = new PyType("Owner", bases, new());
        var payload = PyString.FromString("late");
        var added = new PyType("Added", [], new() { ["payload"] = payload });
        bases.Add(added); // The immutable bases tuple predates this retained reference.
        var instance = new PyInstance(type);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 128);
        Assert.True(walk.TryTraverse([instance]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, added));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
    }

    [Fact]
    public void MulticastAndAnonymousDelegateCapturesPreventCompletion()
    {
        var first = new PyList([PyString.FromString("first")]);
        var last = new PyList([PyString.FromString("last")]);
        Func<int, object> callbacks = first.GetIndex;
        callbacks += last.GetIndex;
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 32);
        Assert.False(walk.TryTraverse([new PyReversedIterator(1, callbacks)]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.UnknownShape, walk.Incomplete);
        Assert.False(walk.TryTraverse([new PyReversedIterator(1, index => first.GetIndex(index))]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.UnknownShape, walk.Incomplete);
    }

    [Fact]
    public void CancellationDuringChildScanningClearsTheReusableWorkspace()
    {
        using var cancel = new CancellationTokenSource();
        using var walk = new MemoryStorageWalk(new MemoryGovernor(10000), 256);
        Assert.Throws<OperationCanceledException>(() => walk.TryTraverse([new CancellingStorage(cancel)], cancel.Token));
        Assert.True(walk.Visited.IsEmpty);
        Assert.True(walk.TryTraverse([PyString.FromString("reuse")]));
    }

    [Fact]
    public void PreflightDenialPublishesNoWorkspaceAndStrandsNoReservation()
    {
        var governor = new MemoryGovernor(64);
        var error = Assert.Throws<LythonRuntimeException>(() => new MemoryStorageWalk(governor, 1));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void CancellationWithdrawsVisitedReferencesAndDisposeReleasesOnlyScratch()
    {
        var governor = new MemoryGovernor(10000);
        using var cancel = new CancellationTokenSource();
        var walk = new MemoryStorageWalk(governor, 16);
        var value = new PyList([PyString.FromString("payload")]);
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => walk.TryTraverse([value], cancel.Token));
        Assert.True(walk.Visited.IsEmpty);
        Assert.True(governor.CurrentReservedBytes > 0);
        walk.Dispose();
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Throws<ObjectDisposedException>(() => walk.TryTraverse([value]));
    }

    private sealed class Hostile : IPyDynamicAttributes, IPyOwnershipSnapshot, IEnumerable<object>
    {
        public override int GetHashCode() => throw new InvalidOperationException("guest hash");
        public override bool Equals(object? obj) => throw new InvalidOperationException("guest equality");
        public bool TryGetMember(string name, out object value) => throw new InvalidOperationException("guest getter");
        public bool TrySnapshotOwnership(out long bytes) => throw new InvalidOperationException("snapshot callback");
        public IEnumerator<object> GetEnumerator() => throw new InvalidOperationException("guest iterator");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancellingStorage(CancellationTokenSource cancellation) : IMemoryStorageEdges
    {
        public void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            cancellation.Cancel();
            for (var i = 0; i < 256; i++) if (!visitor.Add(PyNone.Instance)) return;
        }
    }
}
