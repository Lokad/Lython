using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyIteration
{
    internal readonly record struct GeneratorOuter(object Iterator, IEnumerable<object> Sequence, IAsyncEnumerable<object>? AsyncSequence = null);

    internal static GeneratorOuter AcquireGeneratorOuter(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (value is PyInstance or IPyProtocolIterator)
            return AcquiredUserOuter(PyUserIterator.Create(value, context, span), span, context);
        if (value is IPyIteratorValue) return new(value, ToSequence(value, span, context));
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        PyEnumerableIterator iterator;
        try { iterator = new PyEnumerableIterator(value, span, context); }
        catch
        {
            context.MemoryGovernor.Release(PyIteratorBase.IteratorValueBytes);
            throw;
        }
        return new(iterator, iterator.Iterate(), EnumerateAsyncIterator(iterator));
    }

    internal static async ValueTask<GeneratorOuter> AcquireGeneratorOuterAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value is PyInstance or IPyProtocolIterator
            ? AcquiredUserOuter(await PyUserIterator.CreateAsync(value, context, span).ConfigureAwait(false), span, context)
            : AcquireGeneratorOuter(value, span, context);

    private static GeneratorOuter AcquiredUserOuter(PyUserIterator user, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => new(user.Iterator, WithCheckpoints(user.Iterate(), span, context), new AsyncSequence(user, span, context));

    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
        => GetSyncEnumerable(value, span);

    /// <summary>Resolves the Python iteration protocol for arbitrary values, including user-defined <c>__iter__</c>.</summary>
    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => WithCheckpoints(value switch
        {
            PyInstance or IPyProtocolIterator => PyUserIterator.Create(value, context, span).Iterate(),
            // ChainMap iteration snapshots merged keys eagerly with lifetime
            // ownership; values and items keep live per-pull lookups over an
            // owned key order (see PyChainMap for the CPython behavior notes).
            PyChainMap chainMap => chainMap.BuildOwnedKeySnapshot(context, span),
            ChainMapKeysView keysView => keysView.Owner.BuildOwnedKeySnapshot(context, span),
            ChainMapValuesView valuesView => PyChainMap.EnumerateOwnedValues(
                valuesView.Owner,
                valuesView.Owner.BuildOwnedKeySnapshot(context, span),
                span),
            ChainMapItemsView itemsView => PyChainMap.EnumerateOwnedItems(
                itemsView.Owner,
                itemsView.Owner.BuildOwnedKeySnapshot(context, span),
                span),
            _ => GetSyncEnumerable(value, span),
        }, span, context);

    // Bulk materializers inspect known-size/list sources to reserve their
    // backing storage and snapshot self-extension. Keep that source identity
    // available instead of hiding it in a compiler-generated iterator.
    private static IEnumerable<object> WithCheckpoints(
        IEnumerable<object> source, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => source is IReadOnlyCollection<object> collection
            ? new CheckedCollection(collection, span, context)
            : new CheckedSequence(source, span, context);

    internal class CheckedSequence(
        IEnumerable<object> source, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        : IEnumerable<object>
    {
        internal IEnumerable<object> Source => source;
        protected LythonSourceSpan Span { get; } = span;
        protected LythonRuntime.ExecutionContext Context { get; } = context;
        public IEnumerator<object> GetEnumerator() => Cursor.FromSequence(source, Span, Context);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CheckedCollection(
        IReadOnlyCollection<object> source, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        : CheckedSequence(source, span, context), IReadOnlyCollection<object>
    {
        public int Count => source.Count;
    }

    public static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span)
        => new AsyncSequence(Cursor.Create(value, span), span, null);

    /// <summary>Resolves the Python iteration protocol for asynchronous execution, driving synchronous user protocols when needed.</summary>
    public static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value switch
        {
            PyInstance or IPyProtocolIterator => new AsyncSequence(value, span, context),
            // Context-aware cursors retain the same owned ChainMap shapes as
            // the sync router and checkpoint primitive pulls in both modes.
            _ => new AsyncSequence(Cursor.Create(value, span, context), span, null),
        };

    // The lease keeps private backing funded through callbacks, suspension and
    // conversion. Dispose only after every user relinquishes the working list.
    internal sealed class DrainLease : IDisposable
    {
        public List<object> Items { get; }
        private readonly MemoryGovernor.TemporaryMemoryReservation _reservation;

        internal DrainLease(List<object> items, MemoryGovernor.TemporaryMemoryReservation reservation)
        {
            Items = items;
            _reservation = reservation;
        }

        public void Dispose()
        {
            _reservation.Dispose();
        }
    }

    internal static DrainLease DrainLeased(IEnumerable<object> items, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
    {
        var reservation = context.MemoryGovernor.ReserveTemporary(32, span);
        var result = new List<object>();
        try
        {
            foreach (var item in items)
            {
                if (result.Count == result.Capacity)
                {
                    var oldCapacity = result.Capacity;
                    var capacity = oldCapacity == 0 ? 4 : checked(oldCapacity * 2);
                    reservation.Grow(16L * capacity, span);
                    result.Capacity = capacity;
                    reservation.Shrink(16L * oldCapacity);
                }
                result.Add(item);

                context.CheckExecution(span);
                if (result.Count == maximumItems) break;
            }
        }
        catch
        {
            reservation.Dispose();
            throw;
        }

        return new DrainLease(result, reservation);
    }

    internal static async ValueTask<DrainLease> DrainLeasedAsync(IAsyncEnumerable<object> items, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
    {
        var reservation = context.MemoryGovernor.ReserveTemporary(32, span);
        var result = new List<object>();
        try
        {
            await foreach (var item in items.ConfigureAwait(false))
            {
                if (result.Count == result.Capacity)
                {
                    var oldCapacity = result.Capacity;
                    var capacity = oldCapacity == 0 ? 4 : checked(oldCapacity * 2);
                    reservation.Grow(16L * capacity, span);
                    result.Capacity = capacity;
                    reservation.Shrink(16L * oldCapacity);
                }
                result.Add(item);

                context.CheckExecution(span);
                if (result.Count == maximumItems) break;
            }
        }
        catch
        {
            reservation.Dispose();
            throw;
        }

        return new DrainLease(result, reservation);
    }

    public static DrainLease MaterializeLeased(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
        => DrainLeased(ToSequence(value, span, context), span, context, maximumItems);

    public static async ValueTask<DrainLease> MaterializeLeasedAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
        => await DrainLeasedAsync(ToSequenceAsync(value, span, context), span, context, maximumItems).ConfigureAwait(false);

    internal static async ValueTask<IAsyncEnumerable<object>> AcquireComprehensionSequenceAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value is PyInstance or IPyProtocolIterator
            ? new AsyncSequence(await PyUserIterator.CreateAsync(value, context, span).ConfigureAwait(false), span, context)
            : ToSequenceAsync(value, span, context);

    public static IEnumerable<object> EnumerateIterator(IPyIteratorValue iterator)
    {
        while (iterator.TryMoveNext(out var value))
        {
            yield return value;
        }
    }

    public static async IAsyncEnumerable<object> EnumerateAsyncIterator(IPyAsyncIteratorValue iterator)
    {
        while (true)
        {
            var (hasValue, value) = await iterator.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                yield break;
            }

            yield return value;
        }
    }

    private static IEnumerable<object> EnumerateUntyped(System.Collections.IEnumerable sequence)
    {
        foreach (var value in sequence)
        {
            yield return LythonRuntime.RuntimeValue(value);
        }
    }

    private static IEnumerable<object> GetSyncEnumerable(object value, LythonSourceSpan span)
    {
        return value switch
        {
            PyNone => throw RuntimeErrors.NotIterable(value, span),
            PyDict dict => dict.Keys,
            IPyIteratorValue iterator => EnumerateIterator(iterator),
            IPyIterableValue iterable => iterable.Iterate(),
            IEnumerable<object> typed => typed,
            System.Collections.IEnumerable untyped => EnumerateUntyped(untyped),
            _ => throw RuntimeErrors.NotIterable(value, span)
        };
    }

    // Exact sequences use the same live index semantics in private cursors and
    // executable operand storage. No CLR enumerator is needed around that state.
    internal static PyIterationResult AdvanceBuiltinSequence(object source, ref int position)
    {
        switch (source)
        {
            case PyList list:
            {
                if (position >= list.Count) return PyIterationResult.End;
                var value = list[position];
                position++;
                return PyIterationResult.Yield(value);
            }
            case PyTuple tuple:
            {
                if (position >= tuple.Count) return PyIterationResult.End;
                var value = tuple[position];
                position++;
                return PyIterationResult.Yield(value);
            }
            default:
                throw new InvalidOperationException("Expected an exact builtin sequence.");
        }
    }

    internal sealed class Cursor : ExecutableTemporaryValue, IAsyncDisposable, IEnumerator<object>
    {
        private object? _value;
        private readonly LythonSourceSpan _span;
        private IPyIteratorValue? _iterator;
        private LythonRuntime.ExecutionContext? _context;
        private int _movesUntilCheckpoint;
        private int _sequencePosition;
        private IEnumerable<object>? _syncSource;
        private IEnumerator<object>? _syncEnumerator;
        private IAsyncEnumerator<object>? _asyncEnumerator;
        private object _current = PyNone.Instance;
        private bool _disposed;
        // Intrusive link used only after this temporary leaves an async VM
        // stack. Awaiting cleanup needs no extra list or per-removal allocation.
        internal Cursor? NextPendingDisposal;

        internal static Cursor FromAcquired(PyUserIterator iterator, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new(iterator.Iterator, span, iterator, context);

        internal static Cursor FromSequence(IEnumerable<object> source, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new(source, span, context: context) { _syncSource = source };

        internal static Cursor FromGeneratorOuter(GeneratorOuter outer, object source, bool asynchronous,
            LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var cursor = new Cursor(outer.Iterator, span, context: context);
            try
            {
                cursor._syncEnumerator = outer.Sequence.GetEnumerator();
                if (asynchronous && source is IPyAsyncIterableValue iterable)
                    cursor._asyncEnumerator = iterable.IterateAsync().GetAsyncEnumerator();
                else if (outer.AsyncSequence is not null)
                    cursor._asyncEnumerator = outer.AsyncSequence.GetAsyncEnumerator();
                return cursor;
            }
            catch { cursor.Dispose(); throw; }
        }

        object IEnumerator<object>.Current => _current;
        object System.Collections.IEnumerator.Current => _current;
        void System.Collections.IEnumerator.Reset() => throw new NotSupportedException();
        bool System.Collections.IEnumerator.MoveNext()
        {
            if (_disposed) return false;
            try
            {
                // Checked sequence acquisition remains lazy and precedes its
                // first checkpoint. Python iterator values themselves stay open.
                if (_syncEnumerator is null && _iterator is null && _value is not (PyList or PyTuple))
                    _syncEnumerator = (_syncSource ??= GetSyncEnumerable(_value!, _span)).GetEnumerator();
                if (TryMoveNext(out var value)) { _current = value; return true; }
                Dispose();
                return false;
            }
            catch { Dispose(); throw; }
        }

        private Cursor(object value, LythonSourceSpan span, PyUserIterator? userIterator = null,
            LythonRuntime.ExecutionContext? context = null)
        {
            _value = value;
            _span = span;
            _iterator = userIterator is null ? value as IPyIteratorValue : userIterator;
            _context = context;
        }

        public static Cursor Create(object value, LythonSourceSpan span)
            => CreateNative(value, span, null);

        private static Cursor CreateNative(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext? context)
        {
            // Raw Python iterators need no CLR adapter. Retain one native
            // enumerable instead of constructing and discarding it to validate.
            if (value is IPyIteratorValue) return new Cursor(value, span, context: context);
            var sequence = GetSyncEnumerable(value, span);
            // Keep the original receiver when its async iterable slot can win.
            // Native enumerator acquisition stays lazy except dictionary
            // version snapshots, which Python captures at iter().
            var cursor = new Cursor(value, span, context: context) { _syncSource = sequence };
            if (value is PyDict or LythonRuntime.DictKeysView or LythonRuntime.DictValuesView or LythonRuntime.DictItemsView)
                cursor._syncEnumerator = sequence.GetEnumerator();
            return cursor;
        }

        /// <summary>Creates a cursor that resolves user-defined <c>__iter__</c> through the active execution.</summary>
        public static Cursor Create(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (value is PyInstance or IPyProtocolIterator)
            {
                return new Cursor(value, span, PyUserIterator.Create(value, context, span), context);
            }

            // ChainMap cursors enumerate owned snapshots (see ToSequence above)
            // so retained enumerate/zip iterators stay charged instead of
            // holding unowned merge scratch.
            if (value is PyChainMap chainMap)
            {
                return CreateNative(chainMap.BuildOwnedKeySnapshot(context, span), span, context);
            }

            if (value is ChainMapKeysView keysView)
            {
                return CreateNative(keysView.Owner.BuildOwnedKeySnapshot(context, span), span, context);
            }

            if (value is ChainMapValuesView valuesView)
            {
                return CreateNative(
                    PyChainMap.EnumerateOwnedValues(valuesView.Owner, valuesView.Owner.BuildOwnedKeySnapshot(context, span), span),
                    span, context);
            }

            if (value is ChainMapItemsView itemsView)
            {
                return CreateNative(
                    PyChainMap.EnumerateOwnedItems(itemsView.Owner, itemsView.Owner.BuildOwnedKeySnapshot(context, span), span),
                    span, context);
            }

            return CreateNative(value, span, context);
        }

        public static async ValueTask<Cursor> CreateAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => value is PyInstance or IPyProtocolIterator
                ? new Cursor(value, span, await PyUserIterator.CreateAsync(value, context, span).ConfigureAwait(false), context)
                : Create(value, span, context);

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            CheckExecution();
            return TryMoveNextCore(out value);
        }

        private bool TryMoveNextCore([MaybeNullWhen(false)] out object value)
        {
            if (_disposed || _value is null)
            {
                value = PyNone.Instance;
                return false;
            }
            if (_iterator is { } iterator)
            {
                return iterator.TryMoveNext(out value);
            }

            if (_syncEnumerator is null && _value is (PyList or PyTuple))
            {
                var next = AdvanceBuiltinSequence(_value, ref _sequencePosition);
                value = next.HasValue ? next.Value : PyNone.Instance;
                // A retained list cursor cannot revive after append at EOF.
                if (!next.HasValue) _disposed = true;
                return next.HasValue;
            }

            _syncEnumerator ??= (_syncSource ??= GetSyncEnumerable(_value, _span)).GetEnumerator();
            if (_syncEnumerator.MoveNext())
            {
                value = _syncEnumerator.Current;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            CheckExecution();
            if (_disposed) return ValueTask.FromResult(PyIterationResult.End);
            if (_asyncEnumerator is not null)
                return PullAsyncEnumeratorAsync();
            if (_iterator is IPyAsyncIteratorValue asyncIterator)
            {
                return asyncIterator.TryMoveNextAsync();
            }

            // Synchronous iterator slots precede asynchronous iterable fallback.
            if (_iterator is null && _value is IPyAsyncIterableValue asyncIterable)
            {
                _asyncEnumerator = asyncIterable.IterateAsync().GetAsyncEnumerator();
                return PullAsyncEnumeratorAsync();
            }

            return ValueTask.FromResult(TryMoveNextCore(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End);
        }

        private async ValueTask<PyIterationResult> PullAsyncEnumeratorAsync()
        {
            return await _asyncEnumerator!.MoveNextAsync().ConfigureAwait(false)
                ? PyIterationResult.Yield(_asyncEnumerator.Current) : PyIterationResult.End;
        }

        private void CheckExecution()
        {
            if (_context is not null && _movesUntilCheckpoint-- == 0)
            {
                _movesUntilCheckpoint = 63;
                _context.CheckExecution(_span);
            }
        }

        public override void Dispose()
        {
            _disposed = true;
            try { DisposeSynchronousEnumerator(); }
            finally
            {
                // Mixed cursors retain their original subject until actual
                // asynchronous cleanup, even when synchronous cleanup fails.
                if (_asyncEnumerator is null) ClearSources();
            }
        }

        private void DisposeSynchronousEnumerator()
        {
            var enumerator = _syncEnumerator;
            _syncEnumerator = null;
            _current = PyNone.Instance;
            enumerator?.Dispose();
        }

        private void ClearSources()
        {
            _value = null;
            _syncSource = null;
            _iterator = null;
            _context = null;
        }

        public async ValueTask DisposeAsync()
        {
            _disposed = true;
            var enumerator = _asyncEnumerator;
            _asyncEnumerator = null;
            try { DisposeSynchronousEnumerator(); }
            finally
            {
                try
                {
                    if (enumerator is not null)
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                }
                finally { ClearSources(); }
            }
        }

    }
}
