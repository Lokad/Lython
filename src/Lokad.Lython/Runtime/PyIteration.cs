using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyIteration
{
    internal readonly record struct GeneratorOuter(object Iterator, PyUserIterator? User = null);

    internal static GeneratorOuter AcquireGeneratorOuter(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (value is PyInstance or IPyProtocolIterator)
            return AcquiredUserOuter(PyUserIterator.Create(value, context, span));
        if (value is IPyIteratorValue) return new(value);
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        PyEnumerableIterator iterator;
        try { iterator = new PyEnumerableIterator(value, span, context); }
        catch
        {
            context.MemoryGovernor.Release(PyIteratorBase.IteratorValueBytes);
            throw;
        }
        return new(iterator);
    }

    internal static async ValueTask<GeneratorOuter> AcquireGeneratorOuterAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value is PyInstance or IPyProtocolIterator
            ? AcquiredUserOuter(await PyUserIterator.CreateAsync(value, context, span).ConfigureAwait(false))
            : AcquireGeneratorOuter(value, span, context);

    private static GeneratorOuter AcquiredUserOuter(PyUserIterator user)
        => new(user.Iterator, user);

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
        var initialCapacity = InitialDrainCapacity(items, maximumItems);
        var reservation = context.MemoryGovernor.ReserveTemporary(checked(32L + 16L * initialCapacity), span);
        try
        {
            context.CheckExecution(span);
            var result = new List<object>(initialCapacity);
            if (items is Cursor cursor && cursor.TryCopyAdmittedList(result, maximumItems, span, context))
                return new DrainLease(result, reservation);
            var generator = (items as Cursor)?.GetBulkGenerator(context);
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

                var exhausted = generator is not null && generator.AppendBatch(result, maximumItems);
                context.CheckExecution(span);
                if (exhausted || result.Count == maximumItems) break;
            }
            return new DrainLease(result, reservation);
        }
        catch
        {
            reservation.Dispose();
            throw;
        }
    }

    internal static async ValueTask<DrainLease> DrainLeasedAsync(IAsyncEnumerable<object> items, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
    {
        var initialCapacity = InitialDrainCapacity(items, maximumItems);
        var reservation = context.MemoryGovernor.ReserveTemporary(checked(32L + 16L * initialCapacity), span);
        try
        {
            context.CheckExecution(span);
            var result = new List<object>(initialCapacity);
            if (items is Cursor cursor && cursor.TryCopyAdmittedList(result, maximumItems, span, context))
                return new DrainLease(result, reservation);
            var generator = (items as Cursor)?.GetBulkGenerator(context);
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

                var exhausted = generator is not null && await generator.AppendBatchAsync(result, maximumItems).ConfigureAwait(false);
                context.CheckExecution(span);
                if (exhausted || result.Count == maximumItems) break;
            }
            return new DrainLease(result, reservation);
        }
        catch
        {
            reservation.Dispose();
            throw;
        }
    }

    // Capacity is a hint, never a snapshot or iteration bound. Admission may
    // reenter and mutate a live list; the ordinary loop still observes those
    // values and funds replacement overlap if the source outgrows this backing.
    private static int InitialDrainCapacity(object items, int? maximumItems)
    {
        var count = items is Cursor cursor ? cursor.KnownCount.GetValueOrDefault()
            : items is IReadOnlyCollection<object> known ? known.Count : 0;
        return maximumItems is { } maximum ? Math.Min(count, Math.Max(0, maximum)) : count;
    }

    public static DrainLease MaterializeLeased(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
        => DrainLeased(Cursor.Create(value, span, context), span, context, maximumItems);

    public static async ValueTask<DrainLease> MaterializeLeasedAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? maximumItems = null)
        => await DrainLeasedAsync(Cursor.ForAsyncEnumeration(value, span, context), span, context, maximumItems).ConfigureAwait(false);

    public static IEnumerable<object> EnumerateIterator(IPyIteratorValue iterator)
    {
        while ((iterator.TryMoveNext() is { HasValue: true, Value: var value }))
        {
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

    internal sealed class Cursor : ExecutableTemporaryValue, IEnumerator<object>, IEnumerable<object>, IAsyncEnumerator<object>, IAsyncEnumerable<object>
    {
        private object? _value;
        private readonly LythonSourceSpan _span;
        private IPyIteratorValue? _iterator;
        private LythonRuntime.ExecutionContext? _context;
        private int _movesUntilCheckpoint;
        private int _sequencePosition;
        private IEnumerable<object>? _syncSource;
        private IEnumerator<object>? _syncEnumerator;
        private object _current = PyNone.Instance;
        private bool _disposed;
        internal static Cursor FromGeneratorOuter(GeneratorOuter outer, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
            => new(outer.Iterator, span, outer.User, context);

        // CLR collection builders share this single-pass cursor; native consumers
        // use TryMoveNext directly. Source identity preserves their capacity hints
        // and self-extension snapshots without a second enumerable wrapper.
        internal PyList? UnstartedListSource => !_disposed && _iterator is null
            && _sequencePosition == 0 && _syncEnumerator is null
                ? _value as PyList : null;

        internal bool TryCopyAdmittedList(List<object> result, int? maximumItems, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
        {
            if (maximumItems is <= 0 || UnstartedListSource is not { } list
                || _context is not null && !ReferenceEquals(_context, context)) return false;
            // Observe the live list after admission. If reentry grew it beyond
            // the funded capacity, the original cursor still owns advancement.
            var count = maximumItems is { } maximum ? Math.Min(list.Count, maximum) : list.Count;
            if (count > result.Capacity) return false;
            using (this)
                list.CopyAdmittedPrefixTo(result, count, context, span);
            return true;
        }

        internal int? KnownCount => _iterator is null && (_syncSource ?? _value) is IReadOnlyCollection<object> known
            ? known.Count : null;
        internal LythonRuntime.PyGenerator? GetBulkGenerator(LythonRuntime.ExecutionContext? context)
            => context is not null && !_disposed && _syncEnumerator is null
                && _iterator is LythonRuntime.PyGenerator generator && generator.CanBufferFor(context)
                    ? generator : null;
        public Cursor GetEnumerator() => this;
        IEnumerator<object> IEnumerable<object>.GetEnumerator() => this;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this;
        IAsyncEnumerator<object> IAsyncEnumerable<object>.GetAsyncEnumerator(CancellationToken cancellationToken) => this;
        ValueTask<bool> IAsyncEnumerator<object>.MoveNextAsync() => MoveNextForClrAsync();

        // Only CLR async transforms need deferred acquisition. The active
        // execution token is carried by the cursor, not captured by a wrapper.
        internal static IAsyncEnumerable<object> ForAsyncEnumeration(object value, LythonSourceSpan span)
            => Create(value, span);
        internal static IAsyncEnumerable<object> ForAsyncEnumeration(object value, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
            => value is PyInstance or IPyProtocolIterator ? new Cursor(value, span, context: context)
                : Create(value, span, context);

        private async ValueTask<bool> MoveNextForClrAsync()
        {
            if (_disposed) return false;
            try
            {
                var next = await TryMoveNextAsync().ConfigureAwait(false);
                if (next.HasValue) { _current = next.Value; return true; }
                Dispose();
                return false;
            }
            catch { Dispose(); throw; }
        }

        public object Current => _current;
        void System.Collections.IEnumerator.Reset() => throw new NotSupportedException();
        public bool MoveNext()
        {
            if (_disposed) return false;
            try
            {
                // CLR enumerator acquisition remains lazy and precedes its
                // first checkpoint. Python iterator values themselves stay open.
                if (_syncEnumerator is null && _iterator is null && _value is not (PyList or PyTuple))
                    _syncEnumerator = (_syncSource ??= GetSyncEnumerable(_value!, _span)).GetEnumerator();
                if ((TryMoveNext() is { HasValue: true, Value: var value })) { _current = value; return true; }
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

            // ChainMap cursors enumerate owned snapshots
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

        public PyIterationResult TryMoveNext()
        {
            CheckExecution();
            return TryMoveNextCore();
        }

        internal IPyIteratorValue? DirectUnpackSource => !_disposed ? _iterator : null;

        internal PyZipIterator? ShortZipForPull()
        {
            if (DirectUnpackSource is not PyZipIterator { Arity: <= LythonRuntime.InlineIterationOperandCount } zip)
                return null;
            CheckExecution();
            CheckStackForPull();
            return zip;
        }

        private PyIterationResult TryMoveNextCore()
        {
            if (_disposed || _value is null) return PyIterationResult.End;
            if (IteratorForPull() is { } iterator) return iterator.TryMoveNext();

            if (_syncEnumerator is null && _value is (PyList or PyTuple))
            {
                var next = AdvanceBuiltinSequence(_value, ref _sequencePosition);
                // A retained list cursor cannot revive after append at EOF.
                if (!next.HasValue) _disposed = true;
                return next;
            }

            _syncEnumerator ??= (_syncSource ??= GetSyncEnumerable(_value, _span)).GetEnumerator();
            if (_syncEnumerator is PyRange.Enumerator integers) return integers.TryMoveNext();
            return _syncEnumerator.MoveNext() ? PyIterationResult.Yield(_syncEnumerator.Current) : PyIterationResult.End;
        }

        public ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            CheckExecution();
            if (_disposed) return ValueTask.FromResult(PyIterationResult.End);
            if (_iterator is null && _value is PyInstance or IPyProtocolIterator)
                return AcquireDeferredUserAsync();
            if (IteratorForPull() is { } iterator)
                return iterator is IPyAsyncIteratorValue asynchronous
                    ? asynchronous.TryMoveNextAsync() : new(iterator.TryMoveNext());

            return ValueTask.FromResult(TryMoveNextCore());
        }

        private async ValueTask<PyIterationResult> AcquireDeferredUserAsync()
        {
            var iterator = await PyUserIterator.CreateAsync(_value!, _context!, _span).ConfigureAwait(false);
            _iterator = iterator;
            CheckStackForPull();
            return await iterator.TryMoveNextAsync().ConfigureAwait(false);
        }

        private IPyIteratorValue? IteratorForPull()
        {
            var iterator = _iterator;
            // Native iterators can pull other cursors before reaching any
            // guest call or opcode, including during an async prefix.
            if (iterator is not null) CheckStackForPull();
            return iterator;
        }

        private void CheckStackForPull()
        {
            if (_context is { } context) context.State.CheckInterpreterStack(_span);
            else ExecutionState.EnsureInterpreterStack(_span);
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
            var enumerator = _syncEnumerator;
            _syncEnumerator = null;
            _current = PyNone.Instance;
            try { enumerator?.Dispose(); }
            finally
            {
                _value = null;
                _syncSource = null;
                _iterator = null;
                _context = null;
            }
        }

        // CLR async consumers use the same private cursor. Python iterators
        // are borrowed, while any cursor-owned CLR enumerator is synchronous.
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
