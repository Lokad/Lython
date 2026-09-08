namespace Lokad.Lython.Runtime;

internal sealed class PyUserIterator : IPyIteratorValue
{
    private readonly object _iterator;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyUserIterator(PyInstance iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        : this(ResolveIterator(InvokeIter(iterable, context, span), context, span), context, span)
    {
    }

    public static async ValueTask<PyUserIterator> CreateAsync(PyInstance iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var resolved = await InvokeIterAsync(iterable, context, span).ConfigureAwait(false);
        return new PyUserIterator(ResolveIterator(resolved, context, span), context, span);
    }

    private PyUserIterator(object iterator, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _iterator = iterator;
        _context = context;
        _span = span;
    }

    /// <summary>
    /// The object produced by <c>__iter__</c>; often the source instance itself,
    /// which is why <c>iter(it) is it</c> holds for well-behaved iterators.
    /// </summary>
    public object Iterator => _iterator;

    private static object InvokeIter(PyInstance iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!iterable.TryGetAttribute("__iter__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{iterable.Type.Name}' object is not iterable", span);
        }

        return callable.Invoke([], span, context);
    }

    private static async ValueTask<object> InvokeIterAsync(PyInstance iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!iterable.TryGetAttribute("__iter__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{iterable.Type.Name}' object is not iterable", span);
        }

        return await callable.InvokeAsync([], span, context).ConfigureAwait(false);
    }

    private static object ResolveIterator(object resolved, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Like CPython, iter() validates eagerly: the result must be an iterator
        // or provide __next__, so a bad __iter__ fails here instead of later.
        if (resolved is IPyIteratorValue)
        {
            return resolved;
        }

        if (resolved is PyInstance instance &&
            instance.TryGetAttribute("__next__", context, span, out _))
        {
            return instance;
        }

        throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
    }

    internal static bool HasNext(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => instance.TryGetAttribute("__next__", context, span, out var member) && member is LythonRuntime.ICallable;

    public bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_iterator is IPyIteratorValue iterator)
        {
            return iterator.TryMoveNext(out value);
        }

        return TryAdvanceInstance((PyInstance)_iterator, _context, _span, out value);
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_iterator is IPyIteratorValue iterator)
        {
            return iterator.TryMoveNext(out var value)
                ? PyIterationResult.Yield(value)
                : PyIterationResult.End;
        }

        return await TryAdvanceInstanceAsync((PyInstance)_iterator, _context, _span).ConfigureAwait(false);
    }

    internal static bool TryAdvanceInstance(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (!instance.TryGetAttribute("__next__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
        }

        try
        {
            value = callable.Invoke([], span, context);
            return true;
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
        {
            value = PyNone.Instance;
            return false;
        }
    }

    internal static async ValueTask<PyIterationResult> TryAdvanceInstanceAsync(PyInstance instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute("__next__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
        }

        try
        {
            var value = await callable.InvokeAsync([], span, context).ConfigureAwait(false);
            return PyIterationResult.Yield(value);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
        {
            return PyIterationResult.End;
        }
    }

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
}
