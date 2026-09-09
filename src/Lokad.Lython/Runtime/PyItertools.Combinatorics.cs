using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class PyCombinatoricTuple
{
    public static PyTuple Create(
        object[] pool,
        int[] indices,
        int count,
        MemoryGovernor memoryGovernor,
        LythonSourceSpan span)
    {
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = pool[indices[i]];
        }

        return PyTuple.FromOwnedArray(items, memoryGovernor, span);
    }
}

internal static class PyCombinatoricOwnership
{
    // Index tables persist for the iterator lifetime; charge them once at
    // construction instead of per produced tuple.
    internal static void ChargeIndexArray(MemoryGovernor? governor, LythonSourceSpan? span, int length)
    {
        if (governor is null)
        {
            return;
        }

        var bytes = 32L + (4L * length);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }
}

internal sealed class PyProductIterator : PyIteratorBase
{
    private readonly IReadOnlyList<IReadOnlyList<object>> _pools;
    private readonly int[] _indices;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private bool _started;
    private bool _done;

    public PyProductIterator(IReadOnlyList<IReadOnlyList<object>> pools, MemoryGovernor? memoryGovernor, LythonSourceSpan? allocationSpan)
    {
        _pools = pools;
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, allocationSpan, pools.Count);
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

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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
    private readonly PyIteration.Cursor[] _iterators;
    private readonly object _fillValue;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private bool _done;

    public PyZipLongestIterator(IReadOnlyList<object> iterables, object fillValue, LythonSourceSpan span, MemoryGovernor? memoryGovernor, LythonSourceSpan? allocationSpan, LythonRuntime.ExecutionContext context)
    {
        _iterators = new PyIteration.Cursor[iterables.Count];
        for (var i = 0; i < iterables.Count; i++)
        {
            _iterators[i] = PyIteration.Cursor.Create(iterables[i], span, context);
        }

        _fillValue = fillValue;
        _memoryGovernor = memoryGovernor;
        _allocationSpan = allocationSpan;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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
            if (_iterators[i].TryMoveNext(out var current))
            {
                items[i] = LythonRuntime.RuntimeValue(current);
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

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_done || _iterators.Length == 0)
        {
            return PyIterationResult.End;
        }

        var items = new object[_iterators.Length];
        var anyAdvanced = false;
        for (var i = 0; i < _iterators.Length; i++)
        {
            var (hasValue, current) = await _iterators[i].TryMoveNextAsync().ConfigureAwait(false);
            if (hasValue)
            {
                items[i] = LythonRuntime.RuntimeValue(current);
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
            return PyIterationResult.End;
        }

        var value = _memoryGovernor is null
            ? new PyTuple(items)
            : new PyTuple(items, _memoryGovernor, _allocationSpan);
        return PyIterationResult.Yield(value);
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

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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

    private readonly PyIteration.Cursor _source;
    private readonly List<object> _saved = [];
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _sourceExhausted;
    private int _index;

    public PyCycleIterator(object source, MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _source = PyIteration.Cursor.Create(source, span, context);
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (!_sourceExhausted)
        {
            if (_source.TryMoveNext(out var current))
            {
                value = LythonRuntime.RuntimeValue(current);
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

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecutionBudget(_span);
        if (!_sourceExhausted)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (hasValue)
            {
                var value = LythonRuntime.RuntimeValue(current);
                _memoryGovernor.Reserve(CachedItemBytes, _span);
                _memoryGovernor.Commit(CachedItemBytes);
                _saved.Add(value);
                _context.ObserveCollectionCount(_saved.Count, _span);
                return PyIterationResult.Yield(value);
            }

            _sourceExhausted = true;
        }

        if (_saved.Count == 0)
        {
            return PyIterationResult.End;
        }

        var saved = _saved[_index];
        _index = (_index + 1) % _saved.Count;
        return PyIterationResult.Yield(saved);
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
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        for (var i = 0; i < r; i++)
        {
            _indices[i] = i;
        }

        _done = r > pool.Length;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            _started = true;
            value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span);
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

        value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations object>");

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
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        _done = pool.Length == 0 && r > 0;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            _started = true;
            value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span);
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

        value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations_with_replacement object>");

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
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, pool.Length);
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
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

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            _started = true;
            value = PyCombinatoricTuple.Create(_pool, _indices, _r, _memoryGovernor, _span);
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
            value = PyCombinatoricTuple.Create(_pool, _indices, _r, _memoryGovernor, _span);
            return true;
        }

        _done = true;
        value = PyNone.Instance;
        return false;

        static void RotateLeft(int[] values, int start)
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

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.permutations object>");

}
