using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyType : IPyRenderableValue, LythonRuntime.ICallable, IPyHashableValue
{
    private readonly Dictionary<string, object> _members;

    public PyType(string name, IReadOnlyList<PyType> bases, Dictionary<string, object> members)
    {
        Name = name;
        Bases = bases;
        _members = members;
        Mro = BuildMro(this, bases);
        BindOwnedMembers();
    }

    public string Name { get; }

    public IReadOnlyList<PyType> Bases { get; }

    public IReadOnlyList<PyType> Mro { get; }

    public IReadOnlyList<DataclassFieldSpec>? DataclassFields { get; private set; }

    public DataclassFieldSpec[]? DataclassHelperFields { get; private set; }

    public DataclassFieldSpec[]? DataclassReprFields { get; private set; }

    public DataclassFieldSpec[]? DataclassComparableFields { get; private set; }

    public IReadOnlyDictionary<string, DataclassFieldSpec>? DataclassFieldsByName { get; private set; }

    public bool DataclassReprEnabled { get; private set; }

    public bool DataclassEqEnabled { get; private set; }

    public bool DataclassOrderEnabled { get; private set; }

    public DataclassHashMode DataclassHashMode { get; private set; }

    public PyType? MetaType { get; private set; }

    public bool TryGetOwnMember(string name, [MaybeNullWhen(false)] out object value) => _members.TryGetValue(name, out value);

    public IEnumerable<KeyValuePair<string, object>> EnumerateOwnMembers() => _members;

    public IEnumerable<string> EnumerateMemberNames()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in Mro)
        {
            foreach (var name in type._members.Keys)
            {
                if (seen.Add(name))
                {
                    yield return name;
                }
            }
        }
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        => PyAttributeLookup.TryResolveTypeMember(this, name, out value);

    public bool TryLookupInMro(string name, int startIndex, [MaybeNullWhen(false)] out object value, out PyType? ownerType)
    {
        for (var i = startIndex; i < Mro.Count; i++)
        {
            var current = Mro[i];
            if (current._members.TryGetValue(name, out value))
            {
                ownerType = current;
                return true;
            }
        }

        value = PyNone.Instance;
        ownerType = null;
        return false;
    }

    public bool TryGetSuccessorMroIndex(PyType anchorType, out int index)
    {
        for (var i = 0; i < Mro.Count; i++)
        {
            if (ReferenceEquals(Mro[i], anchorType))
            {
                index = i + 1;
                return true;
            }
        }

        index = -1;
        return false;
    }

    public bool TrySetMember(string name, object value)
    {
        if (value is IClassNamedMember named)
        {
            named.BindName(name);
        }

        if (value is IClassOwnedMember owned)
        {
            owned.BindOwner(this);
        }

        _members[name] = value;
        return true;
    }

    public bool RemoveOwnMember(string name) => _members.Remove(name);

    public void InitializeClassMembers(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var pair in _members)
        {
            if (TryGetSetNameCallable(pair.Value, context, span, out var callable))
            {
                _ = callable.Invoke(
                    [
                        CallArgumentValue.Positional(this),
                        CallArgumentValue.Positional(PyString.FromString(pair.Key))
                    ],
                    span,
                    context);
            }
        }
    }

    public bool IsSubtypeOf(PyType other)
    {
        return Mro.Any(type => ReferenceEquals(type, other));
    }

    public bool TryGetMatchArgs(out IReadOnlyList<string> matchArgs)
    {
        if (TryGetMember("__match_args__", out var value) && value is PyTuple tuple)
        {
            var names = new string[tuple.Count];
            var index = 0;
            foreach (var item in tuple)
            {
                if (!PyStringOps.TryAsString(item, out var text))
                {
                    matchArgs = Array.Empty<string>();
                    return false;
                }

                names[index++] = text.AsString();
            }

            matchArgs = names;
            return true;
        }

        matchArgs = Array.Empty<string>();
        return false;
    }

    public void SetDataclassMetadata(IReadOnlyList<DataclassFieldSpec> fields, bool reprEnabled, bool eqEnabled, bool orderEnabled, DataclassHashMode hashMode)
    {
        DataclassFields = fields;
        DataclassHelperFields = fields.Where(static field => field.Kind == DataclassFieldKind.Normal).ToArray();
        DataclassReprFields = fields.Where(static field => field.Repr).ToArray();
        DataclassComparableFields = fields.Where(static field => field.Compare).ToArray();
        DataclassFieldsByName = fields.ToDictionary(static field => field.Name, StringComparer.Ordinal);
        DataclassReprEnabled = reprEnabled;
        DataclassEqEnabled = eqEnabled;
        DataclassOrderEnabled = orderEnabled;
        DataclassHashMode = hashMode;
    }

    public void SetMetaType(PyType metaType) => MetaType = metaType;

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (ReferenceEquals(MetaType, this))
        {
            return InvokeBuiltInType(arguments, span, context);
        }

        object instance = new PyInstance(this);
        if (TryGetMember("__new__", out var allocator))
        {
            if (allocator is not LythonRuntime.ICallable newCallable)
            {
                throw new LythonRuntimeException("TypeError", $"Class '{Name}' has a non-callable __new__.", span);
            }

            var newArguments = new CallArgumentValue[arguments.Length + 1];
            newArguments[0] = CallArgumentValue.Positional(this);
            Array.Copy(arguments, 0, newArguments, 1, arguments.Length);
            instance = newCallable.Invoke(newArguments, span, context);
        }

        if (instance is not PyInstance pyInstance || !pyInstance.Type.IsSubtypeOf(this))
        {
            return instance;
        }

        if (TryGetMember("__init__", out var initializer))
        {
            if (initializer is not IPyBindableCallable bindable)
            {
                throw new LythonRuntimeException("TypeError", $"Class '{Name}' has a non-callable __init__.", span);
            }

            var bound = (LythonRuntime.ICallable)bindable.Bind(pyInstance);
            var result = bound.Invoke(arguments, span, context);
            if (!ReferenceEquals(result, PyNone.Instance))
            {
                throw new LythonRuntimeException("TypeError", $"__init__ for class '{Name}' must return None.", span);
            }

            return pyInstance;
        }

        return pyInstance;
    }

    public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (ReferenceEquals(MetaType, this))
        {
            return InvokeBuiltInType(arguments, span, context);
        }

        object instance = new PyInstance(this);
        if (TryGetMember("__new__", out var allocator))
        {
            if (allocator is not LythonRuntime.ICallable newCallable)
            {
                throw new LythonRuntimeException("TypeError", $"Class '{Name}' has a non-callable __new__.", span);
            }

            var newArguments = new CallArgumentValue[arguments.Length + 1];
            newArguments[0] = CallArgumentValue.Positional(this);
            Array.Copy(arguments, 0, newArguments, 1, arguments.Length);
            instance = await newCallable.InvokeAsync(newArguments, span, context).ConfigureAwait(false);
        }

        if (instance is not PyInstance pyInstance || !pyInstance.Type.IsSubtypeOf(this))
        {
            return instance;
        }

        if (TryGetMember("__init__", out var initializer))
        {
            if (initializer is not IPyBindableCallable bindable)
            {
                throw new LythonRuntimeException("TypeError", $"Class '{Name}' has a non-callable __init__.", span);
            }

            var bound = (LythonRuntime.ICallable)bindable.Bind(pyInstance);
            var result = await bound.InvokeAsync(arguments, span, context).ConfigureAwait(false);
            if (!ReferenceEquals(result, PyNone.Instance))
            {
                throw new LythonRuntimeException("TypeError", $"__init__ for class '{Name}' must return None.", span);
            }

            return pyInstance;
        }

        return pyInstance;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<class '{Name}'>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    public override string ToString() => $"<class '{Name}'>";

    private object InvokeBuiltInType(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length == 1 && arguments[0].IsPositional)
        {
            return GetRuntimeTypeObject(arguments[0].Value, context, span);
        }

        throw new LythonRuntimeException(
            "TypeError",
            "type(value) supports exactly one argument in Lython; dynamic metaclass construction is unsupported.",
            span);
    }

    private static object GetRuntimeTypeObject(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return value switch
        {
            PyInstance instance => instance.Type,
            PyType type => type.MetaType ?? throw new LythonRuntimeException("TypeError", "Class has no metatype.", span),
            bool => GetBuiltinTypeObject(context, "bool", span),
            BigInteger or int => GetBuiltinTypeObject(context, "int", span),
            double => GetBuiltinTypeObject(context, "float", span),
            PyList => GetBuiltinTypeObject(context, "list", span),
            PyTuple => GetBuiltinTypeObject(context, "tuple", span),
            PyNamedTupleObject namedTuple => namedTuple.Type,
            PyDict => GetBuiltinTypeObject(context, "dict", span),
            PySet => GetBuiltinTypeObject(context, "set", span),
            PyString => GetBuiltinTypeObject(context, "str", span),
            PyBytes => GetBuiltinTypeObject(context, "bytes", span),
            PyPath => GetBuiltinTypeObject(context, "pathlib.Path", span),
            PyTimedelta => PyDateTimeOps.TimedeltaType,
            PyDate => PyDateTimeOps.DateType,
            PyTime => PyDateTimeOps.TimeType,
            PyDateTime => PyDateTimeOps.DateTimeType,
            PyTimezone => PyDateTimeOps.TimezoneType,
            LythonRuntime.StatisticsModule.PyNormalDist => LythonRuntime.StatisticsModule.NormalDistType,
            LythonRuntime.RandomModule.PyRandom => LythonRuntime.RandomModule.RandomType,
            _ => throw new LythonRuntimeException("TypeError", $"type(value) does not support values of type '{value.GetType().Name}' in Lython.", span)
        };
    }

    private static object GetBuiltinTypeObject(LythonRuntime.ExecutionContext context, string name, LythonSourceSpan span)
    {
        if (context.TryGetBuiltin(name, out var value))
        {
            return value;
        }

        throw new LythonRuntimeException("RuntimeError", $"Builtin type '{name}' is not available.", span);
    }

    private static IReadOnlyList<PyType> BuildMro(PyType self, IReadOnlyList<PyType> bases)
    {
        // C3 merges each base MRO with the declared base list. A candidate head
        // is eligible only when it occurs in no other remaining tail; advancing
        // offsets instead of modifying the source MROs preserves their identity.
        var result = new List<PyType> { self };
        if (bases.Count == 0)
        {
            return result;
        }

        var sequences = new IReadOnlyList<PyType>[bases.Count + 1];
        for (var i = 0; i < bases.Count; i++)
        {
            sequences[i] = bases[i].Mro;
        }

        sequences[^1] = bases;
        var offsets = new int[sequences.Length];

        while (true)
        {
            PyType? candidate = null;
            var hasRemainingSequence = false;
            for (var sequenceIndex = 0; sequenceIndex < sequences.Length; sequenceIndex++)
            {
                var sequence = sequences[sequenceIndex];
                var offset = offsets[sequenceIndex];
                if (offset == sequence.Count)
                {
                    continue;
                }

                hasRemainingSequence = true;
                var head = sequence[offset];
                var isValid = true;
                for (var otherIndex = 0; otherIndex < sequences.Length; otherIndex++)
                {
                    if (sequenceIndex == otherIndex)
                    {
                        continue;
                    }

                    var other = sequences[otherIndex];
                    for (var i = offsets[otherIndex] + 1; i < other.Count; i++)
                    {
                        if (ReferenceEquals(other[i], head))
                        {
                            isValid = false;
                            break;
                        }
                    }

                    if (!isValid)
                    {
                        break;
                    }
                }

                if (isValid)
                {
                    candidate = head;
                    break;
                }
            }

            if (!hasRemainingSequence)
            {
                return result;
            }

            if (candidate is null)
            {
                throw new InvalidOperationException(
                    $"Cannot create a consistent method resolution order (MRO) for bases {string.Join(", ", bases.Select(b => b.Name))}.");
            }

            result.Add(candidate);
            for (var sequenceIndex = 0; sequenceIndex < sequences.Length; sequenceIndex++)
            {
                var sequence = sequences[sequenceIndex];
                var offset = offsets[sequenceIndex];
                if (offset < sequence.Count && ReferenceEquals(sequence[offset], candidate))
                {
                    offsets[sequenceIndex]++;
                }
            }
        }
    }

    private void BindOwnedMembers()
    {
        foreach (var pair in _members)
        {
            if (pair.Value is IClassNamedMember named)
            {
                named.BindName(pair.Key);
            }

            if (pair.Value is IClassOwnedMember owned)
            {
                owned.BindOwner(this);
            }
        }
    }

    private static bool TryGetSetNameCallable(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out LythonRuntime.ICallable callable)
    {
        if (value is PyInstance instance && instance.Type.TryLookupInMro("__set_name__", 0, out var rawMethod, out _))
        {
            var bound = rawMethod switch
            {
                IPyBindableCallable bindable => bindable.Bind(instance),
                IPyDescriptor descriptor => descriptor.Get(instance, instance.Type, context, span),
                _ => rawMethod
            };

            if (bound is not LythonRuntime.ICallable resolved)
            {
                throw new LythonRuntimeException("TypeError", "__set_name__ must be callable.", span);
            }

            callable = resolved;
            return true;
        }

        callable = null;
        return false;
    }
}
