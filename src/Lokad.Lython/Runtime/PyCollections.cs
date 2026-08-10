using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyDynamicAttributes, IPySizedValue
{
    private readonly PyDict _items;

    public PyDefaultDict() : this(null) { }

    public PyDefaultDict(object? defaultFactory)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict();
    }

    public PyDefaultDict(object? defaultFactory, MemoryGovernor governor) : this(defaultFactory, governor, null) { }

    public PyDefaultDict(object? defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(governor, allocationSpan);
    }

    public PyDefaultDict(object? defaultFactory, PyDict items)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(items);
    }

    public object? DefaultFactory { get; private set; }

    public int Count => _items.Count;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

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
            null or PyNone => throw new LythonRuntimeException("KeyError", "Key was not found.", span),
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

    public void Clear() => _items.Clear();

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "default_factory")
        {
            value = DefaultFactory ?? PyNone.Instance;
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
            null => PyStringOps.NoneLiteral,
            PyNone => PyStringOps.NoneLiteral,
            _ => PyRendering.ToPythonPyString(DefaultFactory, context)
        };
        return PyRendering.JoinRenderedSequence(
            "defaultdict(",
            [factory, PyRendering.JoinRenderedDictionary(_items, context, interpolated: false)],
            ")");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static object? ValidateDefaultFactory(object? value)
        => value switch
        {
            null or PyNone => value,
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
        _items = new PyDict(governor, allocationSpan);
    }

    public PyCounter(PyCounter other)
    {
        _items = new PyDict(other._items);
    }

    public PyCounter(PyCounter other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyCounter(PyCounter other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _items = new PyDict(other._items, governor, allocationSpan);
    }

    public int Count => _items.Count;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

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
            _items.SetItem(key, LythonRuntime.AddCounterCounts(value, delta, span));
            return;
        }

        _items.SetItem(key, delta);
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedSequence("Counter(", [PyRendering.JoinRenderedDictionary(_items, context, interpolated: false)], ")");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class PyDeque : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
{
    private readonly LinkedList<object> _items = [];

    public PyDeque() : this((int?)null) { }

    public PyDeque(int? maxLength)
    {
        MaxLength = maxLength;
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

    public int? MaxLength { get; }

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
            _items.RemoveFirst();
        }

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
            _items.RemoveLast();
        }

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
            _items.AddFirst(value);
            return;
        }

        if (index >= _items.Count)
        {
            _items.AddLast(value);
            return;
        }

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
                return true;
            }

            current = current.Next;
        }

        return false;
    }

    public void Reverse()
    {
        var items = _items.ToArray();
        _items.Clear();
        for (var i = items.Length - 1; i >= 0; i--)
        {
            _items.AddLast(items[i]);
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
            for (var i = 0; i < steps; i++)
            {
                _items.AddFirst(Pop());
            }
        }
        else if (steps < 0)
        {
            for (var i = 0; i > steps; i--)
            {
                _items.AddLast(PopLeft());
            }
        }
    }

    public void Clear() => _items.Clear();

    public object GetItem(int index) => GetNodeAt(index).Value;

    public object GetIndex(int index) => GetItem(index);

    public object GetSlice(IEnumerable<int> indices)
    {
        var source = _items.ToArray();
        return new PyDeque(EnumerateSliceItems(), MaxLength);

        IEnumerable<object> EnumerateSliceItems()
        {
            foreach (var index in indices)
            {
                yield return source[index];
            }
        }
    }

    public object CreateSlice(IEnumerable<object> items) => new PyDeque(items, MaxLength);

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
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => _items;

    public PyString RenderPython(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedValues("deque([", _items, suffix, context, interpolated: false);
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedValues("deque([", _items, suffix, context, interpolated: true);
    }

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
