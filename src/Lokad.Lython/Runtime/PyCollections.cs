using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyMutableDynamicAttributes, IPySizedValue
{
    private readonly PyDict _items;

    public PyDefaultDict(object defaultFactory)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict();
    }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor) : this(defaultFactory, governor, null) { }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Own the shell beside the governed inner dict.
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(governor, allocationSpan);
    }

    public PyDefaultDict(object defaultFactory, PyDict items)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(items);
    }

    public object DefaultFactory { get; private set; }

    public int Count => _items.Count;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    internal PyDict InnerDict => _items;

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public object GetOrCreate(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _items.AttachMemoryGovernor(context.MemoryGovernor, span);
        if (_items.TryGetValue(key, out var value))
        {
            return value;
        }

        var created = DefaultFactory switch
        {
            PyNone => throw RuntimeErrors.MissingKey(key, span),
            LythonRuntime.ICallable callable => LythonRuntime.RuntimeValue(callable.Invoke([], span, context)),
            _ => throw new LythonRuntimeException("TypeError", "defaultdict default_factory must be callable or None.", span)
        };

        _items.SetItem(key, created);
        context.ObserveCollectionCount(Count, span);
        return created;
    }

    public void SetItem(object key, object value) => _items.SetItem(key, value);

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => _items.AttachMemoryGovernor(governor, allocationSpan);

    public bool Remove(object key) => _items.Remove(key);

    public bool TryRemoveLast([MaybeNullWhen(false)] out object key, [MaybeNullWhen(false)] out object value)
        => _items.TryRemoveLast(out key, out value);

    public object UpdateFrom(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => LythonRuntime.UpdateDictionary(_items, arguments, span, context);

    public void Clear() => _items.Clear();

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "default_factory")
        {
            value = DefaultFactory;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public bool TrySetMember(string name, object value)
    {
        if (name != "default_factory")
        {
            return false;
        }

        DefaultFactory = ValidateDefaultFactory(value);
        return true;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context)
    {
        var factory = DefaultFactory switch
        {
            PyNone => PyStringOps.NoneLiteral,
            _ => PyRendering.ToPythonPyString(DefaultFactory, context)
        };
        return PyRendering.JoinRenderedSequence(
            "defaultdict(",
            [factory, PyRendering.JoinRenderedDictionary(_items, context, interpolated: false)],
            ")", context);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static object ValidateDefaultFactory(object value)
        => value switch
        {
            PyNone => value,
            LythonRuntime.ICallable => value,
            _ => throw new LythonRuntimeException("TypeError", "defaultdict default_factory must be callable or None.", null)
        };
}
internal sealed class PyCounter : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue
{
    private readonly PyDict _items;

    public PyCounter()
    {
        _items = new PyDict();
    }

    public PyCounter(MemoryGovernor governor) : this(governor, null) { }

    public PyCounter(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Own the shell beside the governed inner dict.
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        _items = new PyDict(governor, allocationSpan);
    }

    public PyCounter(PyCounter other)
    {
        _items = new PyDict(other._items);
    }

    public PyCounter(PyCounter other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyCounter(PyCounter other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        _items = new PyDict(other._items, governor, allocationSpan);
    }

    public int Count => _items.Count;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    internal PyDict InnerDict => _items;

    public object GetCount(object key) => _items.TryGetValue(key, out var value) ? value : BigInteger.Zero;

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public void SetItem(object key, object value) => _items.SetItem(key, value);

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => _items.AttachMemoryGovernor(governor, allocationSpan);

    public bool Remove(object key) => _items.Remove(key);

    public void Clear() => _items.Clear();

    public void Increment(object key, object delta, LythonSourceSpan span)
    {
        if (_items.TryGetValue(key, out var value))
        {
            _items.SetItem(key, LythonRuntime.AddCounterCounts(value, delta, span, OwnerMemoryGovernor));
            return;
        }

        _items.SetItem(key, delta);
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context)
    {
        if (Count == 0)
        {
            return PyString.FromString("Counter()", context.Context.MemoryGovernor);
        }

        return PyRendering.JoinRenderedSequence("Counter(", [PyRendering.JoinRenderedDictionary(_items, context, interpolated: false)], ")", context);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class PyDeque : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue
{
    private readonly LinkedList<object> _items = [];

    // LinkedList nodes are separate heap objects (prev/next/value plus header);
    // charge each live node so retained deques accumulate like other containers.
    // Empty deques stay free like empty sets; the list object itself remains
    // wrapper overhead (MG04).
    private const long DequeNodeBytes = 64;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedNodeBytes;

    public PyDeque() : this((int?)null) { }

    public PyDeque(int? maxLength)
    {
        MaxLength = maxLength;
    }

    public PyDeque(int? maxLength, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        MaxLength = maxLength;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PyDeque(IEnumerable<object> items) : this(items, null) { }

    public PyDeque(IEnumerable<object> items, int? maxLength)
    {
        MaxLength = maxLength;
        foreach (var item in items)
        {
            Append(item);
        }
    }

    public PyDeque(IEnumerable<object> items, int? maxLength, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(maxLength, governor, allocationSpan)
    {
        foreach (var item in items)
        {
            Append(item);
        }
    }

    public int? MaxLength { get; }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public int Count => _items.Count;

    public int Length => Count;

    public object this[int index] => GetItem(index);

    public void Append(object value)
    {
        if (MaxLength == 0)
        {
            return;
        }

        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            // Bounded eviction reuses one node charge: the list stays at maxlen,
            // so no new budget is needed and a tight budget still rotates.
            _items.RemoveFirst();
            _items.AddLast(value);
            return;
        }

        ReserveNode();
        _items.AddLast(value);
    }

    public void AppendLeft(object value)
    {
        if (MaxLength == 0)
        {
            return;
        }

        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            // Same charge reuse as Append: eviction keeps the node count flat.
            _items.RemoveLast();
            _items.AddFirst(value);
            return;
        }

        ReserveNode();
        _items.AddFirst(value);
    }

    public object Pop()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("pop from an empty deque");
        }

        var value = _items.Last.RequireNotNull().Value;
        _items.RemoveLast();
        ReleaseNode();
        return value;
    }

    public object PopLeft()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("pop from an empty deque");
        }

        var value = _items.First.RequireNotNull().Value;
        _items.RemoveFirst();
        ReleaseNode();
        return value;
    }

    public void Extend(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            Append(value);
        }
    }

    public void ExtendLeft(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            AppendLeft(value);
        }
    }

    public int CountValue(object candidate) => _items.Count(item => PyEquality.AreEqual(item, candidate));

    public int IndexOf(object candidate, int start, int stop)
    {
        var index = 0;
        foreach (var item in _items)
        {
            if (index >= start && index < stop && PyEquality.AreEqual(item, candidate))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public void Insert(int index, object value)
    {
        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            throw new InvalidOperationException("deque already at its maximum size");
        }

        if (index <= 0)
        {
            ReserveNode();
            _items.AddFirst(value);
            return;
        }

        if (index >= _items.Count)
        {
            ReserveNode();
            _items.AddLast(value);
            return;
        }

        ReserveNode();
        _items.AddBefore(GetNodeAt(index), value);
    }

    public bool RemoveValue(object candidate)
    {
        var current = _items.First;
        while (current is not null)
        {
            if (PyEquality.AreEqual(current.Value, candidate))
            {
                _items.Remove(current);
                ReleaseNode();
                return true;
            }

            current = current.Next;
        }

        return false;
    }

    public void Reverse()
    {
        // Swap values in place: the node count never changes, so no charging
        // is needed and no snapshot array escapes ownership.
        if (_items.Count <= 1)
        {
            return;
        }

        var forward = _items.First.RequireNotNull();
        var backward = _items.Last.RequireNotNull();
        for (var i = 0; i < _items.Count / 2; i++)
        {
            (forward.Value, backward.Value) = (backward.Value, forward.Value);
            forward = forward.Next.RequireNotNull();
            backward = backward.Previous.RequireNotNull();
        }
    }

    public void Rotate(BigInteger offset)
    {
        if (_items.Count == 0 || offset == 0)
        {
            return;
        }

        var steps = (int)(offset % _items.Count);
        if (steps > _items.Count / 2)
        {
            steps -= _items.Count;
        }
        else if (steps < -_items.Count / 2)
        {
            steps += _items.Count;
        }

        if (steps > 0)
        {
            // Rotate reuses nodes: pop and re-add balance exactly, so keep the
            // committed charge flat instead of releasing and re-reserving.
            for (var i = 0; i < steps; i++)
            {
                var moved = _items.Last.RequireNotNull().Value;
                _items.RemoveLast();
                _items.AddFirst(moved);
            }
        }
        else if (steps < 0)
        {
            for (var i = 0; i > steps; i--)
            {
                var moved = _items.First.RequireNotNull().Value;
                _items.RemoveFirst();
                _items.AddLast(moved);
            }
        }
    }

    public void Clear()
    {
        if (_memoryGovernor is not null && _committedNodeBytes > 0)
        {
            _memoryGovernor.Release(_committedNodeBytes);
            _committedNodeBytes = 0;
        }

        _items.Clear();
    }

    public object GetItem(int index) => GetNodeAt(index).Value;

    public object GetIndex(int index) => GetItem(index);

    public object GetSlice(IEnumerable<int> indices)
    {
        // Snapshot the source under a transient reservation when governed so
        // peak scratch is bounded; the retained slice charges durably below.
        var governor = _memoryGovernor;
        var span = _allocationSpan;
        object[] source;
        if (governor is null)
        {
            source = _items.ToArray();
        }
        else
        {
            using var scratch = governor.ReserveTemporary(checked(24L + (8L * _items.Count)), span);
            source = _items.ToArray();
        }

        IEnumerable<object> EnumerateSliceItems()
        {
            foreach (var index in indices)
            {
                yield return source[index];
            }
        }

        return governor is null
            ? new PyDeque(EnumerateSliceItems(), MaxLength)
            : new PyDeque(EnumerateSliceItems(), MaxLength, governor, span);
    }

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null
        ? new PyDeque(items, MaxLength)
        : new PyDeque(items, MaxLength, _memoryGovernor, _allocationSpan);

    public void SetItem(int index, object value) => SetIndex(index, value);

    public void SetIndex(int index, object value)
    {
        var node = GetNodeAt(index);
        node.Value = value;
    }

    public void RemoveAt(int index)
    {
        var node = GetNodeAt(index);
        _items.Remove(node);
        ReleaseNode();
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => _items;

    public PyString RenderPython(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedReprValues("deque([", _items, suffix, context);
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedReprValues("deque([", _items, suffix, context);
    }

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ReserveNode()
    {
        if (_memoryGovernor is null)
        {
            return;
        }

        _memoryGovernor.Reserve(DequeNodeBytes, _allocationSpan);
        _memoryGovernor.Commit(DequeNodeBytes);
        _committedNodeBytes += DequeNodeBytes;
    }

    private void ReleaseNode()
    {
        // Attached prepopulated nodes were never charged (MG03 gap), so only
        // release when a matching reservation is actually held.
        if (_memoryGovernor is null || _committedNodeBytes < DequeNodeBytes)
        {
            return;
        }

        _memoryGovernor.Release(DequeNodeBytes);
        _committedNodeBytes -= DequeNodeBytes;
    }

    private LinkedListNode<object> GetNodeAt(int index)
    {
        if (index < _items.Count / 2)
        {
            var forward = _items.First.RequireNotNull();
            for (var i = 0; i < index; i++)
            {
                forward = forward.Next.RequireNotNull();
            }

            return forward;
        }

        var backward = _items.Last.RequireNotNull();
        for (var i = _items.Count - 1; i > index; i--)
        {
            backward = backward.Previous.RequireNotNull();
        }

        return backward;
    }

}
