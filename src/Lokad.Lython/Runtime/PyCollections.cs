using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyDynamicAttributes
{
    private readonly PyDict _items;

    public PyDefaultDict(object? defaultFactory = null)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict();
    }

    public PyDefaultDict(object? defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
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

    public bool TryGetMember(string name, out object value)
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

    public BigInteger GetIntegerCountOrZero(object key, LythonSourceSpan span)
    {
        if (!_items.TryGetValue(key, out var value))
        {
            return BigInteger.Zero;
        }

        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "Counter counts must be integers.", span);
        }

        return integer;
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

    public PyDeque(int? maxLength = null)
    {
        MaxLength = maxLength;
    }

    public PyDeque(IEnumerable<object> items, int? maxLength = null)
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

        return new PyDeque(items, MaxLength);
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
        if (MaxLength is null)
        {
            return PyRendering.JoinRenderedSequence("deque([", new RenderedItems(_items, context, interpolated: false), "])");
        }

        var items = PyRendering.JoinRenderedSequence("", new RenderedItems(_items, context, interpolated: false), "");
        return PyString.FromString($"deque([{items.AsString()}], maxlen={MaxLength.Value})");
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        if (MaxLength is null)
        {
            return PyRendering.JoinRenderedSequence("deque([", new RenderedItems(_items, context, interpolated: true), "])");
        }

        var items = PyRendering.JoinRenderedSequence("", new RenderedItems(_items, context, interpolated: true), "");
        return PyString.FromString($"deque([{items.AsString()}], maxlen={MaxLength.Value})");
    }

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

internal sealed class PyNamedTupleType : LythonRuntime.ICallable, IPyRenderableValue, IPyDynamicAttributes, INamedRuntimeCallable
{
    private readonly string _typeName;
    private readonly string[] _fieldNames;
    private readonly object[] _defaults;

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames, IEnumerable<object>? defaults = null)
    {
        _typeName = typeName;
        _fieldNames = fieldNames.ToArray();
        _defaults = defaults?.ToArray() ?? [];
    }

    public string Name => _typeName;

    public IReadOnlyList<string> FieldNames => _fieldNames;

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var values = new object[_fieldNames.Length];
        var assigned = new bool[_fieldNames.Length];
        var required = _fieldNames.Length - _defaults.Length;
        for (var i = 0; i < _fieldNames.Length; i++)
        {
            values[i] = i >= required ? _defaults[i - required] : Missing.Value;
        }

        var positionalIndex = 0;
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positionalIndex >= _fieldNames.Length)
                {
                    throw new LythonRuntimeException("TypeError", $"{_typeName}(...) received too many positional arguments.", span);
                }

                if (assigned[positionalIndex])
                {
                    throw new LythonRuntimeException("TypeError", $"{_typeName}(...) got multiple values for argument '{_fieldNames[positionalIndex]}'.", span);
                }

                values[positionalIndex++] = argument.Value;
                assigned[positionalIndex - 1] = true;
                continue;
            }

            var fieldIndex = IndexOfField(argument.Name);
            if (fieldIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}(...) received an unexpected keyword argument '{argument.Name}'.", span);
            }

            if (assigned[fieldIndex])
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}(...) got multiple values for argument '{argument.Name}'.", span);
            }

            values[fieldIndex] = argument.Value;
            assigned[fieldIndex] = true;
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (ReferenceEquals(values[i], Missing.Value))
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}(...) missing required argument '{_fieldNames[i]}'.", span);
            }
        }

        return new PyNamedTupleObject(this, values);
    }

    public PyNamedTupleObject CreateFromValues(IEnumerable<object> values, LythonSourceSpan span)
    {
        var materialized = values.ToArray();
        if (materialized.Length != _fieldNames.Length)
        {
            throw new LythonRuntimeException("TypeError", $"{_typeName}._make(iterable) expects {_fieldNames.Length} values.", span);
        }

        return new PyNamedTupleObject(this, materialized);
    }

    public bool TryGetMember(string name, out object value)
    {
        value = name switch
        {
            "__name__" => PyString.FromString(_typeName),
            "_fields" => new PyTuple(_fieldNames.Select(PyString.FromString).Cast<object>()),
            "_field_defaults" => BuildFieldDefaults(),
            "_make" => new BoundNamedTupleMake(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"<class '{_typeName}'>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    internal int IndexOfField(string fieldName)
    {
        for (var i = 0; i < _fieldNames.Length; i++)
        {
            if (string.Equals(_fieldNames[i], fieldName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private PyDict BuildFieldDefaults()
    {
        var dict = new PyDict();
        var start = _fieldNames.Length - _defaults.Length;
        for (var i = 0; i < _defaults.Length; i++)
        {
            dict.SetItem(PyString.FromString(_fieldNames[start + i]), _defaults[i]);
        }

        return dict;
    }

    private sealed class BoundNamedTupleMake : LythonRuntime.ICallable, IPyRenderableValue
    {
        private readonly PyNamedTupleType _type;

        public BoundNamedTupleMake(PyNamedTupleType type)
        {
            _type = type;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", $"{_type.Name}._make(iterable) expects one iterable argument.", span);
            }

            return _type.CreateFromValues(LythonRuntime.ToSequence(arguments[0].Value, span), span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_type.Name}._make>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static class Missing
    {
        public static readonly object Value = new();
    }
}

internal sealed class PyNamedTupleObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPyHashableValue
{
    private readonly PyNamedTupleType _type;
    private readonly object[] _values;

    public PyNamedTupleObject(PyNamedTupleType type, object[] values)
    {
        _type = type;
        _values = [.. values];
    }

    public PyNamedTupleType Type => _type;

    public int Count => _values.Length;

    public int Length => _values.Length;

    public object this[int index] => _values[index];

    public object GetItem(int index) => _values[index];

    public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

    public object GetIndex(int index) => _values[index];

    public object GetSlice(IEnumerable<int> indices) => new PyTuple(indices.Select(index => _values[index]));

    public bool IsTruthy() => _values.Length != 0;

    public IEnumerable<object> Iterate() => _values;

    public bool TryGetMember(string name, out object value)
    {
        var fieldIndex = _type.IndexOfField(name);
        if (fieldIndex >= 0)
        {
            value = _values[fieldIndex];
            return true;
        }

        value = name switch
        {
            "_fields" => new PyTuple(_type.FieldNames.Select(PyString.FromString).Cast<object>()),
            "_field_defaults" => GetTypeMember("_field_defaults"),
            "_asdict" => new BoundNamedTupleAsDict(this),
            "_replace" => new BoundNamedTupleReplace(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public int GetPyHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _values)
        {
            hash.Add(PyValueComparer.Instance.GetHashCode(value));
        }

        return hash.ToHashCode();
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        var parts = _type.FieldNames
            .Select((fieldName, index) => $"{fieldName}={PyRendering.ToPythonString(_values[index], context)}");
        return PyString.FromString($"{_type.Name}({string.Join(", ", parts)})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal object[] ToArray() => [.. _values];

    private object GetTypeMember(string name)
    {
        _ = _type.TryGetMember(name, out var value);
        return value;
    }

    private sealed class BoundNamedTupleAsDict : LythonRuntime.ICallable, IPyRenderableValue
    {
        private readonly PyNamedTupleObject _owner;

        public BoundNamedTupleAsDict(PyNamedTupleObject owner)
        {
            _owner = owner;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"{_owner._type.Name}._asdict() expects no arguments.", span);
            }

            var dict = new PyDict(context.MemoryGovernor, span);
            for (var i = 0; i < _owner._values.Length; i++)
            {
                dict.SetItem(PyString.FromString(_owner._type.FieldNames[i]), _owner._values[i]);
            }

            return dict;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_owner._type.Name}._asdict>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class BoundNamedTupleReplace : LythonRuntime.ICallable, IPyRenderableValue
    {
        private readonly PyNamedTupleObject _owner;

        public BoundNamedTupleReplace(PyNamedTupleObject owner)
        {
            _owner = owner;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var values = _owner.ToArray();
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", $"{_owner._type.Name}._replace(...) expects keyword arguments.", span);
                }

                var fieldIndex = _owner._type.IndexOfField(argument.Name);
                if (fieldIndex < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{_owner._type.Name}._replace(...) got unexpected field name '{argument.Name}'.", span);
                }

                values[fieldIndex] = argument.Value;
            }

            return new PyNamedTupleObject(_owner._type, values);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_owner._type.Name}._replace>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

internal sealed class PyChainMap : IMutablePySubscriptableValue, IDeletablePySubscriptableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes
{
    private readonly List<PyDict> _maps;

    public PyChainMap(IEnumerable<PyDict> maps)
    {
        _maps = maps.ToList();
        if (_maps.Count == 0)
        {
            _maps.Add(new PyDict());
        }
    }

    public int Count => BuildMergedKeys().Count;

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => BuildMergedKeys();

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        foreach (var map in _maps)
        {
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        throw new LythonRuntimeException("KeyError", "Key was not found.", span);
    }

    public void SetSubscript(object index, object value, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        _maps[0].SetItem(key, value);
    }

    public void DeleteSubscript(object index, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        if (!_maps[0].Remove(key))
        {
            throw new LythonRuntimeException("KeyError", "Key not found in the first ChainMap mapping.", span);
        }
    }

    public bool TryGetMember(string name, out object value)
    {
        value = name switch
        {
            "maps" => new PyList(_maps.Cast<object>()),
            "parents" => new PyChainMap(_maps.Skip(1)),
            "get" => new BoundChainMapGet(this),
            "keys" => new BoundChainMapKeys(this),
            "values" => new BoundChainMapValues(this),
            "items" => new BoundChainMapItems(this),
            "new_child" => new BoundChainMapNewChild(this),
            "copy" => new BoundChainMapCopy(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedSequence("ChainMap(", new RenderedMaps(_maps, context), ")");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private object GetOrDefault(object key, object defaultValue)
    {
        foreach (var map in _maps)
        {
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return defaultValue;
    }

    private IReadOnlyList<object> BuildMergedKeys()
    {
        var keys = new List<object>();
        foreach (var map in _maps)
        {
            foreach (var key in map.Keys)
            {
                if (!keys.Any(existing => PyEquality.AreEqual(existing, key)))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    private IReadOnlyList<KeyValuePair<object, object>> BuildMergedItems()
    {
        var items = new List<KeyValuePair<object, object>>();
        foreach (var key in BuildMergedKeys())
        {
            items.Add(new KeyValuePair<object, object>(key, GetOrDefault(key, PyNone.Instance)));
        }

        return items;
    }

    private static PyDict ExpectMap(object value, LythonSourceSpan span)
        => value switch
        {
            PyDict dict => dict,
            PyDefaultDict defaultDict => ToPyDict(defaultDict),
            _ => throw new LythonRuntimeException("TypeError", "ChainMap maps must be dictionaries.", span)
        };

    internal static IReadOnlyList<PyDict> NormalizeMaps(IEnumerable<object> values, LythonSourceSpan span)
        => values.Select(value => ExpectMap(value, span)).ToArray();

    private static PyDict ToPyDict(PyDefaultDict defaultDict)
    {
        var dict = new PyDict();
        foreach (var pair in defaultDict)
        {
            dict.SetItem(pair.Key, pair.Value);
        }

        return dict;
    }

    private sealed class BoundChainMapGet : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapGet(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, "ChainMap.get", "Method", ["key", "default"], requiredCount: 1);
            var key = LythonRuntime.ValidateDictionaryKey(bound[0], span, context.MemoryGovernor);
            return _owner.GetOrDefault(key, bound.Length == 2 ? bound[1] : PyNone.Instance);
        }
    }

    private sealed class BoundChainMapKeys : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapKeys(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.keys() expects no arguments.", span);
            }

            return new PyList(_owner.BuildMergedKeys(), context.MemoryGovernor, span);
        }
    }

    private sealed class BoundChainMapValues : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapValues(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.values() expects no arguments.", span);
            }

            return new PyList(_owner.BuildMergedItems().Select(pair => pair.Value), context.MemoryGovernor, span);
        }
    }

    private sealed class BoundChainMapItems : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapItems(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.items() expects no arguments.", span);
            }

            return new PyList(
                _owner.BuildMergedItems().Select(pair => new PyTuple([pair.Key, pair.Value], context.MemoryGovernor, span)),
                context.MemoryGovernor,
                span);
        }
    }

    private sealed class BoundChainMapNewChild : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapNewChild(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.new_child([m]) expects zero or one mapping.", span);
            }

            var maps = new List<PyDict>
            {
                arguments.Length == 0 ? new PyDict(context.MemoryGovernor, span) : ExpectMap(arguments[0].Value, span)
            };
            maps.AddRange(_owner._maps);
            return new PyChainMap(maps);
        }
    }

    private sealed class BoundChainMapCopy : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapCopy(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.copy() expects no arguments.", span);
            }

            var maps = new List<PyDict> { new(_owner._maps[0], context.MemoryGovernor, span) };
            maps.AddRange(_owner._maps.Skip(1));
            return new PyChainMap(maps);
        }
    }

    private sealed class RenderedMaps : IEnumerable<PyString>
    {
        private readonly IEnumerable<PyDict> _maps;
        private readonly PyRenderingContext _context;

        public RenderedMaps(IEnumerable<PyDict> maps, PyRenderingContext context)
        {
            _maps = maps;
            _context = context;
        }

        public IEnumerator<PyString> GetEnumerator()
        {
            foreach (var map in _maps)
            {
                yield return PyRendering.ToPythonPyString(map, _context);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
