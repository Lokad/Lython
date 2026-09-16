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
    private readonly long _committedStorageBytes;

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

        // Snapshot the governed construction shares (the name plus per-field strings
        // beside the fields-tuple backing) so fresh types adopt exactly; ungoverned
        // types commit nothing and carry no coupon.
        long shares = 0;
        if (governor is not null)
        {
            shares = RuntimeMemoryEstimates.SaturatingAdd(shares, PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(typeName)));
            foreach (var fieldName in _fieldNames)
            {
                shares = RuntimeMemoryEstimates.SaturatingAdd(shares, PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(fieldName)));
            }

            shares = RuntimeMemoryEstimates.SaturatingAdd(shares, PyTuple.EstimateApproximateBytes(_fieldNames.Length));
        }

        _committedStorageBytes = shares;
    }

    public string Name => _typeName;

    // Governed construction shares snapshotted above; ungoverned types carry nothing.
    internal long CommittedStorageBytes => _committedStorageBytes;

    // Fresh namedtuple types reclaim through the pool once dropped; the constructor
    // commits the name/fields backing above with no other owner, so adopt the snapshot
    // with refund on entry denial.
    internal static PyNamedTupleType TrackFreshNamedTupleType(PyNamedTupleType type, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.Services.State.CallTemporaries.TrackFreshMutable(type, type.CommittedStorageBytes, span);
        return type;
    }

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

        var record = new PyNamedTupleObject(this, values, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(record, record.CommittedStorageBytes);
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
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", $"{_type.Name}._make(iterable) expects one iterable argument.", span);
            }

            var record = _type.CreateFromValues(LythonRuntime.ToSequence(arguments[0].Value, span, context), context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(record, record.CommittedStorageBytes);
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
        context.CheckExecutionBudget(span);
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
        context.CheckExecutionBudget(span);
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

    // Current committed backing charges, mirroring the tuple slot rate: records never grow,
    // so the snapshot stays exact. Unowned records carry nothing.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : PyTuple.EstimateApproximateBytes(_values.Length);

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

            context.Services.State.CallTemporaries.TrackFreshMutable(dict, dict.CommittedStorageBytes);
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

            var record = new PyNamedTupleObject(_owner._type, values, context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(record, record.CommittedStorageBytes);
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

internal sealed class PyChainMap : IMutablePySubscriptableValue, IDeletablePySubscriptableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes, IPySizedValue
{
    private static readonly LythonCallableSignature GetCallSignature = LythonCallableSignature.Create(
        "ChainMap.get",
        ["key", "default"],
        requiredCount: 1);

    private static readonly LythonCallableSignature PopCallSignature = LythonCallableSignature.Create(
        "ChainMap.pop",
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

    internal MemoryGovernor? OwnerMemoryGovernor => _maps[0].OwnerMemoryGovernor;

    internal IReadOnlyList<PyDict> Maps => _maps;

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

    public bool ContainsKey(object candidate, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(candidate, span);
        foreach (var map in _maps)
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
        if (_maps[0].TryGetValue(key, out value))
        {
            _maps[0].Remove(key);
            return true;
        }

        value = PyNone.Instance;
        return false;
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
            "maps" => OwnedMapsList(),
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

    internal IReadOnlyList<object> BuildMergedKeys()
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

    internal IReadOnlyList<KeyValuePair<object, object>> BuildMergedItems()
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

    internal bool TryGetMergedValue(object key, [MaybeNullWhen(false)] out object value)
    {
        foreach (var map in _maps)
        {
            if (map.TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    internal int CountMergedKeys()
    {
        var keys = new HashSet<object>(PyValueComparer.Instance);
        foreach (var map in _maps)
        {
            keys.UnionWith(map.Keys);
        }

        return keys.Count;
    }

    private static PyDict ExpectMap(object value, LythonSourceSpan span, MemoryGovernor governor, LythonRuntime.ExecutionContext context)
        => value switch
        {
            PyDict dict => dict,
            PyDefaultDict defaultDict => ToPyDict(defaultDict, governor, span, context),
            _ => throw new LythonRuntimeException("TypeError", "ChainMap maps must be dictionaries.", span)
        };

    internal static IReadOnlyList<PyDict> NormalizeMaps(IEnumerable<object> values, LythonSourceSpan span, MemoryGovernor governor, LythonRuntime.ExecutionContext context)
        => values.Select(value => ExpectMap(value, span, governor, context)).ToArray();

    private static PyDict ToPyDict(PyDefaultDict defaultDict, MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var dict = new PyDict(governor, span);
        foreach (var pair in defaultDict)
        {
            dict.SetItem(pair.Key, pair.Value);
        }

        context.Services.State.CallTemporaries.TrackFreshMutable(dict, dict.CommittedStorageBytes);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ChainMap.keys() expects no arguments.", span);
            }

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var keys = new ChainMapKeysView(_owner);
            context.Services.State.CallTemporaries.Track(keys, 64L);
            return keys;
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

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var values = new ChainMapValuesView(_owner);
            context.Services.State.CallTemporaries.Track(values, 64L);
            return values;
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

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var items = new ChainMapItemsView(_owner);
            context.Services.State.CallTemporaries.Track(items, 64L);
            return items;
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

            PyDict first;
            if (arguments.Length == 0)
            {
                first = new PyDict(context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshMutable(first, first.CommittedStorageBytes);
            }
            else
            {
                first = ExpectMap(arguments[0].Value, span, context.MemoryGovernor, context);
            }

            var maps = new List<PyDict> { first };
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

            var copy = new PyDict(_owner._maps[0], context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(copy, copy.CommittedStorageBytes);
            var maps = new List<PyDict> { copy };
            maps.AddRange(_owner._maps.Skip(1));
            return new PyChainMap(maps);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
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

            return LythonRuntime.UpdateDictionary(_owner.Maps[0], arguments, span, context);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
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
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(arguments, span, ContainsCallSignature, PythonCallableKind.Method);
            return PyContainment.Contains(_owner, bound[0], span);
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

    public int Count => _owner.CountMergedKeys();

    public IEnumerator<object> GetEnumerator()
    {
        foreach (var pair in _owner.BuildMergedItems())
        {
            yield return pair.Value;
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
        foreach (var pair in _owner.BuildMergedItems())
        {
            yield return governor is null
                ? PyTuple.FromOwnedArray([pair.Key, pair.Value])
                : PyTuple.FromOwnedArray([pair.Key, pair.Value], governor, null);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ItemsView(", [_owner], ")", context, interpolated: false);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedValues("ItemsView(", [_owner], ")", context, interpolated: true);
}
