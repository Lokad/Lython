namespace Lokad.Lython.Runtime;

internal sealed class PyUserIterator : IPyIteratorValue
{
    private readonly object _iterator;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyUserIterator(PyInstance iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _context = context;
        _span = span;
        if (!iterable.TryGetAttribute("__iter__", context, span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{iterable.Type.Name}' object is not iterable", span);
        }

        _iterator = callable.Invoke([], span, context);
        if (_iterator is not IPyIteratorValue && _iterator is not PyInstance)
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
        }
    }

    public bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_iterator is IPyIteratorValue iterator)
        {
            return iterator.TryMoveNext(out value);
        }

        var instance = (PyInstance)_iterator;
        if (!instance.TryGetAttribute("__next__", _context, _span, out var member) || member is not LythonRuntime.ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", _span);
        }

        try
        {
            value = callable.Invoke([], _span, _context);
            return true;
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
        {
            value = PyNone.Instance;
            return false;
        }
    }

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
}
