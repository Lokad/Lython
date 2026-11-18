using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct FunctionDefaults(PyTuple? Positional, PyDict? Keywords)
{
    internal void RefundUnpublished(MemoryGovernor governor)
    {
        if (Positional is not null) governor.RefundUnpublishedValue(Positional);
        if (Keywords is not null) governor.RefundUnpublishedValue(Keywords);
    }
}

internal sealed class FunctionBindingPlan
{
    private readonly FunctionParameterLayout _layout;

    public FunctionBindingPlan(
        string callableName,
        PythonCallableKind callableKind,
        FunctionParameterLayout layout,
        FunctionDefaults defaults)
    {
        _layout = layout;
        CallableName = callableName;
        CallableKind = callableKind;
        PositionalDefaults = defaults.Positional;
        KeywordDefaults = defaults.Keywords;
    }

    private static FunctionDefaults CreateGeneratedDefaults(FunctionParameterLayout layout,
        IReadOnlyDictionary<string, object> defaultValues, LythonRuntime.ExecutionContext? context)
    {
        var governor = context?.MemoryGovernor;
        PyTuple? positional = null;
        PyDict? keywords = null;
        try
        {
            var firstDefault = layout.PositionalCount;
            while (firstDefault > 0 && defaultValues.ContainsKey(layout.NameAt(firstDefault - 1))) firstDefault--;
            var count = layout.PositionalCount - firstDefault;
            if (count != 0)
            {
                var items = Enumerable.Range(firstDefault, count).Select(i => defaultValues[layout.NameAt(i)]);
                positional = governor is null ? new PyTuple(items) : new PyTuple(items, governor);
            }

            for (var i = 0; i < layout.KeywordOnlyCount; i++)
            {
                if (!defaultValues.TryGetValue(layout.NameAt(layout.PositionalCount + i), out var value)) continue;
                keywords ??= governor is null ? new PyDict() : new PyDict(governor);
                keywords.SetItem(layout.KeywordDefaultName(i), value);
            }
            return new(positional, keywords);
        }
        catch
        {
            // These values have not escaped the constructor. Refunding only
            // their new storage preserves funding for the supplied default values.
            if (governor is not null) new FunctionDefaults(positional, keywords).RefundUnpublished(governor);
            throw;
        }
    }

    // Native generated definitions have no compiled owner. Admit their immutable
    // tables through successful default construction, preserving full rollback.
    public static FunctionBindingPlan CreateDynamic(string callableName, PythonCallableKind callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters, IReadOnlyDictionary<string, object> defaultValues,
        LythonRuntime.ExecutionContext? context = null)
    {
        var bytes = FunctionParameterLayout.EstimateBytes(parameters);
        using var storage = context?.MemoryGovernor.ReserveTemporary(bytes, null);
        var layout = FunctionParameterLayout.Create(parameters, context?.State.CancellationToken ?? default);
        var result = new FunctionBindingPlan(callableName, callableKind, layout, CreateGeneratedDefaults(layout, defaultValues, context));
        storage?.Commit(bytes);
        return result;
    }

    public string CallableName { get; }

    public PythonCallableKind CallableKind { get; }

    public int PositionalCount => _layout.PositionalCount;

    public int ParameterCount => _layout.ParameterCount;

    public string NameAt(int index) => _layout.NameAt(index);

    public int KeywordOnlyCount => _layout.KeywordOnlyCount;

    // Flat call-time layout: positional, then keyword-only, then variadic
    // names. Prepared once per definition and shared across functions; per-call
    // binding fills the final slots in this order; their unassigned sentinel
    // distinguishes omission without a parallel presence map or name dictionary.
    public IReadOnlyList<string> LayoutParameterNames => _layout.LayoutParameterNames;

    public IReadOnlyDictionary<string, int> LayoutParameterIndex => _layout.LayoutParameterIndex;

    // Number of positional plus keyword-only slots. Keyword matching must
    // stay below this count and at or above PositionalOnlyCount: a keyword
    // equal to a positional-only or variadic name is overflow,
    // exactly like a name the plan never declared.
    public int NamedLayoutCount => _layout.NamedLayoutCount;

    public int PositionalOnlyCount => _layout.PositionalOnlyCount;

    public int VariadicListIndex => _layout.VariadicListIndex;

    public int VariadicDictionaryIndex => _layout.VariadicDictionaryIndex;

    public object? PositionalDefaults { get; private set; }
    public object? KeywordDefaults { get; private set; }
    public PyString KeywordDefaultName(int index) => _layout.KeywordDefaultName(index);

    internal bool TryGetKeywordIndex(CallArgumentValue argument, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, out int index)
    {
        if (argument.NativeKeyword is { } key)
            return _layout.TryGetNativeKeywordIndex(key, context, span, out index);
        return LayoutParameterIndex.TryGetValue(argument.KeywordName, out index) &&
            index >= PositionalOnlyCount && index < NamedLayoutCount;
    }

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
            var index = parameterIndex - (PositionalCount - items.Count);
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
