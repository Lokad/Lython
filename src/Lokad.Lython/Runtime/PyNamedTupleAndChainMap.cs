using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyNamedTupleType : LythonRuntime.ICallable, IPyRenderableValue, IPyDynamicAttributes, INamedRuntimeCallable
{
    private readonly string _typeName;
    private readonly string[] _fieldNames;
    private readonly object[] _defaults;

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames) : this(typeName, fieldNames, null) { }

    public PyNamedTupleType(string typeName, IEnumerable<string> fieldNames, IEnumerable<object>? defaults)
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

        return new PyNamedTupleObject(this, values);
    }

    public PyNamedTupleObject CreateFromValues(IEnumerable<object> values, LythonSourceSpan? span)
    {
        var materialized = values.ToArray();
        if (materialized.Length != _fieldNames.Length)
        {
            throw new LythonRuntimeException("TypeError", $"{_typeName}._make(iterable) expects {_fieldNames.Length} values.", span);
        }

        return new PyNamedTupleObject(this, materialized);
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
            if (arguments.Length != 1 || arguments[0].IsKeyword)
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

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
        return _type.TryGetMember(name, out var value)
            ? value
            : throw new InvalidOperationException($"Named tuple type member '{name}' is missing.");
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

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
