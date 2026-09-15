using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class FunctionBindingPlan
{
    public FunctionBindingPlan(
        string callableName,
        PythonCallableKind callableKind,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyDictionary<string, object> defaultValues)
    {
        CallableName = callableName;
        CallableKind = callableKind;
        Parameters = parameters;
        DefaultValues = defaultValues;

        var positional = new List<LoweredFunctionParameter>(parameters.Count);
        var keywordOnly = new List<LoweredFunctionParameter>(parameters.Count);
        var named = new Dictionary<string, LoweredFunctionParameter>(parameters.Count, StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            switch (parameter.Kind)
            {
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
    // stay within these: a keyword equal to a variadic name is overflow,
    // exactly like a name the plan never declared.
    public int NamedLayoutCount { get; }

    public LoweredFunctionParameter? VariadicList { get; }

    public LoweredFunctionParameter? VariadicDictionary { get; }

    public IReadOnlyDictionary<string, object> DefaultValues { get; }
}
