using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal abstract class PyIteratorBase : IPyIteratorValue, IPyRenderableValue
{
    public IEnumerable<object> Iterate()
    {
        while (TryMoveNext(out var value))
        {
            yield return value;
        }
    }

    public abstract bool TryMoveNext(out object value);

    public abstract PyString RenderPython(PyRenderingContext context);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyChainIterator : PyIteratorBase
{
    private readonly IEnumerator<IEnumerable<object>> _outer;
    private IEnumerator<object>? _current;

    public PyChainIterator(IEnumerable<IEnumerable<object>> iterables)
    {
        _outer = iterables.GetEnumerator();
    }

    public override bool TryMoveNext(out object value)
    {
        while (true)
        {
            if (_current is not null && _current.MoveNext())
            {
                value = LythonRuntime.RuntimeValue(_current.Current);
                return true;
            }

            if (!_outer.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _current = _outer.Current.GetEnumerator();
        }
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.chain object>");
}

internal sealed class PyIsliceIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly long _stop;
    private readonly long _step;
    private long _position;
    private long _yielded;

    public PyIsliceIterator(IEnumerable<object> source, long start, long stop, long step)
    {
        _source = source.GetEnumerator();
        _position = start;
        _stop = stop;
        _step = step;

        for (var i = 0L; i < start && _source.MoveNext(); i++)
        {
        }
    }

    public override bool TryMoveNext(out object value)
    {
        if (_position >= _stop)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_source.MoveNext())
        {
            value = PyNone.Instance;
            return false;
        }

        value = LythonRuntime.RuntimeValue(_source.Current);
        _yielded++;
        _position++;

        var skip = _step - 1;
        for (var i = 0L; i < skip && _position < _stop && _source.MoveNext(); i++)
        {
            _position++;
        }

        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.islice object>");
}

internal sealed class PyProductIterator : PyIteratorBase
{
    private readonly IReadOnlyList<IReadOnlyList<object>> _pools;
    private readonly int[] _indices;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private bool _started;
    private bool _done;

    public PyProductIterator(IReadOnlyList<IReadOnlyList<object>> pools, MemoryGovernor? memoryGovernor = null, LythonSourceSpan? allocationSpan = null)
    {
        _pools = pools;
        _indices = new int[pools.Count];
        _memoryGovernor = memoryGovernor;
        _allocationSpan = allocationSpan;
        for (var i = 0; i < pools.Count; i++)
        {
            if (pools[i].Count == 0)
            {
                _done = true;
                break;
            }
        }
    }

    public override bool TryMoveNext(out object value)
    {
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            _started = true;
            value = CurrentTuple();
            return true;
        }

        for (var i = _indices.Length - 1; i >= 0; i--)
        {
            if (_indices[i] + 1 < _pools[i].Count)
            {
                _indices[i]++;
                for (var j = i + 1; j < _indices.Length; j++)
                {
                    _indices[j] = 0;
                }

                value = CurrentTuple();
                return true;
            }
        }

        _done = true;
        value = PyNone.Instance;
        return false;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.product object>");

    private PyTuple CurrentTuple()
    {
        var items = new object[_indices.Length];
        for (var i = 0; i < _indices.Length; i++)
        {
            items[i] = _pools[i][_indices[i]];
        }

        return _memoryGovernor is null
            ? PyTuple.FromOwnedArray(items)
            : PyTuple.FromOwnedArray(items, _memoryGovernor, _allocationSpan);
    }
}

internal sealed class PyZipLongestIterator : PyIteratorBase
{
    private readonly IEnumerator<object>[] _iterators;
    private readonly object _fillValue;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private bool _done;

    public PyZipLongestIterator(IEnumerable<IEnumerable<object>> iterables, object fillValue, MemoryGovernor? memoryGovernor = null, LythonSourceSpan? allocationSpan = null)
    {
        if (iterables is IEnumerable<object>[] array)
        {
            _iterators = new IEnumerator<object>[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                _iterators[i] = array[i].GetEnumerator();
            }
        }
        else
        {
            var materialized = iterables.ToArray();
            _iterators = new IEnumerator<object>[materialized.Length];
            for (var i = 0; i < materialized.Length; i++)
            {
                _iterators[i] = materialized[i].GetEnumerator();
            }
        }

        _fillValue = fillValue;
        _memoryGovernor = memoryGovernor;
        _allocationSpan = allocationSpan;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_done || _iterators.Length == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        var items = new object[_iterators.Length];
        var anyAdvanced = false;
        for (var i = 0; i < _iterators.Length; i++)
        {
            if (_iterators[i].MoveNext())
            {
                items[i] = LythonRuntime.RuntimeValue(_iterators[i].Current);
                anyAdvanced = true;
            }
            else
            {
                items[i] = _fillValue;
            }
        }

        if (!anyAdvanced)
        {
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        value = _memoryGovernor is null
            ? new PyTuple(items)
            : new PyTuple(items, _memoryGovernor, _allocationSpan);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.zip_longest object>");
}
