using System.Collections;
using System.Linq;
using System.Numerics;
using System.Text;
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

    public PyNamedTupleType(string typeName, IReadOnlyList<string> fieldNames) : this(typeName, fieldNames, null) { }

    public PyNamedTupleType(string typeName, IReadOnlyList<string> fieldNames, IReadOnlyList<object>? defaults)
        : this(typeName, fieldNames, defaults, null, null)
    {
    }

    public PyNamedTupleType(string typeName, IReadOnlyList<string> fieldNames, IReadOnlyList<object>? defaults, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        // Shell, retained reference arrays and conservative scalar headroom.
        // The caller keeps its defaults lease alive through these final copies.
        var storageBytes = 128L + 16L * fieldNames.Count + 80L * (defaults?.Count ?? 0);
        using var storage = governor?.ReserveTemporary(storageBytes, span);
        _typeName = typeName;
        _fieldNames = fieldNames.ToArray();
        _defaults = defaults?.ToArray() ?? [];
        _governor = governor;
        _allocationSpan = span;
        _nameValue = governor is null ? PyString.FromString(typeName) : PyString.FromString(typeName, governor, span);
        using var tupleStorage = governor?.ReserveTemporary(PyTuple.EstimateApproximateBytes(_fieldNames.Length), span);
        var fieldValues = _fieldNames.Select(name => governor is null
            ? PyString.FromString(name) : PyString.FromString(name, governor, span)).Cast<object>().ToArray();
        _fieldsTuple = governor is null
            ? PyTuple.FromOwnedArray(fieldValues)
            : PyTuple.FromReservedOwnedArray(fieldValues, governor, span, tupleStorage!);
        storage?.Commit(storageBytes);
    }

    public string Name => _typeName;

    internal object? ModuleName { get; set; }

    internal LythonRuntime.FunctionNewMethod GetNewSlot() => NewSlot ??= new LythonRuntime.FunctionNewMethod(this, _typeName);

    internal LythonRuntime.FunctionNewMethod? NewSlot { get; set; }

    public IReadOnlyList<string> FieldNames => _fieldNames;

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
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

        var record = new PyNamedTupleObject(this, values, context.MemoryGovernor, span);
        return record;
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

    private Dictionary<string, TupleGetter>? _fieldGetters;

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

        // Fields resolve to shared per-field descriptors like CPython.
        var fieldIndex = IndexOfField(name);
        if (fieldIndex >= 0)
        {
            _fieldGetters ??= new Dictionary<string, TupleGetter>(StringComparer.Ordinal);
            if (!_fieldGetters.TryGetValue(name, out var getter))
            {
                getter = new TupleGetter(this, fieldIndex);
                _fieldGetters[name] = getter;
            }

            value = getter;
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
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", $"{_type.Name}._make(iterable) expects one iterable argument.", span);
            }

            var record = _type.CreateFromValues(LythonRuntime.ToSequence(arguments[0].Value, span, context), context.MemoryGovernor, span);
            return record;
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

// Namedtuple field descriptors behave like CPython _tuplegetter objects:
// per-field documentation, method-wrapper __get__/__set__ slots, shared per
// owner so identity holds, and no value semantics beyond that (notably not
// callable, and without __name__/__qualname__/__objclass__ like CPython).
internal sealed class TupleGetter : IPyDynamicAttributes, IPyRenderableValue
{
    private readonly object _owner;
    private readonly int _index;

    internal TupleGetter(object owner, int index)
    {
        _owner = owner;
        _index = index;
    }

    internal int Index => _index;

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "__doc__")
        {
            value = PyString.FromString("Alias for field number " + _index);
            return true;
        }

        if (name == "__module__")
        {
            value = LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections");
            return true;
        }

        if (name == "__get__")
        {
            value = new PyBoundMethod(this, TupleGetterGetMethod.Instance);
            return true;
        }

        if (name == "__set__")
        {
            value = new PyBoundMethod(this, TupleGetterSetMethod.Instance);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<_tuplegetter(" + _index + ", 'Alias for field number " + _index + "')>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<_tuplegetter({_index}, 'Alias for field number {_index}')>";
}

internal sealed class TupleGetterGetMethod : LythonRuntime.ICallable, IPyDynamicAttributes, IPySlotWrapper
{
    internal static readonly TupleGetterGetMethod Instance = new();

    private TupleGetterGetMethod()
    {
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "__name__")
        {
            value = PyString.FromString("__get__");
            return true;
        }

        if (name == "__qualname__")
        {
            value = PyString.FromString("_tuplegetter.__get__");
            return true;
        }

        if (name == "__objclass__")
        {
            value = PyType.TupleGetterType;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "wrapper __get__() takes no keyword arguments", span);
            }
        }

        // arguments[0] is the getter the bound method prepended.
        var positionals = arguments.Length - 1;
        if (positionals < 1)
        {
            throw new LythonRuntimeException("TypeError", " expected at least 1 argument, got 0", span);
        }

        if (positionals > 2)
        {
            throw new LythonRuntimeException("TypeError", " expected at most 2 arguments, got " + positionals, span);
        }

        if (arguments[0].Value is not TupleGetter getter)
        {
            throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
        }

        var target = arguments[1].Value;
        if (target is PyNone)
        {
            if (positionals == 2 && arguments[2].Value is not PyNone)
            {
                return getter;
            }

            throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
        }

        if (!LythonRuntime.DoesObjectMatchBuiltinType("tuple", target))
        {
            throw new LythonRuntimeException("TypeError", "descriptor for index '" + getter.Index + "' for tuple subclasses doesn't apply to a '" + LythonRuntime.UnboundTypeMethod.PythonTypeName(target, context) + "' object", span);
        }

        return target switch
        {
            PyTuple tuple => tuple[getter.Index],
            PyNamedTupleObject namedTuple => namedTuple.GetItem(getter.Index),
            PyTypingNamedTupleObject typingTuple => typingTuple.GetItem(getter.Index),
            LythonRuntime.TimeStructTimeValue structTime => structTime.GetItem(getter.Index),
            _ => throw new LythonRuntimeException("TypeError", "descriptor for index '" + getter.Index + "' for tuple subclasses doesn't apply to a '" + LythonRuntime.UnboundTypeMethod.PythonTypeName(target, context) + "' object", span),
        };
    }
}

internal sealed class TupleGetterSetMethod : LythonRuntime.ICallable, IPyDynamicAttributes, IPySlotWrapper
{
    internal static readonly TupleGetterSetMethod Instance = new();

    private TupleGetterSetMethod()
    {
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "__name__")
        {
            value = PyString.FromString("__set__");
            return true;
        }

        if (name == "__qualname__")
        {
            value = PyString.FromString("_tuplegetter.__set__");
            return true;
        }

        if (name == "__objclass__")
        {
            value = PyType.TupleGetterType;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "wrapper __set__() takes no keyword arguments", span);
            }
        }

        if (arguments.Length - 1 != 2)
        {
            throw new LythonRuntimeException("TypeError", " expected 2 arguments, got " + (arguments.Length - 1), span);
        }

        throw new LythonRuntimeException("AttributeError", "can't set attribute", span);
    }
}

internal sealed class PyNamedTupleObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPyHashableValue, IPyGovernedValue
{
    private readonly PyNamedTupleType _type;
    private readonly object[] _values;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private readonly long _committedBytes;

    public PyNamedTupleObject(PyNamedTupleType type, object[] values)
    {
        _type = type;
        _values = [.. values];
    }

    // Guest-constructed instances own their backing array at the tuple slot
    // rate; engine-owned tuples without a governor stay free. Conservative
    // numeric slot allowances join the backing charge without identity maps.
    public PyNamedTupleObject(PyNamedTupleType type, object[] values, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _type = type;
        _values = PyTuple.CopyGovernedItems(values, governor, allocationSpan, out _committedBytes);
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    // Current committed backing charges, mirroring the tuple slot rate: records never grow,
    // so the snapshot stays exact. Unowned records carry nothing; scalar-slot
    // allowances belong to the same backing owner through its drop lifetime.
    internal long CommittedStorageBytes => _committedBytes;

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
            .Select((fieldName, index) => $"{fieldName}={PyRendering.ToReprPyString(_values[index], context).AsString()}");
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
            context.CheckExecution(span);
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
            context.CheckExecution(span);
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

            var record = new PyNamedTupleObject(_owner._type, values, context.MemoryGovernor, span);
            return record;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_owner._type.Name}._replace>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

// ChainMap del-miss carries its key for lazy rendering: CPython spells the
// failure as one quoted composite, which cannot be composed at the
// governor-less del site, so the composite (including its double quotes)
// renders at display time through the display governor like other governed
// renderer outputs.
internal sealed class ChainMapMissingKey(object key) : IPyRenderableValue
{
    public object Key { get; } = key;

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendAscii("Key not found in the first mapping: ");
        builder.Append(PyRendering.ToReprPyString(Key, context));
        var composite = builder.ToPyStringAndRelease();
        // Quote like CPython repr: double quotes only when the text holds
        // a single quote but no double quote, otherwise single-quoted with
        // escapes through the shared renderer.
        var text = composite.AsString();
        if (text.Contains((char)39) && !text.Contains((char)34))
        {
            return PyString.FromString("\"" + text + "\"", context.Context.MemoryGovernor);
        }

        return PyRendering.ToReprPyString(composite, context);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

// Aliased ChainMap sources: the narrow mapping contract a ChainMap needs over
// its underlying mappings (plain dicts, defaultdicts, counters) without copying
// them at construction. Plain lookups never trigger factories or zero-fills;
// subscript reads resolve those through PyChainMap with an execution context.
internal interface IChainMapSource
{
    // The original mapping object, exposed through the maps view with stable
    // identity like CPython.
    object Underlying { get; }

    int Count { get; }

    IEnumerable<object> Keys { get; }

    IEnumerable<object> Values { get; }

    IEnumerable<KeyValuePair<object, object>> Items { get; }

    bool TryGetValue(object key, [MaybeNullWhen(false)] out object value);

    void SetItem(object key, object value);

    bool Remove(object key);

    MemoryGovernor? OwnerMemoryGovernor { get; }
}

internal sealed class PyChainMap : IMutablePySubscriptableValue, IDeletablePySubscriptableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyMutableDynamicAttributes, IPySizedValue
{
    private static readonly LythonCallableSignature GetCallSignature = LythonCallableSignature.Create(
        "ChainMap.get",
        ["key", "default"],
        requiredCount: 1);

    private static readonly LythonCallableSignature PopCallSignature = LythonCallableSignature.Create(
        "ChainMap.pop",
        ["key", "default"],
        requiredCount: 1);

    private object _mapValues;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonRuntime.ExecutionContext? _context;
    private readonly LythonSourceSpan _span;

    public PyChainMap(IEnumerable<IChainMapSource> maps, LythonRuntime.ExecutionContext? context = null,
        LythonSourceSpan? span = null)
    {
        _context = context;
        _span = span ?? new LythonSourceSpan(0, 0, 0, 0);
        using var source = maps.GetEnumerator();
        var supplied = source.MoveNext();
        var first = supplied ? source.Current
            : context is null ? new PyDict() : new PyDict(context.MemoryGovernor, span);
        _memoryGovernor = context?.MemoryGovernor ?? first.OwnerMemoryGovernor;
        using var shell = _memoryGovernor?.ReserveTemporary(64L, span);
        var values = _memoryGovernor is null ? new PyList() : new PyList(Array.Empty<object>(), _memoryGovernor, span);
        try
        {
            values.Add(first.Underlying);
            while (source.MoveNext())
            {
                context?.CheckExecution(span);
                values.Add(source.Current.Underlying);
            }
            _mapValues = values;
            shell?.Commit(64L);
        }
        catch
        {
            if (_memoryGovernor is not null)
            {
                values.DiscardUnpublishedStorage();
                if (!supplied) _memoryGovernor.RefundUnpublishedValue(first);
            }
            throw;
        }
    }

    public int Count => CountMergedKeys();
    public int Length => Count;
    public bool IsTruthy() => Maps.Any(map => map.Count != 0);
    internal MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;
    internal IEnumerable<IChainMapSource> Maps => EnumerateMaps();
    internal IChainMapSource FirstMap
    {
        get
        {
            using var maps = Maps.GetEnumerator();
            return maps.MoveNext() ? maps.Current
                : throw new LythonRuntimeException("IndexError", "list index out of range", _span);
        }
    }

    private IEnumerable<IChainMapSource> EnumerateMaps()
    {
        var values = _context is null
            ? PyIteration.ToSequence(_mapValues, _span)
            : PyIteration.ToSequence(_mapValues, _span, _context);
        foreach (var value in values) yield return ExpectMap(value, _span);
    }

    public bool TrySetMember(string name, object value)
    {
        if (name != "maps") return false;
        _mapValues = value;
        return true;
    }

    // Returned snapshots are batch-owned; counting uses only private scratch.
    public IEnumerable<object> Iterate() => BuildOwnedKeySnapshot(_context, _span);

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (_context is not null) return GetSubscript(index, span, _context);
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        foreach (var map in Maps)
        {
            // Factory-free walk for contexts without execution access: plain
            // misses continue, while counter zero-fills stop the chain like
            // CPython. defaultdict misses fall through to a KeyError here; the
            // contextful overload below invokes the factory instead.
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }

            if (map is PyCounter counter)
            {
                return counter.GetCount(key);
            }
        }

        throw RuntimeErrors.MissingKey(index, span);
    }

    // Subscript read with missing-key behavior: defaultdict misses invoke the
    // factory (storing the result) and counter misses yield zero, stopping the
    // chain in both cases like CPython. Plain misses continue onward.
    internal object GetSubscript(object index, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }

            if (map is PyCounter counter)
            {
                return counter.GetCount(key);
            }

            if (map is PyDefaultDict defaultdict)
            {
                return defaultdict.GetOrCreate(key, context, span);
            }
        }

        throw RuntimeErrors.MissingKey(index, span);
    }

    public bool ContainsKey(object candidate, LythonSourceSpan span)
    {
        // Membership never triggers factories or zero-fills: a missing key is
        // simply absent even where a subscript read would manufacture a value.
        var key = LythonRuntime.ValidateDictionaryKey(candidate, span);
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out _))
            {
                return true;
            }
        }

        return false;
    }

    internal bool TryPopFirstMap(object key, [MaybeNullWhen(false)] out object value)
    {
        if (FirstMap.TryGetValue(key, out value))
        {
            FirstMap.Remove(key);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public void SetSubscript(object index, object value, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        FirstMap.SetItem(key, value);
    }

    public void DeleteSubscript(object index, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        if (!FirstMap.Remove(key))
        {
            throw new LythonRuntimeException("KeyError", "Key not found in the first ChainMap mapping.", span, null, new ChainMapMissingKey(key));
        }
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        // Unhashable mappings serve __hash__ as None like CPython; the
        // switch below uses PyNone as its missing sentinel, so this
        // precedes it.
        if (name == "__hash__")
        {
            value = PyNone.Instance;
            return true;
        }

        value = name switch
        {
            "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections"),
            "maps" => _mapValues,
            "parents" => CreateParents(),
            "get" => new BoundChainMapGet(this),
            "pop" => new BoundChainMapPop(this),
            "keys" => new BoundChainMapKeys(this),
            "values" => new BoundChainMapValues(this),
            "items" => new BoundChainMapItems(this),
            "new_child" => new BoundChainMapNewChild(this),
            "copy" => new BoundChainMapCopy(this),
            "__len__" => new BoundChainMapLen(this),
            "__contains__" => new BoundChainMapContains(this),
            "__getitem__" => new BoundChainMapGetItem(this),
            "__setitem__" => new BoundChainMapSetItem(this),
            "__delitem__" => new BoundChainMapDeleteItem(this),
            "update" => new BoundChainMapUpdate(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    private PyChainMap CreateParents()
    {
        if (Maps.Skip(1).Any()) return new PyChainMap(Maps.Skip(1), _context, _span);
        var empty = _memoryGovernor is null ? new PyDict() : new PyDict(_memoryGovernor, _span);
        return new PyChainMap([empty], _context, _span);
    }

    public PyString RenderPython(PyRenderingContext context)
            => PyRendering.JoinRenderedSequence("ChainMap(", new RenderedMaps(PyIteration.ToSequence(_mapValues, _span, context.Context), context), ")", context);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private object GetOrDefault(object key, object defaultValue)
    {
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return defaultValue;
    }

    private PyDict MergeKeys(LythonRuntime.ExecutionContext? context, LythonSourceSpan span)
    {
        var governor = context?.MemoryGovernor ?? _memoryGovernor;
        var merged = governor is null ? new PyDict() : new PyDict(governor, span);
        try
        {
            if (_mapValues is not IReadOnlyList<object> maps)
                throw new LythonRuntimeException("TypeError", "ChainMap.maps is not a reversible sequence", span);
            for (var i = maps.Count - 1; i >= 0; i--)
            {
                context?.CheckExecution(span);
                // A reversed Python list iterator stops if reentry shortened it
                // below its current position; appends do not extend that cursor.
                if (i >= maps.Count) break;
                var map = ExpectMap(maps[i], span);
                var dictionary = map switch
                {
                    PyDict dict => dict,
                    PyCounter counter => counter.InnerDict,
                    PyDefaultDict defaults => defaults.InnerDict,
                    _ => throw new InvalidOperationException("Unsupported native ChainMap source.")
                };
                merged.AddKeysFrom(dictionary, context, span);
            }
            return merged;
        }
        catch { governor?.RefundUnpublishedValue(merged); throw; }
    }

    internal PyList BuildOwnedKeySnapshot(LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        context ??= _context;
        span ??= _span;
        var governor = context?.MemoryGovernor ?? _memoryGovernor;
        var merged = MergeKeys(context, span);
        try
        {
            return governor is null ? new PyList(merged.Keys) : new PyList(merged.Keys, governor, span);
        }
        finally { governor?.RefundUnpublishedValue(merged); }
    }

    internal PyList BuildMergedKeys(LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
        => BuildOwnedKeySnapshot(context, span);

    // Native mapping consumers borrow private merge storage only until their
    // enumeration ends. Guest iterators use the separately owned snapshot above.
    internal IEnumerable<object> EnumerateMappingKeys(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var merged = MergeKeys(context, span);
        try
        {
            foreach (var key in merged.Keys) yield return key;
        }
        finally { context.MemoryGovernor.RefundUnpublishedValue(merged); }
    }

    // Spanless live lookup for span-free enumerators: same chain rules as the
    // context-free subscript (counter zero-fills stop; defaultdict misses fall
    // through to KeyError without a factory, which needs execution access).
    internal object GetLiveValue(object key)
    {
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out var value))
            {
                return value;
            }

            if (map is PyCounter counter)
            {
                return counter.GetCount(key);
            }
        }

        throw RuntimeErrors.MissingKey(key, null);
    }

    internal bool TryGetStrictValue(object key, [MaybeNullWhen(false)] out object value)
    {
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Live value stream over an owned key order: keys freeze at snapshot time
    // while values resolve per pull like CPython view iterators (a deleted key
    // raises KeyError; counter zero-fills stop without a factory).
    internal static IEnumerable<object> EnumerateOwnedValues(
        PyChainMap owner,
        IReadOnlyList<object> snapshotKeys,
        LythonSourceSpan span)
    {
        foreach (var key in snapshotKeys)
        {
            yield return owner.GetSubscript(key, span);
        }
    }

    internal static IEnumerable<object> EnumerateOwnedItems(
        PyChainMap owner,
        IReadOnlyList<object> snapshotKeys,
        LythonSourceSpan span)
    {
        var governor = owner.OwnerMemoryGovernor;
        foreach (var key in snapshotKeys)
        {
            var value = owner.GetSubscript(key, span);
            yield return governor is null
                ? PyTuple.FromOwnedArray([key, value])
                : PyTuple.FromOwnedArray([key, value], governor, null);
        }
    }

    internal bool TryGetMergedValue(object key, [MaybeNullWhen(false)] out object value)
    {
        foreach (var map in Maps)
        {
            if (map.TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    internal int CountMergedKeys(LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= _context;
        span ??= _span;
        var governor = context?.MemoryGovernor ?? _memoryGovernor;
        var merged = MergeKeys(context, span);
        try { return merged.Count; }
        finally { governor?.RefundUnpublishedValue(merged); }
    }

    internal static IChainMapSource ExpectMap(object value, LythonSourceSpan span)
        => value is IChainMapSource source ? source
            : throw new LythonRuntimeException("TypeError", "ChainMap maps must be dictionaries.", span);

    // First-map copies preserve their kind (a defaultdict stays a defaultdict
    // with the same factory); every other map aliases like CPython.
    private static IChainMapSource CopyChainMapSource(IChainMapSource source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var governor = context.MemoryGovernor;
        IChainMapSource copy = source.Underlying switch
        {
            PyDefaultDict defaultdict => CopyDefaultDict(defaultdict, governor, span),
            PyCounter counter => new PyCounter(counter, governor, span),
            _ => new PyDict((PyDict)source.Underlying, governor, span),
        };
        return copy;
    }

    private static PyDefaultDict CopyDefaultDict(PyDefaultDict source, MemoryGovernor governor, LythonSourceSpan span)
        => new(source.DefaultFactory, source.InnerDict, governor, span);

    // Update writes plain keys into the first mapping like CPython, whatever its
    // kind: dicts take the shared path, while defaultdicts and counters write
    // through the source contract (plain overwrite, never counter-add or factory
    // reads). Keyword names adopt fresh governed strings like dict.update.
    private static object UpdateFirstMap(PyChainMap owner, CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var first = owner.FirstMap;
        if (first is PyDict plain)
        {
            return LythonRuntime.UpdateDictionary(plain, arguments, span, context);
        }

        var positional = arguments.Where(argument => argument.IsPositional).ToArray();
        if (positional.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "ChainMap.update expected at most 1 positional argument.", span);
        }

        if (positional.Length == 1)
        {
            LythonRuntime.UpdateDictionaryFromSource(first, positional[0].Value, context, span);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                var keyword = PyString.FromString(argument.KeywordName, context.MemoryGovernor, span);
                first.SetItem(keyword, argument.Value);
            }
        }

        return PyNone.Instance;
    }

    private sealed class BoundChainMapGet : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapGet(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, GetCallSignature, PythonCallableKind.Method);
            var key = LythonRuntime.ValidateDictionaryKey(bound[0], span);
            return _owner.GetOrDefault(key, bound.Length == 2 ? bound[1] : PyNone.Instance);
        }
    }

    private sealed class BoundChainMapPop : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapPop(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, PopCallSignature, PythonCallableKind.Method);
            var key = LythonRuntime.ValidateDictionaryKey(bound[0], span);
            if (_owner.TryPopFirstMap(key, out var found))
            {
                return found;
            }

            if (bound.Length == 2)
            {
                return bound[1];
            }

            _owner.DeleteSubscript(key, span);
            return PyNone.Instance; // Unreachable: missing keys always throw above.
        }
    }

    private sealed class BoundChainMapKeys : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapKeys(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.keys() expects no arguments.", span);
            }

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var keys = new ChainMapKeysView(_owner);
            return keys;
        }
    }

    private sealed class BoundChainMapValues : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapValues(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.values() expects no arguments.", span);
            }

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var values = new ChainMapValuesView(_owner);
            return values;
        }
    }

    private sealed class BoundChainMapItems : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapItems(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.items() expects no arguments.", span);
            }

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var items = new ChainMapItemsView(_owner);
            return items;
        }
    }

    private sealed class BoundChainMapNewChild : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapNewChild(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.new_child([m]) expects zero or one mapping.", span);
            }

            IChainMapSource first;
            if (arguments.Length == 0)
            {
                var fresh = new PyDict(context.MemoryGovernor, span);
                first = fresh;
            }
            else
            {
                first = ExpectMap(arguments[0].Value, span);
            }

            return new PyChainMap(_owner.Maps.Prepend(first), context, span);
        }
    }

    private sealed class BoundChainMapCopy : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapCopy(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.copy() expects no arguments.", span);
            }

            var copy = CopyChainMapSource(_owner.FirstMap, context, span);
            return new PyChainMap(_owner.Maps.Skip(1).Prepend(copy), context, span);
        }
    }

    private sealed class BoundChainMapGetItem : LythonRuntime.ICallable
    {
        private static readonly LythonCallableSignature GetItemCallSignature = LythonCallableSignature.Create(
            "ChainMap.__getitem__",
            ["index"],
            requiredCount: 1);

        private readonly PyChainMap _owner;

        public BoundChainMapGetItem(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, GetItemCallSignature, PythonCallableKind.Method);
            return LythonRuntime.ReadSubscriptValue(_owner, bound[0], span, context);
        }
    }

    private sealed class BoundChainMapUpdate : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapUpdate(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var positional = 0;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional++;
                }
            }

            if (positional > 1)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.update expected at most 1 positional argument.", span);
            }

            return UpdateFirstMap(_owner, arguments, span, context);
        }
    }

    private sealed class BoundChainMapDeleteItem : LythonRuntime.ICallable
    {
        private static readonly LythonCallableSignature DeleteItemCallSignature = LythonCallableSignature.Create(
            "ChainMap.__delitem__",
            ["index"],
            requiredCount: 1);

        private readonly PyChainMap _owner;

        public BoundChainMapDeleteItem(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, DeleteItemCallSignature, PythonCallableKind.Method);
            LythonRuntime.DeleteSubscriptValue(_owner, bound[0], span, context);
            return PyNone.Instance;
        }
    }

    private sealed class BoundChainMapSetItem : LythonRuntime.ICallable
    {
        private static readonly LythonCallableSignature SetItemCallSignature = LythonCallableSignature.Create(
            "ChainMap.__setitem__",
            ["index", "value"],
            requiredCount: 2);

        private readonly PyChainMap _owner;

        public BoundChainMapSetItem(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, SetItemCallSignature, PythonCallableKind.Method);
            LythonRuntime.SetSubscriptValue(_owner, bound[0], bound[1], span, context);
            return PyNone.Instance;
        }
    }

    private sealed class BoundChainMapLen : LythonRuntime.ICallable
    {
        private readonly PyChainMap _owner;

        public BoundChainMapLen(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.__len__() expects no arguments.", span);
            }

            return LythonRuntime.LenChainMap(_owner, span, context);
        }
    }

    private sealed class BoundChainMapContains : LythonRuntime.ICallable
    {
        private static readonly LythonCallableSignature ContainsCallSignature = LythonCallableSignature.Create(
            "ChainMap.__contains__",
            ["item"],
            requiredCount: 1);

        private readonly PyChainMap _owner;

        public BoundChainMapContains(PyChainMap owner) => _owner = owner;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, ContainsCallSignature, PythonCallableKind.Method);
            using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
            return PyContainment.Contains(_owner, bound[0], span);
        }
    }

    private sealed class RenderedMaps : IEnumerable<PyString>
    {
        private readonly IEnumerable<object> _maps;
        private readonly PyRenderingContext _context;

        public RenderedMaps(IEnumerable<object> maps, PyRenderingContext context)
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

internal sealed class ChainMapKeysView : IReadOnlyCollection<object>, IPyRenderableValue
{
    private readonly PyChainMap _owner;

    public ChainMapKeysView(PyChainMap owner) => _owner = owner;

    internal PyChainMap Owner => _owner;

    public int Count => _owner.CountMergedKeys();

    public IEnumerator<object> GetEnumerator() => _owner.BuildMergedKeys().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("KeysView(", [_owner], ")", context, interpolated: false);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("KeysView(", [_owner], ")", context, interpolated: true);
}

internal sealed class ChainMapValuesView : IReadOnlyCollection<object>, IPyRenderableValue
{
    private readonly PyChainMap _owner;

    public ChainMapValuesView(PyChainMap owner) => _owner = owner;

    internal PyChainMap Owner => _owner;

    public int Count => _owner.CountMergedKeys();

    public IEnumerator<object> GetEnumerator()
    {
        foreach (var key in _owner.BuildMergedKeys())
        {
            yield return _owner.GetLiveValue(key);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ValuesView(", [_owner], ")", context, interpolated: false);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ValuesView(", [_owner], ")", context, interpolated: true);
}

internal sealed class ChainMapItemsView : IReadOnlyCollection<object>, IPyRenderableValue
{
    private readonly PyChainMap _owner;

    public ChainMapItemsView(PyChainMap owner) => _owner = owner;

    internal PyChainMap Owner => _owner;

    public int Count => _owner.CountMergedKeys();

    public IEnumerator<object> GetEnumerator()
    {
        var governor = _owner.OwnerMemoryGovernor;
        foreach (var key in _owner.BuildMergedKeys())
        {
            var value = _owner.GetLiveValue(key);
            yield return governor is null
                ? PyTuple.FromOwnedArray([key, value])
                : PyTuple.FromOwnedArray([key, value], governor, null);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ItemsView(", [_owner], ")", context, interpolated: false);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ItemsView(", [_owner], ")", context, interpolated: true);
}
