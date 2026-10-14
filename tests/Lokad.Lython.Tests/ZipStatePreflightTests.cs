using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ZipStatePreflightTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorPublishesItsOwnerAndRefundsFailedInitialization(bool fail)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var probe = new ConstructorProbe(fail);
        context.Variables["probe"] = probe;
        var parsed = LythonFrontend.Compile("class Source:\n    def __iter__(self):\n        probe()\n        return iter([1])\nreturn Source()\n");
        Assert.Empty(parsed.Diagnostics);
        var code = ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
        var source = LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.NotNull(source);
        var zip = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["zip"]);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        if (fail)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => zip.Invoke(arguments, Span, context));
            Assert.Equal("controlled constructor failure", failure.Message);
            Assert.NotNull(probe.Owner);
            Assert.Equal(0, probe.Owner.CommittedStorageBytes);
        }
        else
        {
            var result = zip.Invoke(arguments, Span, context);
            Assert.Same(probe.Owner, result);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.State.ActiveExecutionRoot);
    }

    private sealed class ConstructorProbe(bool fail) : LythonRuntime.ICallable
    {
        internal PyZipIterator? Owner;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            IMemoryStorageEdges? current = context.State.ActiveExecutionRoot;
            while (current is not null)
            {
                if (current is LythonRuntime.NativeIterableOperation operation)
                {
                    Owner = Assert.IsType<PyZipIterator>(operation.Result);
                    break;
                }
                current = current is ExecutionFrame scope ? scope.ActiveCaller
                    : ((LythonRuntime.ExecutableFrameState)current).ActiveCaller;
            }
            Assert.NotNull(Owner);
            if (fail) throw new InvalidOperationException("controlled constructor failure");
            return PyNone.Instance;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialRowsRemainStoredAcrossReentrantPulls(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        context.Variables.Clear();
        var payload = PyString.FromString(new string('x', 16000));
        var first = new ValueSource(payload);
        var second = new ReentrantSource(context);
        var zip = new PyZipIterator([first, second], false, Span, context);
        second.Zip = zip;
        var result = asynchronous ? (await zip.TryMoveNextAsync()).Value : Pull(zip);
        var row = Assert.IsType<PyTuple>(result);
        Assert.Same(payload, row[0]);
        Assert.True(second.Checked);
        Assert.Null(context.State.ActiveExecutionRoot);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static object Pull(PyZipIterator zip)
    {
        Assert.True(zip.TryMoveNext(out var value));
        return value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedRowPreflightDoesNotConsumeOrReplaceTheCaller(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new CountingSource();
        var zip = new PyZipIterator([source], false, Span, context);
        using var caller = LythonRuntime.NativeIterableOperation.Create(PyNone.Instance, PyNone.Instance, context, Span);
        // The pull operation fits, but its row buffer does not.
        var funding = context.MemoryGovernor.MaxAccountedBytes!.Value
            - context.MemoryGovernor.CurrentCommittedBytes - context.MemoryGovernor.CurrentReservedBytes - 128;
        context.MemoryGovernor.Reserve(funding, Span);
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
                : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(0, source.Pulls);
            Assert.Same(caller, context.State.ActiveExecutionRoot);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        }
        finally { context.MemoryGovernor.ReleaseReserved(funding); }
        Assert.True(asynchronous ? (await zip.TryMoveNextAsync()).HasValue : zip.TryMoveNext(out _));
        Assert.Equal(1, source.Pulls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedTupleRegistrationRefundsOnlyTheFreshRow(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new CountingSource();
        var zip = new PyZipIterator([source], false, Span, context);
        var held = Assert.IsType<PyTuple>(asynchronous ? (await zip.TryMoveNextAsync()).Value : Pull(zip));
        var rowBytes = PyTuple.EstimateStorageBytes([BigInteger.One]);
        // Admit the helper, row buffer and tuple; deny its new registry entry.
        var funding = context.MemoryGovernor.MaxAccountedBytes!.Value
            - context.MemoryGovernor.CurrentCommittedBytes - 128 - (32 + 80) - rowBytes;
        context.MemoryGovernor.Reserve(funding, Span);
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await zip.TryMoveNextAsync())
                : Assert.Throws<LythonRuntimeException>(() => zip.TryMoveNext(out _));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(2, source.Pulls);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(BigInteger.One, held[0]);
            Assert.Null(context.State.ActiveExecutionRoot);
        }
        finally { context.MemoryGovernor.ReleaseReserved(funding); }
        var next = Assert.IsType<PyTuple>(asynchronous ? (await zip.TryMoveNextAsync()).Value : Pull(zip));
        Assert.NotSame(held, next);
        Assert.Equal(3, source.Pulls);
    }

    private sealed class CountingSource : IPyAsyncIteratorValue
    {
        internal int Pulls;
        public bool TryMoveNext(out object value) { Pulls++; value = BigInteger.One; return true; }
        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            await Task.Yield();
            Pulls++;
            return PyIterationResult.Yield(BigInteger.One);
        }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public void ConstructorFundingCoversMeasuredCursorStorage(int count)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var source = new PyList([BigInteger.One]);
        var sources = Enumerable.Repeat<object>(source, count).ToArray();
        _ = new PyZipIterator(sources, false, Span, context);
        var initial = context.MemoryGovernor.CurrentCommittedBytes;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var iterator = new PyZipIterator(sources, false, Span, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var admitted = context.MemoryGovernor.CurrentCommittedBytes - initial;
        Assert.True(admitted >= allocated, $"admitted {admitted}, allocated {allocated}");
        GC.KeepAlive(iterator);
    }

    private sealed class ValueSource(object payload) : IPyAsyncIteratorValue, IMemoryStorageEdges
    {
        public bool TryMoveNext(out object value) { value = payload; return true; }
        public ValueTask<PyIterationResult> TryMoveNextAsync() => ValueTask.FromResult(PyIterationResult.Yield(payload));
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
        public void VisitStorageChildren(MemoryStorageWalk visitor) => visitor.Add(payload);
    }

    private sealed class ReentrantSource(LythonRuntime.ExecutionContext context) : IPyAsyncIteratorValue, IMemoryStorageEdges
    {
        internal PyZipIterator Zip = null!;
        internal bool Checked;
        private bool _inside;
        private void Inspect()
        {
            var current = Assert.IsType<LythonRuntime.NativeIterableOperation>(context.State.ActiveExecutionRoot);
            var parent = Assert.IsType<LythonRuntime.NativeIterableOperation>(current.ActiveCaller);
            var outerRow = Assert.IsType<object[]>(parent.Current);
            Assert.Equal(16000, Assert.IsType<PyString>(outerRow[0]).Utf8Bytes.Length);
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 256);
            Assert.True(walk.TryTraverse([current]), walk.UnsupportedType?.FullName);
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, outerRow));
            Checked = true;
        }
        public bool TryMoveNext(out object value)
        {
            if (_inside) Inspect();
            else
            {
                _inside = true;
                try { _ = Pull(Zip); }
                finally { _inside = false; }
            }
            value = BigInteger.One;
            return true;
        }
        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            await Task.Yield();
            if (_inside) Inspect();
            else
            {
                _inside = true;
                try { Assert.True((await Zip.TryMoveNextAsync()).HasValue); }
                finally { _inside = false; }
            }
            return PyIterationResult.Yield(BigInteger.One);
        }
        public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
        public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);
        public void VisitStorageChildren(MemoryStorageWalk visitor)
        {
            if (visitor.Add(Zip)) visitor.Add(context);
        }
    }
}
