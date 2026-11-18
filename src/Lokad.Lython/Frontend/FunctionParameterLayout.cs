using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// One definition-owned names/index table describes binding order. Defaults and
// annotations remain with their own consumers, never this immutable signature.
internal sealed class FunctionParameterLayout
{
    private readonly string[] _names;
    private readonly record struct NativeKeyword(PyString Key, int Slot);
    private readonly NativeKeyword[] _nativeKeywords;
    private readonly PyString[] _keywordDefaultNames;

    private FunctionParameterLayout(string[] names, Dictionary<string, int> indices,
        int positionalCount, int keywordOnlyCount, int positionalOnlyCount,
        int variadicListIndex, int variadicDictionaryIndex, CancellationToken cancellationToken)
    {
        _names = names;
        LayoutParameterIndex = indices;
        PositionalCount = positionalCount;
        KeywordOnlyCount = keywordOnlyCount;
        PositionalOnlyCount = positionalOnlyCount;
        NamedLayoutCount = positionalCount + keywordOnlyCount;
        VariadicListIndex = variadicListIndex;
        VariadicDictionaryIndex = variadicDictionaryIndex;

        var keywordCount = NamedLayoutCount - positionalOnlyCount;
        _keywordDefaultNames = keywordOnlyCount == 0 ? [] : new PyString[keywordOnlyCount];
        _nativeKeywords = keywordCount == 0 ? [] : new NativeKeyword[keywordCount];
        for (var i = positionalOnlyCount; i < NamedLayoutCount; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i - positionalOnlyCount);
            var key = PyString.FromString(names[i]);
            _nativeKeywords[i - positionalOnlyCount] = new(key, i);
            if (i >= positionalCount) _keywordDefaultNames[i - positionalCount] = key;
        }
        if (keywordCount > 1)
            Array.Sort(_nativeKeywords, static (left, right) => PyString.CompareOrdinal(left.Key, right.Key));
        PythonTokenization.CheckCancellation(cancellationToken, 0);
    }

    public static FunctionParameterLayout Create(IReadOnlyList<LoweredFunctionParameter> parameters,
        CancellationToken cancellationToken = default)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind), cancellationToken);

    internal static FunctionParameterLayout FromSyntax(IReadOnlyList<FunctionParameterSyntax> parameters,
        CancellationToken cancellationToken)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind), cancellationToken);

    private static FunctionParameterLayout Create<T>(IReadOnlyList<T> parameters,
        Func<T, (string Name, FunctionParameterKind Kind)> shape, CancellationToken cancellationToken)
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
            variadicListIndex, variadicDictionaryIndex, cancellationToken);
    }

    // Compiled metadata uses the coarse source estimate. Generated definitions
    // still admit this conservative estimate before constructing their tables.
    public static long EstimateBytes(IReadOnlyList<LoweredFunctionParameter> parameters)
    {
        if (parameters.Count == 0) return 0;
        var bytes = 256L + 256L * parameters.Count;
        foreach (var parameter in parameters)
            if (parameter.Kind is FunctionParameterKind.Positional or FunctionParameterKind.KeywordOnly)
                bytes = checked(bytes + 3L * parameter.Name.Length);
        return bytes;
    }

    internal bool TryGetNativeKeywordIndex(PyString key, LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        out int index)
    {
        var lower = 0;
        var upper = _nativeKeywords.Length;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var entry = _nativeKeywords[middle];
            var order = PyString.CompareOrdinal(key, entry.Key, context, span);
            if (order == 0)
            {
                index = entry.Slot;
                return true;
            }
            if (order < 0) upper = middle;
            else lower = middle + 1;
        }
        index = -1;
        return false;
    }

    internal PyString KeywordDefaultName(int index) => _keywordDefaultNames[index];

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
