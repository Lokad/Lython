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
        => value is PyRange range ? new CheckedRange(range, span, context) : WithCheckpoints(value switch
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
        public virtual IEnumerator<object> GetEnumerator() => new CheckedEnumerator(source, Span, Context);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CheckedRange(
        PyRange range, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        : CheckedSequence(range.Iterate(), span, context)
    {
        // Keep the original source shape for bulk-materializer inspection.
        public override IEnumerator<object> GetEnumerator() => range.IterateChecked(Span, Context).GetEnumerator();
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

    /// <summary>Materializes an arbitrary iterable in asynchronous execution, resolving user-defined <c>__iter__</c>.</summary>
    public static async ValueTask<List<object>> MaterializeAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => await DrainAsync(ToSequenceAsync(value, span, context), span, context).ConfigureAwait(false);

    /// <summary>
    /// Shared asynchronous drain behind every materializer: growth is charged
    /// before the backing array can allocate (lists double from an initial
    /// four, matching the storage capacity prediction), so an unbounded input
    /// meets the memory budget instead of over-allocating first. Funded
    /// capacity (reserved but not yet observed) is tracked separately from
    /// observed capacity, so each growth increment charges exactly once and a
    /// surprising reallocation only funds its real discrepancy. The temporary
    /// reservation also covers the caller-owned final copy and is released
    /// before ownership transfers; every consumer below converts the returned
    /// scratch into governed storage immediately (R12 audit).
    /// </summary>
    internal static async ValueTask<List<object>> DrainAsync(
        IAsyncEnumerable<object> items,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        using var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
        var result = new List<object>();
        long fundedCapacity = 0;
        await foreach (var item in items.ConfigureAwait(false))
        {
            if (result.Count == result.Capacity)
            {
                var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                if (predicted > fundedCapacity)
                {
                    reservation.Grow(checked(16L * (predicted - fundedCapacity)), span);
                    fundedCapacity = predicted;
                }
            }

            result.Add(item);
            if (result.Capacity > fundedCapacity)
            {
                reservation.Grow(16L * (result.Capacity - fundedCapacity), span);
                fundedCapacity = result.Capacity;
            }

            context.ObserveCollectionCount(result.Count, span);
            if ((result.Count & 63) == 0)
            {
                context.CheckExecution(span);
            }
        }

        reservation.Grow(16L * result.Count, span);
        return result;
    }

    /// <summary>Materializes an arbitrary iterable in synchronous execution, resolving user-defined <c>__iter__</c>.</summary>
    public static List<object> Materialize(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => Drain(ToSequence(value, span, context), span, context);

    /// <summary>
    /// Shared synchronous drain behind every eager materializer: growth is charged
    /// before the backing array can allocate (lists double from an initial
    /// four, matching the storage capacity prediction), so an unbounded input
    /// meets the memory budget instead of over-allocating first. The temporary
    /// reservation also covers the caller-owned final copy and is released
    /// before ownership transfers. Funded-versus-observed capacity follows the
    /// asynchronous twin above.
    /// </summary>
    internal static List<object> Drain(
        IEnumerable<object> items,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        using var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
        var result = new List<object>();
        long fundedCapacity = 0;
        foreach (var item in items)
        {
            if (result.Count == result.Capacity)
            {
                var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                if (predicted > fundedCapacity)
                {
                    reservation.Grow(checked(16L * (predicted - fundedCapacity)), span);
                    fundedCapacity = predicted;
                }
            }

            result.Add(item);
            if (result.Capacity > fundedCapacity)
            {
                reservation.Grow(16L * (result.Capacity - fundedCapacity), span);
                fundedCapacity = result.Capacity;
            }

            context.ObserveCollectionCount(result.Count, span);
            if ((result.Count & 63) == 0)
            {
                context.CheckExecution(span);
            }
        }

        reservation.Grow(16L * result.Count, span);
        return result;
    }

    // N09: explicit reservation lifetime for consumers that keep drained scratch
    // alive across guest callbacks. Unlike Drain (which releases before returning
    // for immediate conversion with overlap proof), the lease keeps growth charged
    // until disposed - dispose after the callback completes and ownership transfers.
    internal sealed class DrainLease : IDisposable
    {
        public List<object> Items { get; }
        private MemoryGovernor.TemporaryMemoryReservation _reservation;

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
        var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
        var result = new List<object>();
        long fundedCapacity = 0;
        try
        {
            foreach (var item in items)
            {
                if (result.Count == result.Capacity)
                {
                    var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                    if (predicted > fundedCapacity)
                    {
                        reservation.Grow(checked(16L * (predicted - fundedCapacity)), span);
                        fundedCapacity = predicted;
                    }
                }

                result.Add(item);
                if (result.Capacity > fundedCapacity)
                {
                    reservation.Grow(16L * (result.Capacity - fundedCapacity), span);
                    fundedCapacity = result.Capacity;
                }

                context.ObserveCollectionCount(result.Count, span);
                if ((result.Count & 63) == 0)
                {
                    context.CheckExecution(span);
                }
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
        var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
        var result = new List<object>();
        long fundedCapacity = 0;
        try
        {
            await foreach (var item in items.ConfigureAwait(false))
            {
                if (result.Count == result.Capacity)
                {
                    var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                    if (predicted > fundedCapacity)
                    {
                        reservation.Grow(checked(16L * (predicted - fundedCapacity)), span);
                        fundedCapacity = predicted;
                    }
                }

                result.Add(item);
                if (result.Capacity > fundedCapacity)
                {
                    reservation.Grow(16L * (result.Capacity - fundedCapacity), span);
                    fundedCapacity = result.Capacity;
                }

                context.ObserveCollectionCount(result.Count, span);
                if ((result.Count & 63) == 0)
                {
                    context.CheckExecution(span);
                }
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

    internal sealed class Cursor : IExecutableTemporaryValue, IAsyncDisposable, IEnumerator<object>
    {
        private readonly object _value;
        private readonly LythonSourceSpan _span;
        private readonly PyUserIterator? _userIterator;
        private readonly LythonRuntime.ExecutionContext? _context;
        private int _movesUntilCheckpoint;
        private IEnumerator<object>? _syncEnumerator;
        private IAsyncEnumerator<object>? _asyncEnumerator;
        private object _current = PyNone.Instance;
        // Intrusive link used only after this temporary leaves an async VM
        // stack. Awaiting cleanup needs no extra list or per-removal allocation.
        internal Cursor? NextPendingDisposal;

        internal static Cursor FromAcquired(PyUserIterator iterator, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new(iterator.Iterator, span, iterator, context);

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
            if (TryMoveNext(out var value)) { _current = value; return true; }
            Dispose();
            return false;
        }

        private Cursor(object value, LythonSourceSpan span, PyUserIterator? userIterator = null,
            LythonRuntime.ExecutionContext? context = null)
        {
            _value = value;
            _span = span;
            _userIterator = userIterator;
            _context = context;
            // Dictionary iterators capture size/version at iter(), before any
            // first pull. The views stay live until their iterator is created.
            if (value is PyDict or LythonRuntime.DictKeysView or LythonRuntime.DictValuesView or LythonRuntime.DictItemsView)
                _syncEnumerator = GetSyncEnumerable(value, span).GetEnumerator();
        }

        public static Cursor Create(object value, LythonSourceSpan span)
        {
            _ = GetSyncEnumerable(value, span);
            return new Cursor(value, span);
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
                return new Cursor(chainMap.BuildOwnedKeySnapshot(context, span), span, context: context);
            }

            if (value is ChainMapKeysView keysView)
            {
                return new Cursor(keysView.Owner.BuildOwnedKeySnapshot(context, span), span, context: context);
            }

            if (value is ChainMapValuesView valuesView)
            {
                return new Cursor(
                    PyChainMap.EnumerateOwnedValues(valuesView.Owner, valuesView.Owner.BuildOwnedKeySnapshot(context, span), span),
                    span, context: context);
            }

            if (value is ChainMapItemsView itemsView)
            {
                return new Cursor(
                    PyChainMap.EnumerateOwnedItems(itemsView.Owner, itemsView.Owner.BuildOwnedKeySnapshot(context, span), span),
                    span, context: context);
            }

            _ = GetSyncEnumerable(value, span);
            return new Cursor(value, span, context: context);
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
            if (_userIterator is not null)
            {
                return _userIterator.TryMoveNext(out value);
            }

            if (_value is IPyIteratorValue iterator)
            {
                return iterator.TryMoveNext(out value);
            }

            _syncEnumerator ??= GetSyncEnumerable(_value, _span).GetEnumerator();
            if (_syncEnumerator.MoveNext())
            {
                value = _syncEnumerator.Current;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            CheckExecution();
            if (_asyncEnumerator is not null)
                return await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false)
                    ? PyIterationResult.Yield(_asyncEnumerator.Current) : PyIterationResult.End;
            if (_userIterator is not null)
            {
                return await _userIterator.TryMoveNextAsync().ConfigureAwait(false);
            }

            if (_value is IPyAsyncIteratorValue asyncIterator)
            {
                return await asyncIterator.TryMoveNextAsync().ConfigureAwait(false);
            }

            if (_value is IPyIteratorValue iterator)
            {
                return iterator.TryMoveNext(out var value)
                    ? PyIterationResult.Yield(value)
                    : PyIterationResult.End;
            }

            if (_value is IPyAsyncIterableValue asyncIterable)
            {
                _asyncEnumerator ??= asyncIterable.IterateAsync().GetAsyncEnumerator();
                if (await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    return PyIterationResult.Yield(_asyncEnumerator.Current);
                }

                return PyIterationResult.End;
            }

            return TryMoveNextCore(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End;
        }

        private void CheckExecution()
        {
            if (_context is not null && _movesUntilCheckpoint-- == 0)
            {
                _movesUntilCheckpoint = 63;
                _context.CheckExecution(_span);
            }
        }

        public void Dispose()
        {
            var enumerator = _syncEnumerator;
            _syncEnumerator = null;
            _current = PyNone.Instance;
            enumerator?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            var enumerator = _asyncEnumerator;
            _asyncEnumerator = null;
            try { Dispose(); }
            finally
            {
                if (enumerator is not null)
                    await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }

    }
}
