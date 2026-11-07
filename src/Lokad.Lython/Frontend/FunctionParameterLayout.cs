namespace Lokad.Lython.Frontend;

// Parameter shape belongs to the definition. Defaults and closures belong to
// each created function, so they must never enter this shared metadata.
internal sealed class FunctionParameterLayout
{
    public FunctionParameterLayout(IReadOnlyList<LoweredFunctionParameter> parameters)
    {
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

        var positional = positionalCount == 0 ? [] : new LoweredFunctionParameter[positionalCount];
        var keywordOnly = keywordOnlyCount == 0 ? [] : new LoweredFunctionParameter[keywordOnlyCount];
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
        var names = layoutCount == 0 ? [] : new string[layoutCount];
        var indices = new Dictionary<string, int>(parameters.Count, StringComparer.Ordinal);
        var slot = 0;
        foreach (var parameter in positional)
        {
            indices[parameter.Name] = slot;
            names[slot++] = parameter.Name;
        }
        foreach (var parameter in keywordOnly)
        {
            indices[parameter.Name] = slot;
            names[slot++] = parameter.Name;
        }
        if (VariadicList is not null)
        {
            indices[VariadicList.Name] = slot;
            names[slot++] = VariadicList.Name;
        }
        if (VariadicDictionary is not null)
        {
            indices[VariadicDictionary.Name] = slot;
            names[slot] = VariadicDictionary.Name;
        }
        LayoutParameterNames = names;
        LayoutParameterIndex = indices;
    }

    // Compilation covers this metadata in its coarse source estimate. Native
    // generated definitions admit it explicitly before allocating the tables.
    public static long EstimateBytes(int parameterCount) => parameterCount == 0 ? 0 : 128L + 128L * parameterCount;

    public IReadOnlyList<LoweredFunctionParameter> PositionalParameters { get; }
    public IReadOnlyList<LoweredFunctionParameter> KeywordOnlyParameters { get; }
    public IReadOnlyList<string> LayoutParameterNames { get; }
    public IReadOnlyDictionary<string, int> LayoutParameterIndex { get; }
    public int NamedLayoutCount { get; }
    public int PositionalOnlyCount { get; }
    public LoweredFunctionParameter? VariadicList { get; }
    public LoweredFunctionParameter? VariadicDictionary { get; }
}
