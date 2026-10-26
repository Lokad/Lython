using System.Collections;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class EnumerableStateTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserIteratorAcquisitionKeepsItsTimingAndEachEnumerationResolvesOnce(bool asynchronous)
    {
        var context = Context();
        var probe = new AcquisitionProbe();
        context.Variables["probe"] = probe;
        context.Variables["iter"] = context.State.BuiltinVariables["iter"];
        var parsed = LythonFrontend.Compile("class Source:\n    def __iter__(self):\n        probe()\n        return iter([1])\nreturn Source()\n",regexProvider:Utf8RegexProvider.Instance);
        Assert.Empty(parsed.Diagnostics);
        var code = ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
        var source = LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        Assert.NotNull(source);
        if (asynchronous)
        {
            var sequence = PyIteration.ToSequenceAsync(source, Span, context);
            Assert.Equal(0, probe.Calls);
            for (var i = 1; i <= 2; i++)
            {
                await using var iterator = sequence.GetAsyncEnumerator();
                Assert.Equal(i - 1, probe.Calls);
                Assert.True(await iterator.MoveNextAsync());
                Assert.Equal(i, probe.Calls);
                Assert.Equal(BigInteger.One, iterator.Current);
                Assert.False(await iterator.MoveNextAsync());
            }
            Assert.Equal(2, probe.AsyncCalls);
        }
        else
        {
            var sequence = PyIteration.ToSequence(source, Span, context);
            Assert.Equal(1, probe.Calls);
            using var iterator = sequence.GetEnumerator();
            Assert.Equal(1, probe.Calls);
            Assert.True(iterator.MoveNext());
            Assert.Equal(BigInteger.One, iterator.Current);
            Assert.False(iterator.MoveNext());
            Assert.Equal(1, probe.Calls);
        }
    }

    [Fact]
    public async Task AsyncDictionaryVersionIsCapturedWhenTheSequenceIsCreated()
    {
        var context = Context();
        var dictionary = new PyDict();
        dictionary.SetItem("first", BigInteger.One);
        var sequence = PyIteration.ToSequenceAsync(dictionary, Span, context);
        dictionary.SetItem("second", new BigInteger(2));
        await using var iterator = sequence.GetAsyncEnumerator();
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await iterator.MoveNextAsync());
        Assert.Equal("RuntimeError", failure.ExceptionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnumeratorAcquiresOnceAndCleanupIsIdempotent(bool asynchronous)
    {
        var context = Context();
        var payload = PyString.FromString(new string('x', 16000));
        var source = new OwnedSource(payload);
        object enumerator;
        if (asynchronous)
        {
            var iterator = PyIteration.ToSequenceAsync(source, Span, context).GetAsyncEnumerator();
            Assert.Equal(0, source.Acquired);
            Assert.True(await iterator.MoveNextAsync());
            enumerator = iterator;
        }
        else
        {
            var iterator = PyIteration.ToSequence(source, Span, context).GetEnumerator();
            Assert.Equal(0, source.Acquired);
            Assert.True(iterator.MoveNext());
            enumerator = iterator;
        }
        Assert.Equal(1, source.Acquired);
        Assert.Equal(1, source.CurrentReads);
        if (asynchronous) await ((IAsyncDisposable)enumerator).DisposeAsync();
        else ((IDisposable)enumerator).Dispose();
        Assert.Equal(1, source.Disposed);
        if (asynchronous) Assert.False(await ((IAsyncEnumerator<object>)enumerator).MoveNextAsync());
        else Assert.False(((IEnumerator<object>)enumerator).MoveNext());
        Assert.Equal(1, source.Disposed);
    }



    [Fact]
    public void SynchronousAcquisitionStillPrecedesTheFirstCancellationCheckpoint()
    {
        using var cancellation = new CancellationTokenSource();
        var context = Context(cancellation.Token);
        var source = new OwnedSource(PyNone.Instance);
        using var iterator = PyIteration.ToSequence(source, Span, context).GetEnumerator();
        cancellation.Cancel();
        Assert.Throws<LythonRuntimeException>(() => iterator.MoveNext());
        Assert.Equal(1, source.Acquired);
        Assert.Equal(0, source.CurrentReads);
        Assert.Equal(1, source.Disposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CurrentAndCleanupFailuresDisposeOnce(bool asynchronous, bool cleanupFailure)
    {
        var context = Context();
        var source = new OwnedSource(PyString.FromString(new string('x', 16000)))
        {
            FailCurrent = !cleanupFailure,
            FailDispose = cleanupFailure,
        };
        object enumerator;
        if (asynchronous)
        {
            var iterator = PyIteration.ToSequenceAsync(source, Span, context).GetAsyncEnumerator();
            enumerator = iterator;
            if (cleanupFailure) Assert.True(await iterator.MoveNextAsync());
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => await iterator.MoveNextAsync());
            Assert.Equal(cleanupFailure ? "cleanup failed" : "current failed", failure.Message);
            await iterator.DisposeAsync();
        }
        else
        {
            var iterator = PyIteration.ToSequence(source, Span, context).GetEnumerator();
            enumerator = iterator;
            if (cleanupFailure) Assert.True(iterator.MoveNext());
            var failure = Assert.Throws<InvalidOperationException>(() => iterator.MoveNext());
            Assert.Equal(cleanupFailure ? "cleanup failed" : "current failed", failure.Message);
            iterator.Dispose();
        }
        Assert.Equal(1, source.Disposed);
    }

    private static LythonRuntime.ExecutionContext Context(CancellationToken cancellation = default)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { CancellationToken = cancellation },regexProvider:Utf8RegexProvider.Instance);
        var moduleName = context.Variables["__name__"];
        context.Variables.Clear();
        context.Variables["__name__"] = moduleName;
        return context;
    }

    private sealed class AcquisitionProbe : LythonRuntime.ICallable
    {
        internal int Calls;
        internal int AsyncCalls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            AsyncCalls++;
            return Invoke(arguments, span, context);
        }
    }

    private sealed class OwnedSource(object payload) : IEnumerable<object>, IPyAsyncIterableValue
    {
        internal int Acquired;
        internal int Disposed;
        internal int CurrentReads;
        internal bool FailCurrent;
        internal bool FailDispose;
        public IEnumerator<object> GetEnumerator() { Acquired++; return new Enumerator(this, payload); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IEnumerable<object> Iterate() => this;
        public IAsyncEnumerable<object> IterateAsync() => new AsyncEnumerable(this, payload);


        private sealed class Enumerator(OwnedSource owner, object payload) : IEnumerator<object>
        {
            private bool _moved;
            public object Current { get { owner.CurrentReads++; if (owner.FailCurrent) throw new InvalidOperationException("current failed"); return payload; } }
            object IEnumerator.Current => Current;
            public bool MoveNext() { if (_moved) return false; _moved = true; return true; }
            public void Reset() => throw new NotSupportedException();
            public void Dispose() { owner.Disposed++; if (owner.FailDispose) throw new InvalidOperationException("cleanup failed"); }

        }

        private sealed class AsyncEnumerable(OwnedSource owner, object payload) : IAsyncEnumerable<object>
        {
            public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                owner.Acquired++;
                return new AsyncEnumerator(owner, payload);
            }
        }

        private sealed class AsyncEnumerator(OwnedSource owner, object payload) : IAsyncEnumerator<object>
        {
            private bool _moved;
            public object Current { get { owner.CurrentReads++; if (owner.FailCurrent) throw new InvalidOperationException("current failed"); return payload; } }
            public async ValueTask<bool> MoveNextAsync()
            {
                await Task.Yield();
                if (_moved) return false;
                _moved = true;
                return true;
            }
            public ValueTask DisposeAsync() { owner.Disposed++; if (owner.FailDispose) throw new InvalidOperationException("cleanup failed"); return ValueTask.CompletedTask; }

        }
    }

    private sealed class OpaqueSource : IEnumerable<object>
    {
        public IEnumerator<object> GetEnumerator() { yield return BigInteger.One; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }
}
