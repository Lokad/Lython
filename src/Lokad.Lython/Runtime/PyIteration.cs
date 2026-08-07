using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyIteration
{
    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
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

    public static IAsyncEnumerable<object> ToSequenceAsync(object value, LythonSourceSpan span)
        => EnumerateCursorAsync(Cursor.Create(value, span));

    public static async ValueTask<List<object>> MaterializeAsync(object value, LythonSourceSpan span)
    {
        var result = new List<object>();
        await foreach (var item in ToSequenceAsync(value, span).ConfigureAwait(false))
        {
            result.Add(item);
        }

        return result;
    }

    private static IEnumerable<object> EnumerateIterator(IPyIteratorValue iterator)
    {
        while (iterator.TryMoveNext(out var value))
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
        private IEnumerator<object>? _syncEnumerator;
        private IAsyncEnumerator<object>? _asyncEnumerator;

        private Cursor(object value, LythonSourceSpan span)
        {
            _value = value;
            _span = span;
        }

        public static Cursor Create(object value, LythonSourceSpan span)
        {
            _ = GetSyncEnumerable(value, span);
            return new Cursor(value, span);
        }

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
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
            if (_value is IPyAsyncIteratorValue asyncIterator)
            {
                return await asyncIterator.TryMoveNextAsync().ConfigureAwait(false);
            }

            if (_value is IPyIteratorValue iterator)
            {
                return iterator.TryMoveNext(out var value)
                    ? (true, value)
                    : (false, PyNone.Instance);
            }

            if (_value is IPyAsyncIterableValue asyncIterable)
            {
                _asyncEnumerator ??= asyncIterable.IterateAsync().GetAsyncEnumerator();
                if (await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    return (true, _asyncEnumerator.Current);
                }

                return (false, PyNone.Instance);
            }

            return TryMoveNext(out var syncValue)
                ? (true, syncValue)
                : (false, PyNone.Instance);
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
    }
}
