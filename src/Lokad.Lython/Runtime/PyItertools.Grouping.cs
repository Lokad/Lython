using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyGroupByIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
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
        object source,
        LythonRuntime.ICallable? keyFunction,
        MemoryGovernor memoryGovernor,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _keyFunction = keyFunction;
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        _activeGroup?.Drain();

        if (!TryReadNext(out var item, out var key))
        {
            value = PyNone.Instance;
            return false;
        }

        _activeGroupId++;
        var group = new PyGroupIterator(this, _activeGroupId, key, item);
        _activeGroup = group;
        var produced = PyTuple.FromOwnedArray([key, _activeGroup], _memoryGovernor, _span);
        value = produced;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        if (_activeGroup is not null)
        {
            await _activeGroup.DrainAsync().ConfigureAwait(false);
        }

        var next = await TryReadNextAsync().ConfigureAwait(false);
        if (!next.HasValue)
        {
            return PyIterationResult.End;
        }

        var item = next.Value.Item;
        var key = next.Value.Key;
        _activeGroupId++;
        var group = new PyGroupIterator(this, _activeGroupId, key, item);
        _activeGroup = group;
        var produced = PyTuple.FromOwnedArray([key, _activeGroup], _memoryGovernor, _span);
        return PyIterationResult.Yield(produced);
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

        if (!_source.TryMoveNext(out var current))
        {
            item = PyNone.Instance;
            key = PyNone.Instance;
            return false;
        }

        item = LythonRuntime.RuntimeValue(current);
        key = ComputeKey(item);
        return true;
    }

    private async ValueTask<OptionalValue<GroupItem>> TryReadNextAsync()
    {
        if (_hasLookahead)
        {
            _hasLookahead = false;
            return OptionalValue<GroupItem>.Present(new GroupItem(_lookaheadItem, _lookaheadKey));
        }

        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return OptionalValue<GroupItem>.Missing;
        }

        var item = LythonRuntime.RuntimeValue(current);
        var key = await ComputeKeyAsync(item).ConfigureAwait(false);
        return OptionalValue<GroupItem>.Present(new GroupItem(item, key));
    }

    private object ComputeKey(object item)
        => _keyFunction is null
            ? item
            : LythonRuntime.RuntimeValue(CallableInvocation.InvokeUnary(_keyFunction, item, _span, _context));

    private async ValueTask<object> ComputeKeyAsync(object item)
        => _keyFunction is null
            ? item
            : LythonRuntime.RuntimeValue(await CallableInvocation.InvokeUnaryAsync(_keyFunction, item, _span, _context).ConfigureAwait(false));

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
            PyIteratorBase.ChargeIteratorValue(parent._memoryGovernor, parent._span);
            _parent = parent;
            _id = id;
            _key = key;
            _firstItem = firstItem;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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

        public override async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            if (_done || _parent._activeGroupId != _id)
            {
                return PyIterationResult.End;
            }

            if (_firstPending)
            {
                _firstPending = false;
                return PyIterationResult.Yield(_firstItem);
            }

            var next = await _parent.TryReadNextAsync().ConfigureAwait(false);
            if (!next.HasValue)
            {
                _done = true;
                return PyIterationResult.End;
            }

            var item = next.Value.Item;
            var key = next.Value.Key;
            if (PyEquality.AreEqual(key, _key))
            {
                return PyIterationResult.Yield(item);
            }

            _parent.StoreLookahead(item, key);
            _done = true;
            return PyIterationResult.End;
        }

        public void Drain()
        {
            while (TryMoveNext(out _))
            {
            }
        }

        public async ValueTask DrainAsync()
        {
            while (true)
            {
                var (hasValue, _) = await TryMoveNextAsync().ConfigureAwait(false);
                if (!hasValue)
                {
                    return;
                }
            }
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools._grouper object>");
    }

    private readonly record struct GroupItem(object Item, object Key);
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

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value) => _state.TryGetNext(_index, out value);

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
        => await _state.TryGetNextAsync(_index).ConfigureAwait(false);

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools._tee object>");
}

internal sealed class PyTeeSharedState
{
    private const long QueuedItemBytes = 64;
    private readonly PyIteration.Cursor _source;
    private readonly Queue<object>[] _queues;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _sourceExhausted;
    private bool _reading;

    public PyTeeSharedState(object source, int count, MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = checked(64L + 80L * count);
        using var storage = memoryGovernor.ReserveTemporary(bytes, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        try
        {
            _queues = new Queue<object>[count];
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                _queues[i] = new Queue<object>();
            }
            _memoryGovernor = memoryGovernor;
            _context = context;
            _span = span;
            storage.Commit(bytes);
        }
        catch { _source.Dispose(); throw; }
    }

    private bool TakeBuffered(int index, [MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (_queues[index].Count == 0) { value = PyNone.Instance; return false; }
        value = _queues[index].Dequeue();
        _memoryGovernor.Release(QueuedItemBytes);
        return true;
    }

    // Queue exposes its current backing through EnsureCapacity(0), which never
    // allocates. Admission covers replacement overlap before EnsureCapacity(n).
    private int NextCapacity(Queue<object> queue)
    {
        var current = queue.EnsureCapacity(0);
        if (queue.Count < current) return current;
        if (current > Array.MaxLength - 4)
            throw RuntimeErrors.Memory("tee queue exceeds managed array capacity", _span);
        return (int)Math.Min(Array.MaxLength, Math.Max(4L, 2L * current));
    }

    private MemoryGovernor.TemporaryMemoryReservation PrepareFanout(int index)
    {
        if (_reading) throw new LythonRuntimeException("RuntimeError", "cannot re-enter the tee iterator", _span);
        var payload = _memoryGovernor.ReserveTemporary(checked(QueuedItemBytes * (_queues.Length - 1L)), _span);
        try
        {
            long replacementBytes = 0;
            for (var i = 0; i < _queues.Length; i++)
            {
                _context.CheckExecution(_span);
                if (i == index) continue;
                var current = _queues[i].EnsureCapacity(0);
                var next = NextCapacity(_queues[i]);
                if (next > current) replacementBytes = checked(replacementBytes + 16L * next);
            }
            using var backing = _memoryGovernor.ReserveTemporary(replacementBytes, _span);
            for (var i = 0; i < _queues.Length; i++)
            {
                _context.CheckExecution(_span);
                if (i == index) continue;
                var queue = _queues[i];
                var current = queue.EnsureCapacity(0);
                if (NextCapacity(queue) == current) continue;
                var capacity = queue.EnsureCapacity(checked(queue.Count + 1));
                // Old backing is relinquished by Queue; actual retained growth
                // stays funded even if a later source pull fails or suspends.
                backing.Commit(16L * (capacity - current));
            }
            return payload;
        }
        catch { payload.Dispose(); throw; }
    }

    private void Publish(int index, object value, MemoryGovernor.TemporaryMemoryReservation payload)
    {
        for (var i = 0; i < _queues.Length; i++)
        {
            _context.CheckExecution(_span);
            if (i == index) continue;
            _queues[i].Enqueue(value); // Capacity is already admitted and allocated.
            payload.Commit(QueuedItemBytes);
        }
    }

    public bool TryGetNext(int index, [MaybeNullWhen(false)] out object value)
    {
        if (TakeBuffered(index, out value)) return true;
        if (_sourceExhausted) return false;
        using var payload = PrepareFanout(index);
        _reading = true;
        try
        {
            if (!_source.TryMoveNext(out var current)) { _sourceExhausted = true; return false; }
            value = LythonRuntime.RuntimeValue(current);
            Publish(index, value, payload);
            return true;
        }
        finally { _reading = false; }
    }

    public async ValueTask<PyIterationResult> TryGetNextAsync(int index)
    {
        if (TakeBuffered(index, out var queued)) return PyIterationResult.Yield(queued);
        if (_sourceExhausted) return PyIterationResult.End;
        using var payload = PrepareFanout(index);
        _reading = true;
        try
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue) { _sourceExhausted = true; return PyIterationResult.End; }
            var value = LythonRuntime.RuntimeValue(current);
            Publish(index, value, payload);
            return PyIterationResult.Yield(value);
        }
        finally { _reading = false; }
    }
}
