using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue
{
    private readonly PyDict _items;

    public PyDefaultDict(object? defaultFactory = null)
    {
        DefaultFactory = defaultFactory;
        _items = new PyDict();
    }

    public PyDefaultDict(object? defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        DefaultFactory = defaultFactory;
        _items = new PyDict(governor, allocationSpan);
    }

    public PyDefaultDict(object? defaultFactory, PyDict items)
    {
        DefaultFactory = defaultFactory;
        _items = new PyDict(items);
    }

    public object? DefaultFactory { get; }

    public int Count => _items.Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    public bool TryGetValue(object key, out object value) => _items.TryGetValue(key, out value);

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

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
        => _items.AttachMemoryGovernor(governor, allocationSpan);

    public bool Remove(object key) => _items.Remove(key);

    public void Clear() => _items.Clear();

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
}

internal sealed class PyCounter : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue
{
    private readonly PyDict _items;

    public PyCounter()
    {
        _items = new PyDict();
    }

    public PyCounter(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        _items = new PyDict(governor, allocationSpan);
    }

    public PyCounter(PyCounter other)
    {
        _items = new PyDict(other._items);
    }

    public PyCounter(PyCounter other, MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        _items = new PyDict(other._items, governor, allocationSpan);
    }

    public int Count => _items.Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    public object GetCount(object key) => _items.TryGetValue(key, out var value) ? value : BigInteger.Zero;

    public bool TryGetValue(object key, out object value) => _items.TryGetValue(key, out value);

    public void SetItem(object key, object value) => _items.SetItem(key, value);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
        => _items.AttachMemoryGovernor(governor, allocationSpan);

    public bool Remove(object key) => _items.Remove(key);

    public void Clear() => _items.Clear();

    public void Increment(object key, BigInteger delta)
    {
        if (_items.TryGetValue(key, out var value) && Numbers.PyNumberOps.TryAsInteger(value, out var current))
        {
            _items.SetItem(key, current + delta);
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

    public PyDeque()
    {
    }

    public PyDeque(IEnumerable<object> items)
    {
        foreach (var item in items)
        {
            _items.AddLast(item);
        }
    }

    public int Count => _items.Count;

    public int Length => Count;

    public object this[int index] => GetItem(index);

    public void Append(object value) => _items.AddLast(value);

    public void AppendLeft(object value) => _items.AddFirst(value);

    public object Pop()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("pop from an empty deque");
        }

        var value = _items.Last!.Value;
        _items.RemoveLast();
        return value;
    }

    public object PopLeft()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("pop from an empty deque");
        }

        var value = _items.First!.Value;
        _items.RemoveFirst();
        return value;
    }

    public void Extend(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            _items.AddLast(value);
        }
    }

    public void ExtendLeft(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            _items.AddFirst(value);
        }
    }

    public int CountValue(object candidate) => _items.Count(item => PyEquality.AreEqual(item, candidate));

    public void Reverse()
    {
        var items = _items.ToArray();
        _items.Clear();
        for (var i = items.Length - 1; i >= 0; i--)
        {
            _items.AddLast(items[i]);
        }
    }

    public void Rotate(int offset)
    {
        if (_items.Count == 0 || offset == 0)
        {
            return;
        }

        var steps = offset % _items.Count;
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

    public object GetItem(int index) => _items.ElementAt(index);

    public object GetIndex(int index) => GetItem(index);

    public object GetSlice(IEnumerable<int> indices)
    {
        var items = new List<object>();
        foreach (var index in indices)
        {
            items.Add(GetItem(index));
        }

        return new PyDeque(items);
    }

    public object CreateSlice(IEnumerable<object> items) => new PyDeque(items);

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
        => PyRendering.JoinRenderedSequence("deque([", new RenderedItems(_items, context, interpolated: false), "])");

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedSequence("deque([", new RenderedItems(_items, context, interpolated: true), "])");

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private LinkedListNode<object> GetNodeAt(int index)
    {
        var current = _items.First!;
        for (var i = 0; i < index; i++)
        {
            current = current.Next!;
        }

        return current;
    }

    private sealed class RenderedItems : IEnumerable<PyString>
    {
        private readonly IEnumerable<object> _items;
        private readonly PyRenderingContext _context;
        private readonly bool _interpolated;

        public RenderedItems(IEnumerable<object> items, PyRenderingContext context, bool interpolated)
        {
            _items = items;
            _context = context;
            _interpolated = interpolated;
        }

        public IEnumerator<PyString> GetEnumerator()
        {
            foreach (var item in _items)
            {
                yield return _interpolated ? PyRendering.ToInterpolatedPyString(item, _context) : PyRendering.ToPythonPyString(item, _context);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
