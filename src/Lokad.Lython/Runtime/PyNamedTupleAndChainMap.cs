using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyNamedTupleType : LythonRuntime.ICallable, IPyRenderableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes, INamedRuntimeCallable
{
    private readonly string _typeName;
    private readonly string[] _fieldNames;
    private readonly object[] _defaults;
    private readonly MemoryGovernor? _governor;
    private readonly LythonSourceSpan? _allocationSpan;
    private readonly PyString _nameValue;
    private readonly PyTuple _fieldsTuple;
    private PyDict? _fieldDefaults;

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames) : this(typeName, fieldNames, null) { }

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames, IEnumerable<object>? defaults)
        : this(typeName, fieldNames, defaults, null, null)
    {
    }

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames, IEnumerable<object>? defaults, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        _typeName = typeName;
        _fieldNames = fieldNames.ToArray();
        _defaults = defaults?.ToArray() ?? [];
        _governor = governor;
        _allocationSpan = span;
        _nameValue = governor is null ? PyString.FromString(typeName) : PyString.FromString(typeName, governor, span);
        _fieldsTuple = governor is null
            ? PyTuple.FromOwnedArray(_fieldNames.Select(PyString.FromString).Cast<object>().ToArray())
            : PyTuple.FromOwnedArray(_fieldNames.Select(name => PyString.FromString(name, governor, span)).Cast<object>().ToArray(), governor, span);
    }

    public string Name => _typeName;

    internal object? ModuleName { get; set; }

    internal LythonRuntime.FunctionNewMethod GetNewSlot() => NewSlot ??= new LythonRuntime.FunctionNewMethod(this, _typeName);

    internal LythonRuntime.FunctionNewMethod? NewSlot { get; set; }

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
            if (argument.IsPositional)
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

            var fieldIndex = IndexOfField(argument.KeywordName);
            if (fieldIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);
            }

            if (assigned[fieldIndex])
            {
                throw new LythonRuntimeException("TypeError", $"{_typeName}(...) got multiple values for argument '{argument.KeywordName}'.", span);
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

        return new PyNamedTupleObject(this, values, context.MemoryGovernor, span);
    }

    public PyNamedTupleObject CreateFromValues(IEnumerable<object> values, LythonSourceSpan? span)
        => CreateFromValues(values, null, span);

    public PyNamedTupleObject CreateFromValues(IEnumerable<object> values, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var materialized = values.ToArray();
        if (materialized.Length != _fieldNames.Length)
        {
            throw new LythonRuntimeException("TypeError", $"{_typeName}._make(iterable) expects {_fieldNames.Length} values.", span);
        }

        return governor is null
            ? new PyNamedTupleObject(this, materialized)
            : new PyNamedTupleObject(this, materialized, governor, span);
    }

    public bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        // Tuple sequence members live on the run tuple constructor, so
        // reads alias its cached descriptors like CPython.
        if ((name == "index" || name == "count") &&
            context.TryGetBuiltin("tuple", out var tupleType) &&
            tupleType is IPyContextualDynamicAttributes tupleAttributes &&
            tupleAttributes.TryGetMember(name, context, span, out value))
        {
            return true;
        }

        return TryGetMember(name, out value);
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "__module__" && ModuleName is not null)
        {
            value = ModuleName;
            return true;
        }

        value = name switch
        {
            "__name__" => _nameValue,
            "__new__" => GetNewSlot(),
            "_fields" => _fieldsTuple,
            "_field_defaults" => GetFieldDefaults(),
            "_make" => new BoundNamedTupleMake(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
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

    private PyDict GetFieldDefaults()
    {
        if (_fieldDefaults is not null)
        {
            return _fieldDefaults;
        }

        var dict = _governor is null ? new PyDict() : new PyDict(_governor, _allocationSpan);
        var start = _fieldNames.Length - _defaults.Length;
        for (var i = 0; i < _defaults.Length; i++)
        {
            dict.SetItem(
                _governor is null ? PyString.FromString(_fieldNames[start + i]) : PyString.FromString(_fieldNames[start + i], _governor, _allocationSpan),
                _defaults[i]);
        }

        _fieldDefaults = dict;
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
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", $"{_type.Name}._make(iterable) expects one iterable argument.", span);
            }

            return _type.CreateFromValues(LythonRuntime.ToSequence(arguments[0].Value, span, context), context.MemoryGovernor, span);
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

internal sealed class PyNamedTupleObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPyHashableValue, IPyGovernedValue
{
    private readonly PyNamedTupleType _type;
    private readonly object[] _values;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;

    public PyNamedTupleObject(PyNamedTupleType type, object[] values)
    {
        _type = type;
        _values = [.. values];
    }

    // Guest-constructed instances own their backing array at the tuple slot
    // rate; engine-owned tuples without a governor stay free.
    public PyNamedTupleObject(PyNamedTupleType type, object[] values, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        var backingBytes = PyTuple.EstimateApproximateBytes(values.Length);
        governor.Reserve(backingBytes, allocationSpan);
        governor.Commit(backingBytes);
        _type = type;
        _values = [.. values];
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public PyNamedTupleType Type => _type;

    public int Count => _values.Length;

    public int Length => _values.Length;

    public object this[int index] => _values[index];

    public object GetItem(int index) => _values[index];

    public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

    public object GetIndex(int index) => _values[index];

    public object GetSlice(IEnumerable<int> indices) => PyTupleLike.CreateSlice(_values, indices);

    public bool IsTruthy() => _values.Length != 0;

    public IEnumerable<object> Iterate() => _values;

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        var fieldIndex = _type.IndexOfField(name);
        if (fieldIndex >= 0)
        {
            value = _values[fieldIndex];
            return true;
        }

        if (name == "__new__" && _type.TryGetMember(name, out value))
        {
            return true;
        }

        if (name == "__module__" && _type.TryGetMember(name, out value))
        {
            return true;
        }

        if ((name == "_field_defaults" || name == "_make") && _type.TryGetMember(name, out value))
        {
            return true;
        }

        value = name switch
        {
            "__class__" => _type,
            "_fields" => new PyTuple(_type.FieldNames.Select(PyString.FromString).Cast<object>()),
            "_field_defaults" => _type.TryGetMember("_field_defaults", out var fieldDefaults)
                ? fieldDefaults
                : throw new InvalidOperationException("Named tuple type member '_field_defaults' is missing."),
            "_asdict" => new BoundNamedTupleAsDict(this),
            "_replace" => new BoundNamedTupleReplace(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }
    public int GetPyHashCode() => PyTupleLike.ComputeHashCode(_values);

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
                if (argument.IsPositional)
                {
                    throw new LythonRuntimeException("TypeError", $"{_owner._type.Name}._replace(...) expects keyword arguments.", span);
                }

                var fieldIndex = _owner._type.IndexOfField(argument.KeywordName);
                if (fieldIndex < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{_owner._type.Name}._replace(...) got unexpected field name '{argument.KeywordName}'.", span);
                }

                values[fieldIndex] = argument.Value;
            }

            return new PyNamedTupleObject(_owner._type, values, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_owner._type.Name}._replace>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

internal sealed class PyChainMap : IMutablePySubscriptableValue, IDeletablePySubscriptableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPySizedValue
{
    private static readonly LythonCallableSignature GetCallSignature = LythonCallableSignature.Create(
        "ChainMap.get",
        ["key", "default"],
        requiredCount: 1);

    private readonly List<PyDict> _maps;

    public PyChainMap(IEnumerable<PyDict> maps)
    {
        _maps = maps.ToList();
        if (_maps.Count == 0)
        {
            _maps.Add(new PyDict());
        }
    }

    public int Count => CountMergedKeys();

    public int Length => Count;

    public bool IsTruthy() => _maps.Any(map => map.Count != 0);

    // Generic for-loop/list()/any() iteration builds the same merged list as
    // the key views beside no governed copy of its own; hold the merge
    // estimate over the eager build (released before streaming, so slow
    // consumers retain a documented residual while nothing new allocates).
    public IEnumerable<object> Iterate()
    {
        using var scratch = _maps[0].OwnerMemoryGovernor?.ReserveTemporary(EstimateMergeScratchBytes(), null);
        return BuildMergedKeys();
    }

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

        throw RuntimeErrors.MissingKey(index, span);
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

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
            "maps" => OwnedMapsList(),
            "parents" => CreateParents(),
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

    private PyChainMap CreateParents()
    {
        // The fallback map behind a single-map ChainMap borrows its governor
        // from the visible map so parents writes stay charged; a fully
        // ungoverned parent stays free like its maps.
        if (_maps.Count > 1)
        {
            return new PyChainMap(_maps.Skip(1));
        }

        var governor = _maps[0].OwnerMemoryGovernor;
        return new PyChainMap(governor is null ? [new PyDict()] : [new PyDict(governor)]);
    }

    // The maps view is a fresh list per access; charge its backing through the
    // visible map governor like the parents fallback so retained views stay owned.
    private PyList OwnedMapsList()
    {
        var governor = _maps[0].OwnerMemoryGovernor;
        return governor is null
            ? new PyList(_maps.Cast<object>())
            : new PyList(_maps.Cast<object>(), governor, null);
    }

    public PyString RenderPython(PyRenderingContext context)
            => PyRendering.JoinRenderedSequence("ChainMap(", new RenderedMaps(_maps, context), ")", context);

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

    // Merged views visit every key of every map beside the governed copy;
    // reserve the transient list plus dedup-set peak before building.
    internal long EstimateMergeScratchBytes()
    {
        var total = 0L;
        foreach (var map in _maps)
        {
            total = checked(total + map.Count);
        }

        return checked(total * 64L);
    }

    private IReadOnlyList<object> BuildMergedKeys()
    {
        var keys = new List<object>();
        var seen = new HashSet<object>(PyValueComparer.Instance);
        foreach (var map in _maps)
        {
            foreach (var key in map.Keys)
            {
                if (seen.Add(key))
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
        var seen = new HashSet<object>(PyValueComparer.Instance);
        foreach (var map in _maps)
        {
            foreach (var pair in map)
            {
                if (seen.Add(pair.Key))
                {
                    items.Add(pair);
                }
            }
        }

        return items;
    }

    private int CountMergedKeys()
    {
        var keys = new HashSet<object>(PyValueComparer.Instance);
        foreach (var map in _maps)
        {
            keys.UnionWith(map.Keys);
        }

        return keys.Count;
    }

    private static PyDict ExpectMap(object value, LythonSourceSpan span, MemoryGovernor governor)
        => value switch
        {
            PyDict dict => dict,
            PyDefaultDict defaultDict => ToPyDict(defaultDict, governor, span),
            _ => throw new LythonRuntimeException("TypeError", "ChainMap maps must be dictionaries.", span)
        };

    internal static IReadOnlyList<PyDict> NormalizeMaps(IEnumerable<object> values, LythonSourceSpan span, MemoryGovernor governor)
        => values.Select(value => ExpectMap(value, span, governor)).ToArray();

    private static PyDict ToPyDict(PyDefaultDict defaultDict, MemoryGovernor governor, LythonSourceSpan span)
    {
        var dict = new PyDict(governor, span);
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
            var bound = CallBinder.BindNamedArguments(arguments, span, GetCallSignature, PythonCallableKind.Method);
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

            using var scratch = context.MemoryGovernor.ReserveTemporary(_owner.EstimateMergeScratchBytes(), span);
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

            using var scratch = context.MemoryGovernor.ReserveTemporary(_owner.EstimateMergeScratchBytes(), span);
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

            using var scratch = context.MemoryGovernor.ReserveTemporary(_owner.EstimateMergeScratchBytes(), span);
            return new PyList(
                _owner.BuildMergedItems().Select(pair => PyTuple.FromOwnedArray([pair.Key, pair.Value], context.MemoryGovernor, span)),
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
                arguments.Length == 0 ? new PyDict(context.MemoryGovernor, span) : ExpectMap(arguments[0].Value, span, context.MemoryGovernor)
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
