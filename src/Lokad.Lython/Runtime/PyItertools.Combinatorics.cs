using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class PyCombinatoricTuple
{
    private static long ScalarHeadroom(int count) => checked(RuntimeMemoryEstimates.ScalarStorageBytes * count);

    internal static MemoryGovernor.TemporaryMemoryReservation Reserve(int count, MemoryGovernor governor, LythonSourceSpan span)
        => governor.ReserveTemporary(checked(PyTuple.EstimateApproximateBytes(count) + ScalarHeadroom(count)), span);

    internal static PyTuple Publish(object[] items, MemoryGovernor governor, LythonSourceSpan span,
        MemoryGovernor.TemporaryMemoryReservation storage)
        => PyTuple.FromReservedOwnedArray(items, governor, span, storage, ScalarHeadroom(items.Length));

    public static PyTuple Create(IReadOnlyList<object> pool, int[] indices, int count,
        MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        using var storage = Reserve(count, governor, span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            context.CheckExecution(span);
            items[i] = pool[indices[i]];
        }
        return Publish(items, governor, span, storage);
    }
}

internal static class PyCombinatoricOwnership
{
    internal static long PoolArrayBytes(long length) => checked(64L + (16L * length));

    internal static long IndexArrayBytes(long length) => checked(32L + (4L * length));

    // Index tables persist for the iterator lifetime; charge them once at
    // construction instead of per produced tuple.
    internal static void ChargeIndexArray(MemoryGovernor? governor, LythonSourceSpan? span, int length)
    {
        if (governor is null)
        {
            return;
        }

        var bytes = IndexArrayBytes(length);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }
}

internal sealed class PyProductIterator : PyIteratorBase
{
    private readonly IReadOnlyList<IReadOnlyList<object>> _pools;
    private readonly int[] _indices;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _allocationSpan;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _started;
    private bool _done;

    public PyProductIterator(IReadOnlyList<IReadOnlyList<object>> pools, MemoryGovernor memoryGovernor, LythonSourceSpan allocationSpan, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, allocationSpan);
        _context = context;
        _pools = pools;
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, allocationSpan, pools.Count);
        _indices = new int[pools.Count];
        _memoryGovernor = memoryGovernor;
        _allocationSpan = allocationSpan;
        for (var i = 0; i < pools.Count; i++)
        {
            _context.CheckExecution(_allocationSpan);
            if (pools[i].Count == 0)
            {
                _done = true;
                break;
            }
        }
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_allocationSpan);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            var first = CurrentTuple();
            _started = true;
            value = first;
            return true;
        }

        for (var i = _indices.Length - 1; i >= 0; i--)
        {
            _context.CheckExecution(_allocationSpan);
            if (_indices[i] + 1 < _pools[i].Count)
            {
                _indices[i]++;
                for (var j = i + 1; j < _indices.Length; j++)
                {
                    _context.CheckExecution(_allocationSpan);
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
        using var storage = PyCombinatoricTuple.Reserve(_indices.Length, _memoryGovernor, _allocationSpan);
        var items = new object[_indices.Length];
        for (var i = 0; i < _indices.Length; i++)
        {
            _context.CheckExecution(_allocationSpan);
            items[i] = _pools[i][_indices[i]];
        }

        return PyCombinatoricTuple.Publish(items, _memoryGovernor, _allocationSpan, storage);
    }
}

internal sealed class PyZipLongestIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor[] _iterators;
    private readonly object _fillValue;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _allocationSpan;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _done;
    // Per-input exhaustion latches shared by both paths: an input that raises
    // StopIteration stays exhausted (fill value) instead of being retried every
    // row, and _remaining short-circuits the finished state without pulling or
    // allocating a final unused row.
    private readonly bool[] _exhausted;
    private int _remaining;

    public PyZipLongestIterator(IReadOnlyList<object> iterables, object fillValue, LythonSourceSpan span,
        MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        // Retained cursor objects, reference table and exhaustion flags.
        var bytes = checked(64L + 160L * iterables.Count);
        using var storage = memoryGovernor.ReserveTemporary(bytes, span);
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _iterators = new PyIteration.Cursor[iterables.Count];
        try
        {
            for (var i = 0; i < iterables.Count; i++)
            {
                context.CheckExecution(span);
                _iterators[i] = PyIteration.Cursor.Create(iterables[i], span, context);
            }
            _exhausted = new bool[iterables.Count];
            _fillValue = fillValue;
            _memoryGovernor = memoryGovernor;
            _allocationSpan = span;
            _context = context;
            _remaining = iterables.Count;
            storage.Commit(bytes);
        }
        catch
        {
            foreach (var cursor in _iterators) cursor?.Dispose();
            throw;
        }
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_allocationSpan);
        if (_done || _remaining == 0)
        {
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        using var storage = PyCombinatoricTuple.Reserve(_iterators.Length, _memoryGovernor, _allocationSpan);
        var items = new object[_iterators.Length];
        var anyAdvanced = false;
        for (var i = 0; i < _iterators.Length; i++)
        {
            _context.CheckExecution(_allocationSpan);
            if (_exhausted[i])
            {
                items[i] = _fillValue;
                continue;
            }

            if (_iterators[i].TryMoveNext(out var current))
            {
                items[i] = LythonRuntime.RuntimeValue(current);
                anyAdvanced = true;
            }
            else
            {
                _exhausted[i] = true;
                _remaining--;
                items[i] = _fillValue;
            }
        }

        if (!anyAdvanced)
        {
            _done = true;
            value = PyNone.Instance;
            return false;
        }

        // The row array is built fresh above and never escapes except into
        // the tuple, so the tuple takes ownership instead of copying it.
        var produced = PyCombinatoricTuple.Publish(items, _memoryGovernor, _allocationSpan, storage);
        value = produced;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_allocationSpan);
        if (_done || _remaining == 0)
        {
            _done = true;
            return PyIterationResult.End;
        }

        using var storage = PyCombinatoricTuple.Reserve(_iterators.Length, _memoryGovernor, _allocationSpan);
        var items = new object[_iterators.Length];
        var anyAdvanced = false;
        for (var i = 0; i < _iterators.Length; i++)
        {
            _context.CheckExecution(_allocationSpan);
            if (_exhausted[i])
            {
                items[i] = _fillValue;
                continue;
            }

            var (hasValue, current) = await _iterators[i].TryMoveNextAsync().ConfigureAwait(false);
            if (hasValue)
            {
                items[i] = LythonRuntime.RuntimeValue(current);
                anyAdvanced = true;
            }
            else
            {
                _exhausted[i] = true;
                _remaining--;
                items[i] = _fillValue;
            }
        }

        if (!anyAdvanced)
        {
            _done = true;
            return PyIterationResult.End;
        }

        // Same fresh-owned-array escape proof as the synchronous path.
        var produced = PyCombinatoricTuple.Publish(items, _memoryGovernor, _allocationSpan, storage);
        return PyIterationResult.Yield(produced);
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
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        _current = LythonRuntime.RuntimeValue(start);
        _step = LythonRuntime.RuntimeValue(step);
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
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

    // Bounded repeats know their remaining count like CPython; unbounded
    // repeats report no hint so callers fall back to their default.
    public long? RemainingHint => _remaining;

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
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (!_sourceExhausted)
        {
            if (_source.TryMoveNext(out var current))
            {
                value = LythonRuntime.RuntimeValue(current);
                _memoryGovernor.Reserve(CachedItemBytes, _span);
                _memoryGovernor.Commit(CachedItemBytes);
                _saved.Add(value);
                _context.CheckExecution(_span);
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
        _context.CheckExecution(_span);
        if (!_sourceExhausted)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (hasValue)
            {
                var value = LythonRuntime.RuntimeValue(current);
                _memoryGovernor.Reserve(CachedItemBytes, _span);
                _memoryGovernor.Commit(CachedItemBytes);
                _saved.Add(value);
                _context.CheckExecution(_span);
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
    private readonly IReadOnlyList<object> _pool;
    private readonly int[] _indices;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _started;
    private bool _done;

    public PyCombinationsIterator(IReadOnlyList<object> pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _context = context;
        _pool = pool;
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        for (var i = 0; i < r; i++)
        {
            _context.CheckExecution(_span);
            _indices[i] = i;
        }

        _done = r > pool.Count;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            var first = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span, _context);
            _started = true;
            value = first;
            return true;
        }

        var r = _indices.Length;
        var n = _pool.Count;
        var i = r - 1;
        while (i >= 0 && _indices[i] == i + n - r)
        {
            _context.CheckExecution(_span);
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
            _context.CheckExecution(_span);
            _indices[j] = _indices[j - 1] + 1;
        }

        value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span, _context);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations object>");

}

internal sealed class PyCombinationsWithReplacementIterator : PyIteratorBase
{
    private readonly IReadOnlyList<object> _pool;
    private readonly int[] _indices;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _started;
    private bool _done;

    public PyCombinationsWithReplacementIterator(IReadOnlyList<object> pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _context = context;
        _pool = pool;
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
        _indices = new int[r];
        _memoryGovernor = memoryGovernor;
        _span = span;
        _done = pool.Count == 0 && r > 0;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            var first = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span, _context);
            _started = true;
            value = first;
            return true;
        }

        var n = _pool.Count;
        var i = _indices.Length - 1;
        while (i >= 0 && _indices[i] == n - 1)
        {
            _context.CheckExecution(_span);
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
            _context.CheckExecution(_span);
            _indices[j] = next;
        }

        value = PyCombinatoricTuple.Create(_pool, _indices, _indices.Length, _memoryGovernor, _span, _context);
        return true;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.combinations_with_replacement object>");

}

internal sealed class PyPermutationsIterator : PyIteratorBase
{
    private readonly IReadOnlyList<object> _pool;
    private readonly int[] _indices;
    private readonly int[] _cycles;
    private readonly int _r;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _started;
    private bool _done;

    public PyPermutationsIterator(IReadOnlyList<object> pool, int r, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _context = context;
        _pool = pool;
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, pool.Count);
        PyCombinatoricOwnership.ChargeIndexArray(memoryGovernor, span, r);
        _r = r;
        _memoryGovernor = memoryGovernor;
        _span = span;
        _indices = new int[pool.Count];
        for (var i = 0; i < _indices.Length; i++)
        {
            _context.CheckExecution(_span);
            _indices[i] = i;
        }

        _cycles = new int[r];
        for (var i = 0; i < r; i++)
        {
            _context.CheckExecution(_span);
            _cycles[i] = pool.Count - i;
        }

        _done = r > pool.Count;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_started)
        {
            var first = PyCombinatoricTuple.Create(_pool, _indices, _r, _memoryGovernor, _span, _context);
            _started = true;
            value = first;
            return true;
        }

        for (var i = _r - 1; i >= 0; i--)
        {
            _context.CheckExecution(_span);
            _cycles[i]--;
            if (_cycles[i] == 0)
            {
                RotateLeft(_indices, i);
                _cycles[i] = _pool.Count - i;
                continue;
            }

            var j = _cycles[i];
            (_indices[i], _indices[^j]) = (_indices[^j], _indices[i]);
            value = PyCombinatoricTuple.Create(_pool, _indices, _r, _memoryGovernor, _span, _context);
            return true;
        }

        _done = true;
        value = PyNone.Instance;
        return false;

        void RotateLeft(int[] values, int start)
        {
            if (start >= values.Length - 1)
            {
                return;
            }

            var first = values[start];
            for (var i = start; i < values.Length - 1; i++)
            {
                _context.CheckExecution(_span);
                values[i] = values[i + 1];
            }

            values[^1] = first;
        }
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.permutations object>");

}
