using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyEnumerableIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly string _displayName;

    public PyEnumerableIterator(IEnumerable<object> source, string displayName = "iterator")
    {
        _source = source.GetEnumerator();
        _displayName = displayName;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_source.MoveNext())
        {
            value = LythonRuntime.RuntimeValue(_source.Current);
            return true;
        }

        value = PyNone.Instance;
        return false;
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
    private readonly IEnumerator<object>[] _iterators;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyMapIterator(
        LythonRuntime.ICallable function,
        IEnumerable<object>[] iterables,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _function = function;
        _iterators = new IEnumerator<object>[iterables.Length];
        for (var i = 0; i < iterables.Length; i++)
        {
            _iterators[i] = iterables[i].GetEnumerator();
        }

        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        var arguments = new CallArgumentValue[_iterators.Length];
        for (var i = 0; i < _iterators.Length; i++)
        {
            if (!_iterators[i].MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            arguments[i] = new CallArgumentValue(null, LythonRuntime.RuntimeValue(_iterators[i].Current));
        }

        value = LythonRuntime.RuntimeValue(_function.Invoke(arguments, _span, _context));
        return true;
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
    private readonly IEnumerator<object> _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyFilterIterator(
        LythonRuntime.ICallable? function,
        IEnumerable<object> source,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _function = function;
        _source = source.GetEnumerator();
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        while (_source.MoveNext())
        {
            var candidate = LythonRuntime.RuntimeValue(_source.Current);
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

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<filter object>");
    }
}
