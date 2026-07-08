using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyEnumerableIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly string _displayName;

    public PyEnumerableIterator(object source, LythonSourceSpan span, string displayName = "iterator")
    {
        _source = PyIteration.Cursor.Create(source, span);
        _displayName = displayName;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_source.TryMoveNext(out value))
        {
            value = LythonRuntime.RuntimeValue(value);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public override async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
    {
        var (hasValue, value) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        return hasValue
            ? (true, LythonRuntime.RuntimeValue(value))
            : (false, PyNone.Instance);
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<" + _displayName + " object>");
    }
}

internal sealed class PyCallableSentinelIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable _callable;
    private readonly object _sentinel;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyCallableSentinelIterator(
        LythonRuntime.ICallable callable,
        object sentinel,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _callable = callable;
        _sentinel = sentinel;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        var result = LythonRuntime.RuntimeValue(_callable.Invoke([], _span, _context));
        if (PyEquality.AreEqual(result, _sentinel))
        {
            value = PyNone.Instance;
            return false;
        }

        value = result;
        return true;
    }

    public override async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
    {
        _context.CheckExecutionBudget(_span);
        var result = LythonRuntime.RuntimeValue(await _callable.InvokeAsync([], _span, _context).ConfigureAwait(false));
        if (PyEquality.AreEqual(result, _sentinel))
        {
            return (false, PyNone.Instance);
        }

        return (true, result);
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<callable_iterator object>");
    }
}

internal sealed class PyReversedIterator : PyIteratorBase
{
    private readonly Func<int, object> _getIndex;
    private int _nextIndex;

    public PyReversedIterator(int length, Func<int, object> getIndex)
    {
        _nextIndex = length - 1;
        _getIndex = getIndex;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_nextIndex < 0)
        {
            value = PyNone.Instance;
            return false;
        }

        value = LythonRuntime.RuntimeValue(_getIndex(_nextIndex));
        _nextIndex--;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<reversed object>");
    }
}

internal sealed class PyMapIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable _function;
    private readonly PyIteration.Cursor[] _iterators;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyMapIterator(
        LythonRuntime.ICallable function,
        object[] iterables,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _function = function;
        _iterators = new PyIteration.Cursor[iterables.Length];
        for (var i = 0; i < iterables.Length; i++)
        {
            _iterators[i] = PyIteration.Cursor.Create(iterables[i], span);
        }

        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        var arguments = new CallArgumentValue[_iterators.Length];
        for (var i = 0; i < _iterators.Length; i++)
        {
            if (!_iterators[i].TryMoveNext(out var current))
            {
                value = PyNone.Instance;
                return false;
            }

            arguments[i] = new CallArgumentValue(null, LythonRuntime.RuntimeValue(current));
        }

        value = LythonRuntime.RuntimeValue(_function.Invoke(arguments, _span, _context));
        return true;
    }

    public override async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
    {
        var arguments = new CallArgumentValue[_iterators.Length];
        for (var i = 0; i < _iterators.Length; i++)
        {
            var (hasValue, current) = await _iterators[i].TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                return (false, PyNone.Instance);
            }

            arguments[i] = new CallArgumentValue(null, LythonRuntime.RuntimeValue(current));
        }

        var value = LythonRuntime.RuntimeValue(await _function.InvokeAsync(arguments, _span, _context).ConfigureAwait(false));
        return (true, value);
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<map object>");
    }
}

internal sealed class PyFilterIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable? _function;
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyFilterIterator(
        LythonRuntime.ICallable? function,
        object source,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _function = function;
        _source = PyIteration.Cursor.Create(source, span);
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        while (_source.TryMoveNext(out var current))
        {
            var candidate = LythonRuntime.RuntimeValue(current);
            var keep = _function is null
                ? PyTruthiness.IsTruthy(candidate)
                : PyTruthiness.IsTruthy(_function.Invoke([new CallArgumentValue(null, candidate)], _span, _context));

            if (keep)
            {
                value = candidate;
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public override async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
    {
        while (true)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                return (false, PyNone.Instance);
            }

            var candidate = LythonRuntime.RuntimeValue(current);
            var keep = _function is null
                ? PyTruthiness.IsTruthy(candidate)
                : PyTruthiness.IsTruthy(await _function.InvokeAsync([new CallArgumentValue(null, candidate)], _span, _context).ConfigureAwait(false));

            if (keep)
            {
                return (true, candidate);
            }
        }
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<filter object>");
    }
}
