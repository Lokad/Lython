namespace Lokad.Lython.Frontend;

// One definition-owned names/index table describes binding order. Defaults and
// annotations remain with their own consumers, never this immutable signature.
internal sealed class FunctionParameterLayout
{
    private readonly string[] _names;

    private FunctionParameterLayout(string[] names, Dictionary<string, int> indices,
        int positionalCount, int keywordOnlyCount, int positionalOnlyCount,
        int variadicListIndex, int variadicDictionaryIndex)
    {
        _names = names;
        LayoutParameterIndex = indices;
        PositionalCount = positionalCount;
        KeywordOnlyCount = keywordOnlyCount;
        PositionalOnlyCount = positionalOnlyCount;
        NamedLayoutCount = positionalCount + keywordOnlyCount;
        VariadicListIndex = variadicListIndex;
        VariadicDictionaryIndex = variadicDictionaryIndex;
    }

    public static FunctionParameterLayout Create(IReadOnlyList<LoweredFunctionParameter> parameters)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind));

    internal static FunctionParameterLayout FromSyntax(IReadOnlyList<FunctionParameterSyntax> parameters)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind));

    private static FunctionParameterLayout Create<T>(IReadOnlyList<T> parameters,
        Func<T, (string Name, FunctionParameterKind Kind)> shape)
    {
        var positionalCount = 0;
        var keywordOnlyCount = 0;
        var positionalOnlyCount = 0;
        var hasVariadicList = false;
        var hasVariadicDictionary = false;
        foreach (var item in parameters)
        {
            switch (shape(item).Kind)
            {
                case FunctionParameterKind.PositionalOnly:
                    positionalCount++;
                    positionalOnlyCount++;
                    break;
                case FunctionParameterKind.Positional: positionalCount++; break;
                case FunctionParameterKind.KeywordOnly: keywordOnlyCount++; break;
                case FunctionParameterKind.VariadicList: hasVariadicList = true; break;
                case FunctionParameterKind.VariadicDictionary: hasVariadicDictionary = true; break;
                default: throw new InvalidOperationException("Unknown function parameter kind.");
            }
        }

        var namedCount = positionalCount + keywordOnlyCount;
        var variadicListIndex = hasVariadicList ? namedCount : -1;
        var variadicDictionaryIndex = hasVariadicDictionary ? namedCount + (hasVariadicList ? 1 : 0) : -1;
        var layoutCount = namedCount + (hasVariadicList ? 1 : 0) + (hasVariadicDictionary ? 1 : 0);
        var names = layoutCount == 0 ? [] : new string[layoutCount];
        var indices = new Dictionary<string, int>(parameters.Count, StringComparer.Ordinal);
        var positionalIndex = 0;
        var keywordOnlyIndex = positionalCount;
        foreach (var item in parameters)
        {
            var parameter = shape(item);
            var slot = parameter.Kind switch
            {
                FunctionParameterKind.PositionalOnly or FunctionParameterKind.Positional => positionalIndex++,
                FunctionParameterKind.KeywordOnly => keywordOnlyIndex++,
                FunctionParameterKind.VariadicList => variadicListIndex,
                FunctionParameterKind.VariadicDictionary => variadicDictionaryIndex,
                _ => throw new InvalidOperationException("Unknown function parameter kind."),
            };
            names[slot] = parameter.Name;
            indices[parameter.Name] = slot;
        }
        return new(names, indices, positionalCount, keywordOnlyCount, positionalOnlyCount,
            variadicListIndex, variadicDictionaryIndex);
    }

    // Compiled metadata uses the coarse source estimate. Generated definitions
    // still admit this conservative estimate before constructing their tables.
    public static long EstimateBytes(int parameterCount) => parameterCount == 0 ? 0 : 128L + 128L * parameterCount;

    public int PositionalCount { get; }
    public int KeywordOnlyCount { get; }
    public int ParameterCount => _names.Length;
    public string NameAt(int index) => _names[index];
    public IReadOnlyList<string> LayoutParameterNames => _names;
    public IReadOnlyDictionary<string, int> LayoutParameterIndex { get; }
    public int NamedLayoutCount { get; }
    public int PositionalOnlyCount { get; }
    public int VariadicListIndex { get; }
    public int VariadicDictionaryIndex { get; }
}
