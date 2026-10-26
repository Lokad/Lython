using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyGroupByIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly object? _keyFunction;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _item = PyNone.Instance, _key = PyNone.Instance, _target = PyNone.Instance;
    private bool _hasCurrent, _hasTarget;
    private long _groupId;

    public PyGroupByIterator(PyIteration.Cursor source, object? keyFunction, MemoryGovernor governor,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _source = source; _keyFunction = keyFunction; _memoryGovernor = governor;
        _context = LythonRuntime.GetGlobalContext(context); _span = span;
    }

    private bool Advance()
    {
        if (!_source.TryMoveNext(out var item)) return false;
        var key = _keyFunction is null ? item : CallableInvocation.InvokeUnary(_keyFunction, item, _span, _context);
        _item = LythonRuntime.RuntimeValue(item); _key = LythonRuntime.RuntimeValue(key); _hasCurrent = true;
        return true;
    }

    private async ValueTask<bool> AdvanceAsync()
    {
        var item = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!item.HasValue) return false;
        var key = _keyFunction is null ? item.Value
            : await CallableInvocation.InvokeUnaryAsync(_keyFunction, item.Value, _span, _context).ConfigureAwait(false);
        _item = LythonRuntime.RuntimeValue(item.Value); _key = LythonRuntime.RuntimeValue(key); _hasCurrent = true;
        return true;
    }

    private object TakeCurrent()
    {
        var item = _item;
        _hasCurrent = false; _item = _key = PyNone.Instance;
        return item;
    }

    private PyTuple CreateGroup()
    {
        _target = _key; _hasTarget = true;
        return PyTuple.FromOwnedArray([_key, new PyGroupIterator(this, _groupId, _target)], _memoryGovernor, _span);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _groupId++;
        while (true)
        {
            _context.CheckExecution(_span);
            if (!_hasCurrent && !Advance()) { value = PyNone.Instance; return false; }
            if (!_hasTarget || !LythonRuntime.ElementEquals(_target, _key, _context, _span)) break;
            TakeCurrent();
        }
        value = CreateGroup();
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _groupId++;
        while (true)
        {
            _context.CheckExecution(_span);
            if (!_hasCurrent && !await AdvanceAsync().ConfigureAwait(false)) return PyIterationResult.End;
            if (!_hasTarget || !await LythonRuntime.ElementEqualsAsync(_target, _key, _context, _span).ConfigureAwait(false)) break;
            TakeCurrent();
        }
        return PyIterationResult.Yield(CreateGroup());
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.groupby object>");

    private sealed class PyGroupIterator : PyIteratorBase
    {
        private readonly PyGroupByIterator _parent;
        private readonly long _id;
        private readonly object _target;
        public PyGroupIterator(PyGroupByIterator parent, long id, object target)
        {
            ChargeIteratorValue(parent._memoryGovernor, parent._span);
            _parent = parent; _id = id; _target = target;
        }
        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            value = PyNone.Instance;
            if (_parent._groupId != _id || !_parent._hasCurrent && !_parent.Advance()) return false;
            if (!LythonRuntime.ElementEquals(_target, _parent._key, _parent._context, _parent._span) || !_parent._hasCurrent) return false;
            value = _parent.TakeCurrent();
            return true;
        }
        public override async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            if (_parent._groupId != _id || !_parent._hasCurrent && !await _parent.AdvanceAsync().ConfigureAwait(false)) return PyIterationResult.End;
            if (!await LythonRuntime.ElementEqualsAsync(_target, _parent._key, _parent._context, _parent._span).ConfigureAwait(false) || !_parent._hasCurrent) return PyIterationResult.End;
            return PyIterationResult.Yield(_parent.TakeCurrent());
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
