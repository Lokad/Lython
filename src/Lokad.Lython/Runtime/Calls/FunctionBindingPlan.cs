using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class FunctionBindingPlan
{
    public FunctionBindingPlan(
        string callableName,
        PythonCallableKind callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyDictionary<string, object> defaultValues,
        LythonRuntime.ExecutionContext? context = null)
    {
        var governor = context?.MemoryGovernor;
        // The slot map and positional/keyword/layout arrays are owned by
        // this plan. Admit conservative capacity and construction overlap before
        // allocating, even though final arrays no longer need working lists.
        var layoutBytes = parameters.Count == 0 ? 0 : 128L + 128L * parameters.Count;
        governor?.Reserve(layoutBytes, null);
        try
        {
            CallableName = callableName;
            CallableKind = callableKind;

            var positionalCount = 0;
            var keywordOnlyCount = 0;
            foreach (var parameter in parameters)
            {
                switch (parameter.Kind)
                {
                    case FunctionParameterKind.PositionalOnly:
                        positionalCount++;
                        PositionalOnlyCount++;
                        break;
                    case FunctionParameterKind.Positional:
                        positionalCount++;
                        break;
                    case FunctionParameterKind.KeywordOnly:
                        keywordOnlyCount++;
                        break;
                    case FunctionParameterKind.VariadicList:
                        VariadicList = parameter;
                        break;
                    case FunctionParameterKind.VariadicDictionary:
                        VariadicDictionary = parameter;
                        break;
                }
            }

            LoweredFunctionParameter[] positional = positionalCount == 0 ? [] : new LoweredFunctionParameter[positionalCount];
            LoweredFunctionParameter[] keywordOnly = keywordOnlyCount == 0 ? [] : new LoweredFunctionParameter[keywordOnlyCount];
            var positionalIndex = 0;
            var keywordOnlyIndex = 0;
            foreach (var parameter in parameters)
            {
                if (parameter.Kind is FunctionParameterKind.PositionalOnly or FunctionParameterKind.Positional)
                    positional[positionalIndex++] = parameter;
                else if (parameter.Kind == FunctionParameterKind.KeywordOnly)
                    keywordOnly[keywordOnlyIndex++] = parameter;
            }

            PositionalParameters = positional;
            KeywordOnlyParameters = keywordOnly;
            NamedLayoutCount = positionalCount + keywordOnlyCount;
            var layoutCount = NamedLayoutCount + (VariadicList is null ? 0 : 1) + (VariadicDictionary is null ? 0 : 1);
            var layoutNames = layoutCount == 0 ? [] : new string[layoutCount];
            var layoutIndex = new Dictionary<string, int>(parameters.Count, StringComparer.Ordinal);
            var slot = 0;
            foreach (var parameter in positional)
            {
                layoutIndex[parameter.Name] = slot;
                layoutNames[slot++] = parameter.Name;
            }

            foreach (var parameter in keywordOnly)
            {
                layoutIndex[parameter.Name] = slot;
                layoutNames[slot++] = parameter.Name;
            }

            if (VariadicList is not null)
            {
                layoutIndex[VariadicList.Name] = slot;
                layoutNames[slot++] = VariadicList.Name;
            }

            if (VariadicDictionary is not null)
            {
                layoutIndex[VariadicDictionary.Name] = slot;
                layoutNames[slot++] = VariadicDictionary.Name;
            }

            LayoutParameterNames = layoutNames;
            LayoutParameterIndex = layoutIndex;

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
            governor?.Commit(layoutBytes);
        }
        catch
        {
            governor?.ReleaseReserved(layoutBytes);
            throw;
        }
    }

    public string CallableName { get; }

    public PythonCallableKind CallableKind { get; }

    public IReadOnlyList<LoweredFunctionParameter> PositionalParameters { get; }

    public IReadOnlyList<LoweredFunctionParameter> KeywordOnlyParameters { get; }

    // Flat call-time layout: positional, then keyword-only, then variadic
    // names. Built once per plan and shared across invocations; per-call
    // binding fills values in this order with presence bits instead of a
    // per-call name dictionary.
    public IReadOnlyList<string> LayoutParameterNames { get; }

    public IReadOnlyDictionary<string, int> LayoutParameterIndex { get; }

    // Number of positional plus keyword-only slots. Keyword matching must
    // stay below this count and at or above PositionalOnlyCount: a keyword
    // equal to a positional-only or variadic name is overflow,
    // exactly like a name the plan never declared.
    public int NamedLayoutCount { get; }

    public int PositionalOnlyCount { get; }

    public LoweredFunctionParameter? VariadicList { get; }

    public LoweredFunctionParameter? VariadicDictionary { get; }

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
