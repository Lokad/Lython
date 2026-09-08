using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyIteration
{
    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
        => GetSyncEnumerable(value, span);

    /// <summary>Resolves the Python iteration protocol for arbitrary values, including user-defined <c>__iter__</c>.</summary>
    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value is PyInstance instance
            ? new PyUserIterator(instance, context, span).Iterate()
            : GetSyncEnumerable(value, span);

    public static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span)
        => EnumerateCursorAsync(Cursor.Create(value, span));

    /// <summary>Resolves the Python iteration protocol for asynchronous execution, driving synchronous user protocols when needed.</summary>
    public static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => value is PyInstance instance
            ? EnumerateUserIteratorAsync(instance, context, span)
            : EnumerateCursorAsync(Cursor.Create(value, span));

    /// <summary>Materializes an arbitrary iterable in asynchronous execution, resolving user-defined <c>__iter__</c>.</summary>
    public static async ValueTask<List<object>> MaterializeAsync(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // The caller copies the result into a governed container that takes
        // ownership; the temporary reservation below covers growth here and the
        // simultaneous final copy, and is released before ownership transfers.
        // No allocation happens between release and the caller charging
        // ownership, so retained bytes cannot pass through an uncharged window.
        using var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
        var result = new List<object>();
        var chargedCapacity = 0;
        await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
        {
            result.Add(item);
            if (result.Capacity > chargedCapacity)
            {
                reservation.Grow(16L * (result.Capacity - chargedCapacity), span);
                chargedCapacity = result.Capacity;
            }

            context.ObserveCollectionCount(result.Count, span);
            if ((result.Count & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }
        }

        reservation.Grow(16L * result.Count, span);
        return result;
    }

    private static async IAsyncEnumerable<object> EnumerateUserIteratorAsync(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var item in new PyUserIterator(instance, context, span).Iterate())
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

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
            PyNone => throw RuntimeErrors.Type("Object is not iterable.", span),
            PyDict dict => dict.Keys,
            IPyIteratorValue iterator => EnumerateIterator(iterator),
            IPyIterableValue iterable => iterable.Iterate(),
            IEnumerable<object> typed => typed,
            System.Collections.IEnumerable untyped => EnumerateUntyped(untyped),
            _ => throw RuntimeErrors.Type("Object is not iterable.", span)
        };
    }

    private static async IAsyncEnumerable<object> EnumerateCursorAsync(Cursor cursor)
    {
        try
        {
            while (true)
            {
                var (hasValue, value) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!hasValue)
                {
                    yield break;
                }

                yield return value;
            }
        }
        finally
        {
            await cursor.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal sealed class Cursor : IDisposable, IAsyncDisposable
    {
        private readonly object _value;
        private readonly LythonSourceSpan _span;
        private readonly PyUserIterator? _userIterator;
        private IEnumerator<object>? _syncEnumerator;
        private IAsyncEnumerator<object>? _asyncEnumerator;

        private Cursor(object value, LythonSourceSpan span)
            : this(value, span, null)
        {
        }

        private Cursor(object value, LythonSourceSpan span, PyUserIterator? userIterator)
        {
            _value = value;
            _span = span;
            _userIterator = userIterator;
        }

        public static Cursor Create(object value, LythonSourceSpan span)
        {
            _ = GetSyncEnumerable(value, span);
            return new Cursor(value, span);
        }

        /// <summary>Creates a cursor that resolves user-defined <c>__iter__</c> through the active execution.</summary>
        public static Cursor Create(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (value is PyInstance instance)
            {
                return new Cursor(value, span, new PyUserIterator(instance, context, span));
            }

            _ = GetSyncEnumerable(value, span);
            return new Cursor(value, span);
        }

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
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
            if (_userIterator is not null)
            {
                return _userIterator.TryMoveNext(out var userValue)
                    ? PyIterationResult.Yield(userValue)
                    : PyIterationResult.End;
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

            return TryMoveNext(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End;
        }

        public void Dispose()
        {
            _syncEnumerator?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            if (_asyncEnumerator is not null)
            {
                await _asyncEnumerator.DisposeAsync().ConfigureAwait(false);
            }
        }

    }
}
