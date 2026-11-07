using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class FunctionBindingPlan
{
    private readonly FunctionParameterLayout _layout;

    public FunctionBindingPlan(
        string callableName,
        PythonCallableKind callableKind,
        FunctionParameterLayout layout,
        IReadOnlyDictionary<string, object> defaultValues,
        LythonRuntime.ExecutionContext? context = null)
    {
        _layout = layout;
        CallableName = callableName;
        CallableKind = callableKind;
        var governor = context?.MemoryGovernor;
        var nameStorageBytes = KeywordOnlyParameters.Count == 0 ? 0 : 32L + 8L * KeywordOnlyParameters.Count;
        governor?.Reserve(nameStorageBytes, null);
        try { KeywordDefaultNames = KeywordOnlyParameters.Count == 0 ? [] : new PyString[KeywordOnlyParameters.Count]; }
        catch { governor?.ReleaseReserved(nameStorageBytes); throw; }
        governor?.Commit(nameStorageBytes);
        try
        {
            for (var i = 0; i < KeywordDefaultNames.Length; i++)
                KeywordDefaultNames[i] = context is null
                    ? PyString.FromString(KeywordOnlyParameters[i].Name)
                    : PyString.FromString(KeywordOnlyParameters[i].Name, context.MemoryGovernor, null);

            var firstDefault = PositionalParameters.Count;
            while (firstDefault > 0 && defaultValues.ContainsKey(PositionalParameters[firstDefault - 1].Name)) firstDefault--;
            var count = PositionalParameters.Count - firstDefault;
            if (count != 0)
                PositionalDefaults = context is null
                    ? new PyTuple(Enumerable.Range(firstDefault, count).Select(i => defaultValues[PositionalParameters[i].Name]))
                    : new PyTuple(Enumerable.Range(firstDefault, count).Select(i => defaultValues[PositionalParameters[i].Name]), context.MemoryGovernor);

            for (var i = 0; i < KeywordOnlyParameters.Count; i++)
            {
                if (!defaultValues.TryGetValue(KeywordOnlyParameters[i].Name, out var value)) continue;
                var dictionary = KeywordDefaultStorage;
                if (dictionary is null)
                {
                    dictionary = context is null ? new PyDict() : new PyDict(context.MemoryGovernor);
                    KeywordDefaults = dictionary;
                }
                dictionary.SetItem(KeywordDefaultNames[i], value);
            }
        }
        catch
        {
            // These values have not escaped the constructor. Refunding only
            // their new storage preserves funding for the supplied default values.
            if (governor is not null)
            {
                if (PositionalDefaults is { } positionalDefaults) governor.RefundUnpublishedValue(positionalDefaults);
                if (KeywordDefaults is { } keywordDefaults) governor.RefundUnpublishedValue(keywordDefaults);
                foreach (var name in KeywordDefaultNames)
                    if (name is not null) governor.RefundUnpublishedValue(name);
                governor.Release(nameStorageBytes);
            }
            throw;
        }
    }

    // Native generated definitions have no compiled owner. Admit their immutable
    // tables through successful default construction, preserving full rollback.
    public static FunctionBindingPlan CreateDynamic(string callableName, PythonCallableKind callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters, IReadOnlyDictionary<string, object> defaultValues,
        LythonRuntime.ExecutionContext? context = null)
    {
        var bytes = FunctionParameterLayout.EstimateBytes(parameters.Count);
        using var storage = context?.MemoryGovernor.ReserveTemporary(bytes, null);
        var result = new FunctionBindingPlan(callableName, callableKind,
            new FunctionParameterLayout(parameters), defaultValues, context);
        storage?.Commit(bytes);
        return result;
    }

    public string CallableName { get; }

    public PythonCallableKind CallableKind { get; }

    public IReadOnlyList<LoweredFunctionParameter> PositionalParameters => _layout.PositionalParameters;

    public IReadOnlyList<LoweredFunctionParameter> KeywordOnlyParameters => _layout.KeywordOnlyParameters;

    // Flat call-time layout: positional, then keyword-only, then variadic
    // names. Prepared once per definition and shared across functions; per-call
    // binding fills values in this order with presence bits instead of a
    // per-call name dictionary.
    public IReadOnlyList<string> LayoutParameterNames => _layout.LayoutParameterNames;

    public IReadOnlyDictionary<string, int> LayoutParameterIndex => _layout.LayoutParameterIndex;

    // Number of positional plus keyword-only slots. Keyword matching must
    // stay below this count and at or above PositionalOnlyCount: a keyword
    // equal to a positional-only or variadic name is overflow,
    // exactly like a name the plan never declared.
    public int NamedLayoutCount => _layout.NamedLayoutCount;

    public int PositionalOnlyCount => _layout.PositionalOnlyCount;

    public LoweredFunctionParameter? VariadicList => _layout.VariadicList;

    public LoweredFunctionParameter? VariadicDictionary => _layout.VariadicDictionary;

    public object? PositionalDefaults { get; private set; }
    public object? KeywordDefaults { get; private set; }
    public PyString[] KeywordDefaultNames { get; }

    public PyDict? KeywordDefaultStorage => KeywordDefaults switch
    {
        PyDict dictionary => dictionary,
        PyDefaultDict dictionary => dictionary.InnerDict,
        PyCounter dictionary => dictionary.InnerDict,
        _ => null,
    };

    public bool TryGetPositionalDefault(int parameterIndex, [MaybeNullWhen(false)] out object value)
    {
        if (PositionalDefaults is { } defaults && PyTupleLike.TryGetItems(defaults, out var items))
        {
            var index = parameterIndex - (PositionalParameters.Count - items.Count);
            if (index >= 0 && index < items.Count)
            {
                value = items[index];
                return true;
            }
        }
        value = null;
        return false;
    }

    public bool TryGetDefaultMetadata(string name, out object value)
    {
        value = name switch
        {
            "__defaults__" => PositionalDefaults ?? PyNone.Instance,
            "__kwdefaults__" => KeywordDefaults ?? PyNone.Instance,
            _ => PyNone.Instance,
        };
        return name is "__defaults__" or "__kwdefaults__";
    }

    public bool TrySetDefaultMetadata(string name, object value, LythonSourceSpan? span = null)
    {
        if (name == "__defaults__")
        {
            if (value is not PyNone && !PyTupleLike.TryGetItems(value, out _))
                throw new LythonRuntimeException("TypeError", "__defaults__ must be set to a tuple object", span);
            PositionalDefaults = value is PyNone ? null : value;
            return true;
        }
        if (name == "__kwdefaults__")
        {
            if (value is not (PyNone or PyDict or PyDefaultDict or PyCounter))
                throw new LythonRuntimeException("TypeError", "__kwdefaults__ must be set to a dict object", span);
            KeywordDefaults = value is PyNone ? null : value;
            return true;
        }
        return false;
    }
}
