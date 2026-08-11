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
        _source = PyIteration.Cursor.Create(source, span);
        _keyFunction = keyFunction;
        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
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

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecutionBudget(_span);
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
        _activeGroup = new PyGroupIterator(this, _activeGroupId, key, item);
        var value = PyTuple.FromOwnedArray([key, _activeGroup], _memoryGovernor, _span);
        return PyIterationResult.Yield(value);
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

    public PyTeeSharedState(object source, int count, MemoryGovernor memoryGovernor, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _source = PyIteration.Cursor.Create(source, span);
        _queues = new Queue<object>[count];
        for (var i = 0; i < _queues.Length; i++)
        {
            _queues[i] = new Queue<object>();
        }

        _memoryGovernor = memoryGovernor;
        _context = context;
        _span = span;
    }

    public bool TryGetNext(int index, [MaybeNullWhen(false)] out object value)
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

        if (!_source.TryMoveNext(out var current))
        {
            _sourceExhausted = true;
            value = PyNone.Instance;
            return false;
        }

        value = LythonRuntime.RuntimeValue(current);
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

    public async ValueTask<PyIterationResult> TryGetNextAsync(int index)
    {
        _context.CheckExecutionBudget(_span);
        var ownQueue = _queues[index];
        if (ownQueue.Count > 0)
        {
            var queued = ownQueue.Dequeue();
            _memoryGovernor.Release(QueuedItemBytes);
            return PyIterationResult.Yield(queued);
        }

        if (_sourceExhausted)
        {
            return PyIterationResult.End;
        }

        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            _sourceExhausted = true;
            return PyIterationResult.End;
        }

        var value = LythonRuntime.RuntimeValue(current);
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

        return PyIterationResult.Yield(value);
    }
}
