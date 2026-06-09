using Lokad.Lython.Runtime.Text;
using System.Numerics;

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

internal sealed class PyCountIterator : PyIteratorBase
{
    private readonly object _step;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _current;
    private bool _started;

    public PyCountIterator(object start, object step, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _current = LythonRuntime.RuntimeValue(start);
        _step = LythonRuntime.RuntimeValue(step);
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (!_started)
        {
            _started = true;
            value = _current;
            return true;
        }

        _current = LythonRuntime.RuntimeValue(LythonRuntime.AddRuntimeValues(_current, _step, _context, _span));
        value = _current;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.count object>");
}

internal sealed class PyRepeatIterator : PyIteratorBase
{
    private readonly object _item;
    private long? _remaining;

    public PyRepeatIterator(object item, long? times)
    {
        _item = LythonRuntime.RuntimeValue(item);
        _remaining = times;
    }

    public override bool TryMoveNext(out object value)
    {
        if (_remaining is { } remaining)
        {
            if (remaining <= 0)
            {
                value = PyNone.Instance;
                return false;
            }

            _remaining = remaining - 1;
        }

        value = _item;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.repeat object>");
}

internal sealed class PyCycleIterator : PyIteratorBase
{
    private const long CachedItemBytes = 64;

    private readonly IEnumerator<object> _source;
    private readonly List<object> _saved = [];
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _sourceExhausted;
    private int _index;

    public PyCycleIterator(IEnumerable<object> source, MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (!_sourceExhausted)
        {
            if (_source.MoveNext())
            {
                value = LythonRuntime.RuntimeValue(_source.Current);
                _memoryGovernor.Reserve(CachedItemBytes, _span);
                _memoryGovernor.Commit(CachedItemBytes);
                _saved.Add(value);
                _context.ObserveCollectionCount(_saved.Count, _span);
                return true;
            }

            _sourceExhausted = true;
        }

        if (_saved.Count == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        value = _saved[_index];
        _index = (_index + 1) % _saved.Count;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.cycle object>");
}

internal sealed class PyCombinationsIterator : PyIteratorBase
{
    private readonly object[] _pool;
    private readonly int[] _indices;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private bool _started;
    private bool _done;

    public PyCombinationsIterator(object[] pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _pool = pool;
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        for (var i = 0; i < r; i++)
        {
            _indices[i] = i;
        }

        _done = r > pool.Length;
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

        var r = _indices.Length;
        var n = _pool.Length;
        var i = r - 1;
        while (i >= 0 && _indices[i] == i + n - r)
        {
            i--;
        }

        if (i < 0)
        {
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        _indices[i]++;
        for (var j = i + 1; j < r; j++)
        {
            _indices[j] = _indices[j - 1] + 1;
        }

        value = CurrentTuple();
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations object>");

    private PyTuple CurrentTuple()
    {
        var items = new object[_indices.Length];
        for (var i = 0; i < _indices.Length; i++)
        {
            items[i] = _pool[_indices[i]];
        }

        return PyTuple.FromOwnedArray(items, _memoryGovernor, _span);
    }
}

internal sealed class PyCombinationsWithReplacementIterator : PyIteratorBase
{
    private readonly object[] _pool;
    private readonly int[] _indices;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private bool _started;
    private bool _done;

    public PyCombinationsWithReplacementIterator(object[] pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _pool = pool;
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        _done = pool.Length == 0 && r > 0;
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

        var n = _pool.Length;
        var i = _indices.Length - 1;
        while (i >= 0 && _indices[i] == n - 1)
        {
            i--;
        }

        if (i < 0)
        {
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        var next = _indices[i] + 1;
        for (var j = i; j < _indices.Length; j++)
        {
            _indices[j] = next;
        }

        value = CurrentTuple();
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations_with_replacement object>");

    private PyTuple CurrentTuple()
    {
        var items = new object[_indices.Length];
        for (var i = 0; i < _indices.Length; i++)
        {
            items[i] = _pool[_indices[i]];
        }

        return PyTuple.FromOwnedArray(items, _memoryGovernor, _span);
    }
}

internal sealed class PyPermutationsIterator : PyIteratorBase
{
    private readonly object[] _pool;
    private readonly int[] _indices;
    private readonly int[] _cycles;
    private readonly int _r;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private bool _started;
    private bool _done;

    public PyPermutationsIterator(object[] pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _pool = pool;
        _r = r;
        _memoryGovernor = memoryGovernor;
        _span = span;
        _indices = new int[pool.Length];
        for (var i = 0; i < _indices.Length; i++)
        {
            _indices[i] = i;
        }

        _cycles = new int[r];
        for (var i = 0; i < r; i++)
        {
            _cycles[i] = pool.Length - i;
        }

        _done = r > pool.Length;
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

        for (var i = _r - 1; i >= 0; i--)
        {
            _cycles[i]--;
            if (_cycles[i] == 0)
            {
                RotateLeft(_indices, i);
                _cycles[i] = _pool.Length - i;
                continue;
            }

            var j = _cycles[i];
            (_indices[i], _indices[^j]) = (_indices[^j], _indices[i]);
            value = CurrentTuple();
            return true;
        }

        _done = true;
        value = PyNone.Instance;
        return false;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.permutations object>");

    private PyTuple CurrentTuple()
    {
        var items = new object[_r];
        for (var i = 0; i < _r; i++)
        {
            items[i] = _pool[_indices[i]];
        }

        return PyTuple.FromOwnedArray(items, _memoryGovernor, _span);
    }

    private static void RotateLeft(int[] values, int start)
    {
        if (start >= values.Length - 1)
        {
            return;
        }

        var first = values[start];
        for (var i = start; i < values.Length - 1; i++)
        {
            values[i] = values[i + 1];
        }

        values[^1] = first;
    }
}

internal sealed class PyAccumulateIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly LythonRuntime.ICallable? _function;
    private readonly object _initial;
    private readonly bool _hasInitial;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _total = PyNone.Instance;
    private bool _started;

    public PyAccumulateIterator(
        IEnumerable<object> source,
        LythonRuntime.ICallable? function,
        object initial,
        bool hasInitial,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _function = function;
        _initial = initial;
        _hasInitial = hasInitial;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (!_started)
        {
            _started = true;
            if (_hasInitial)
            {
                _total = LythonRuntime.RuntimeValue(_initial);
                value = _total;
                return true;
            }

            if (!_source.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _total = LythonRuntime.RuntimeValue(_source.Current);
            value = _total;
            return true;
        }

        if (!_source.MoveNext())
        {
            value = PyNone.Instance;
            return false;
        }

        var next = LythonRuntime.RuntimeValue(_source.Current);
        _total = _function is null
            ? LythonRuntime.RuntimeValue(LythonRuntime.AddRuntimeValues(_total, next, _context, _span))
            : LythonRuntime.RuntimeValue(_function.Invoke([new CallArgumentValue(null, _total), new CallArgumentValue(null, next)], _span, _context));
        value = _total;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.accumulate object>");
}

internal sealed class PyCompressIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _data;
    private readonly IEnumerator<object> _selectors;

    public PyCompressIterator(IEnumerable<object> data, IEnumerable<object> selectors)
    {
        _data = data.GetEnumerator();
        _selectors = selectors.GetEnumerator();
    }

    public override bool TryMoveNext(out object value)
    {
        while (_data.MoveNext() && _selectors.MoveNext())
        {
            if (PyTruthiness.IsTruthy(_selectors.Current))
            {
                value = LythonRuntime.RuntimeValue(_data.Current);
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.compress object>");
}

internal enum PyPredicateIteratorMode
{
    FilterFalse,
    DropWhile,
    TakeWhile,
}

internal sealed class PyPredicateIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable? _predicate;
    private readonly IEnumerator<object> _source;
    private readonly PyPredicateIteratorMode _mode;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _dropping = true;
    private bool _done;

    public PyPredicateIterator(
        LythonRuntime.ICallable? predicate,
        IEnumerable<object> source,
        PyPredicateIteratorMode mode,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _predicate = predicate;
        _source = source.GetEnumerator();
        _mode = mode;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        while (_source.MoveNext())
        {
            var item = LythonRuntime.RuntimeValue(_source.Current);
            var matched = PredicateMatches(item);
            switch (_mode)
            {
                case PyPredicateIteratorMode.FilterFalse:
                    if (!matched)
                    {
                        value = item;
                        return true;
                    }

                    break;

                case PyPredicateIteratorMode.DropWhile:
                    if (!_dropping || !matched)
                    {
                        _dropping = false;
                        value = item;
                        return true;
                    }

                    break;

                case PyPredicateIteratorMode.TakeWhile:
                    if (!matched)
                    {
                        _done = true;
                        value = PyNone.Instance;
                        return false;
                    }

                    value = item;
                    return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public override PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(_mode switch
        {
            PyPredicateIteratorMode.FilterFalse => "<itertools.filterfalse object>",
            PyPredicateIteratorMode.DropWhile => "<itertools.dropwhile object>",
            _ => "<itertools.takewhile object>",
        });

    private bool PredicateMatches(object item)
        => _predicate is null
            ? PyTruthiness.IsTruthy(item)
            : PyTruthiness.IsTruthy(_predicate.Invoke([new CallArgumentValue(null, item)], _span, _context));
}

internal sealed class PyStarmapIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable _function;
    private readonly IEnumerator<object> _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyStarmapIterator(LythonRuntime.ICallable function, IEnumerable<object> source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _function = function;
        _source = source.GetEnumerator();
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (!_source.MoveNext())
        {
            value = PyNone.Instance;
            return false;
        }

        var args = LythonRuntime.ToSequence(_source.Current, _span)
            .Select(item => new CallArgumentValue(null, LythonRuntime.RuntimeValue(item)))
            .ToArray();
        value = LythonRuntime.RuntimeValue(_function.Invoke(args, _span, _context));
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.starmap object>");
}

internal sealed class PyPairwiseIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private object _previous = PyNone.Instance;
    private bool _hasPrevious;

    public PyPairwiseIterator(IEnumerable<object> source, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _memoryGovernor = memoryGovernor;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        if (!_hasPrevious)
        {
            if (!_source.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _previous = LythonRuntime.RuntimeValue(_source.Current);
            _hasPrevious = true;
        }

        if (!_source.MoveNext())
        {
            value = PyNone.Instance;
            return false;
        }

        var current = LythonRuntime.RuntimeValue(_source.Current);
        value = PyTuple.FromOwnedArray([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.pairwise object>");
}

internal sealed class PyGroupByIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly LythonRuntime.ICallable? _keyFunction;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _lookaheadItem = PyNone.Instance;
    private object _lookaheadKey = PyNone.Instance;
    private bool _hasLookahead;
    private int _activeGroupId;
    private PyGroupIterator? _activeGroup;

    public PyGroupByIterator(
        IEnumerable<object> source,
        LythonRuntime.ICallable? keyFunction,
        MemoryGovernor memoryGovernor,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _keyFunction = keyFunction;
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        _context.CheckExecutionBudget(_span);
        _activeGroup?.Drain();

        if (!TryReadNext(out var item, out var key))
        {
            value = PyNone.Instance;
            return false;
        }

        _activeGroupId++;
        _activeGroup = new PyGroupIterator(this, _activeGroupId, key, item);
        value = PyTuple.FromOwnedArray([key, _activeGroup], _memoryGovernor, _span);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.groupby object>");

    private bool TryReadNext(out object item, out object key)
    {
        if (_hasLookahead)
        {
            _hasLookahead = false;
            item = _lookaheadItem;
            key = _lookaheadKey;
            return true;
        }

        if (!_source.MoveNext())
        {
            item = PyNone.Instance;
            key = PyNone.Instance;
            return false;
        }

        item = LythonRuntime.RuntimeValue(_source.Current);
        key = ComputeKey(item);
        return true;
    }

    private object ComputeKey(object item)
        => _keyFunction is null
            ? item
            : LythonRuntime.RuntimeValue(_keyFunction.Invoke([new CallArgumentValue(null, item)], _span, _context));

    private void StoreLookahead(object item, object key)
    {
        _lookaheadItem = item;
        _lookaheadKey = key;
        _hasLookahead = true;
    }

    private sealed class PyGroupIterator : PyIteratorBase
    {
        private readonly PyGroupByIterator _parent;
        private readonly int _id;
        private readonly object _key;
        private readonly object _firstItem;
        private bool _firstPending = true;
        private bool _done;

        public PyGroupIterator(PyGroupByIterator parent, int id, object key, object firstItem)
        {
            _parent = parent;
            _id = id;
            _key = key;
            _firstItem = firstItem;
        }

        public override bool TryMoveNext(out object value)
        {
            if (_done || _parent._activeGroupId != _id)
            {
                value = PyNone.Instance;
                return false;
            }

            if (_firstPending)
            {
                _firstPending = false;
                value = _firstItem;
                return true;
            }

            if (!_parent.TryReadNext(out var item, out var key))
            {
                _done = true;
                value = PyNone.Instance;
                return false;
            }

            if (PyEquality.AreEqual(key, _key))
            {
                value = item;
                return true;
            }

            _parent.StoreLookahead(item, key);
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        public void Drain()
        {
            while (TryMoveNext(out _))
            {
            }
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools._grouper object>");
    }
}

internal sealed class PyTeeIterator : PyIteratorBase
{
    private readonly PyTeeSharedState _state;
    private readonly int _index;

    public PyTeeIterator(PyTeeSharedState state, int index)
    {
        _state = state;
        _index = index;
    }

    public override bool TryMoveNext(out object value) => _state.TryGetNext(_index, out value);

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools._tee object>");
}

internal sealed class PyTeeSharedState
{
    private const long QueuedItemBytes = 64;

    private readonly IEnumerator<object> _source;
    private readonly Queue<object>[] _queues;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _sourceExhausted;

    public PyTeeSharedState(IEnumerable<object> source, int count, MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _queues = new Queue<object>[count];
        for (var i = 0; i < _queues.Length; i++)
        {
            _queues[i] = new Queue<object>();
        }

        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public bool TryGetNext(int index, out object value)
    {
        _context.CheckExecutionBudget(_span);
        var ownQueue = _queues[index];
        if (ownQueue.Count > 0)
        {
            value = ownQueue.Dequeue();
            _memoryGovernor.Release(QueuedItemBytes);
            return true;
        }

        if (_sourceExhausted)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_source.MoveNext())
        {
            _sourceExhausted = true;
            value = PyNone.Instance;
            return false;
        }

        value = LythonRuntime.RuntimeValue(_source.Current);
        for (var i = 0; i < _queues.Length; i++)
        {
            if (i == index)
            {
                continue;
            }

            _memoryGovernor.Reserve(QueuedItemBytes, _span);
            _memoryGovernor.Commit(QueuedItemBytes);
            _queues[i].Enqueue(value);
            _context.ObserveCollectionCount(_queues[i].Count, _span);
        }

        return true;
    }
}

internal sealed class PyBatchedIterator : PyIteratorBase
{
    private readonly IEnumerator<object> _source;
    private readonly int _size;
    private readonly bool _strict;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;

    public PyBatchedIterator(IEnumerable<object> source, int size, bool strict, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _source = source.GetEnumerator();
        _size = size;
        _strict = strict;
        _memoryGovernor = memoryGovernor;
        _span = span;
    }

    public override bool TryMoveNext(out object value)
    {
        var items = new object[_size];
        var count = 0;
        while (count < _size && _source.MoveNext())
        {
            items[count++] = LythonRuntime.RuntimeValue(_source.Current);
        }

        if (count == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        if (_strict && count < _size)
        {
            throw new LythonRuntimeException("ValueError", "batched(): incomplete batch", _span);
        }

        if (count != _size)
        {
            Array.Resize(ref items, count);
        }

        value = PyTuple.FromOwnedArray(items, _memoryGovernor, _span);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.batched object>");
}
