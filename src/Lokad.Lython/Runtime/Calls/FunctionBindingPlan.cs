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
        // Two name maps and the positional/keyword/layout arrays are owned by
        // this plan. Admit their capacity before allocating, including working
        // lists; the estimate remains conservative until layouts are shared.
        var layoutBytes = parameters.Count == 0 ? 0 : 128L + 128L * parameters.Count;
        governor?.Reserve(layoutBytes, null);
        try
        {
            CallableName = callableName;
            CallableKind = callableKind;
            Parameters = parameters;

            var positional = new List<LoweredFunctionParameter>(parameters.Count);
            var keywordOnly = new List<LoweredFunctionParameter>(parameters.Count);
            var named = new Dictionary<string, LoweredFunctionParameter>(parameters.Count, StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                switch (parameter.Kind)
                {
                    case FunctionParameterKind.PositionalOnly:
                        positional.Add(parameter);
                        PositionalOnlyCount++;
                        break;
                    case FunctionParameterKind.Positional:
                        positional.Add(parameter);
                        named[parameter.Name] = parameter;
                        break;
                    case FunctionParameterKind.KeywordOnly:
                        keywordOnly.Add(parameter);
                        named[parameter.Name] = parameter;
                        break;
                    case FunctionParameterKind.VariadicList:
                        VariadicList = parameter;
                        break;
                    case FunctionParameterKind.VariadicDictionary:
                        VariadicDictionary = parameter;
                        break;
                }
            }

            PositionalParameters = positional.ToArray();
            KeywordOnlyParameters = keywordOnly.ToArray();
            NamedParameters = named;
            var layoutNames = new List<string>(parameters.Count);
            var layoutIndex = new Dictionary<string, int>(parameters.Count, StringComparer.Ordinal);
            foreach (var parameter in positional)
            {
                layoutIndex[parameter.Name] = layoutNames.Count;
                layoutNames.Add(parameter.Name);
            }

            foreach (var parameter in keywordOnly)
            {
                layoutIndex[parameter.Name] = layoutNames.Count;
                layoutNames.Add(parameter.Name);
            }

            NamedLayoutCount = layoutNames.Count;
            if (VariadicList is not null)
            {
                layoutIndex[VariadicList.Name] = layoutNames.Count;
                layoutNames.Add(VariadicList.Name);
            }

            if (VariadicDictionary is not null)
            {
                layoutIndex[VariadicDictionary.Name] = layoutNames.Count;
                layoutNames.Add(VariadicDictionary.Name);
            }

            LayoutParameterNames = layoutNames.ToArray();
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

    public IReadOnlyList<LoweredFunctionParameter> Parameters { get; }

    public IReadOnlyList<LoweredFunctionParameter> PositionalParameters { get; }

    public IReadOnlyList<LoweredFunctionParameter> KeywordOnlyParameters { get; }

    public IReadOnlyDictionary<string, LoweredFunctionParameter> NamedParameters { get; }

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
