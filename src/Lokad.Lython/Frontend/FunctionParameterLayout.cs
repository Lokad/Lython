using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// Generated signatures already have the actual default values.
internal readonly record struct GeneratedFunctionParameter(string Name, FunctionParameterKind Kind, object? DefaultValue = null);

// One definition-owned names/index table describes binding order. Defaults and
// annotations remain with their own consumers, never this immutable signature.
internal sealed class FunctionParameterLayout
{
    internal readonly record struct DefaultSlot(int ParameterIndex, int KeywordIndex);
    internal readonly record struct KeywordDefault(PyString Name, int RecordPosition);

    private readonly string[] _names;
    private readonly record struct NativeKeyword(PyString Key, int Slot);
    private readonly NativeKeyword[] _nativeKeywords;
    private readonly KeywordDefault[] _keywordDefaults;
    internal PyDict.RecordKeys? PreparedKeywordDefaultKeys { get; private set; }

    internal void PrepareKeywordDefaultKeys(CancellationToken cancellationToken, LythonSourceSpan span)
    {
        PreparedKeywordDefaultKeys ??= PyDict.RecordKeys.PrepareDefinition(Defaults.Length - PositionalDefaultCount,
            this, static (parameters, index) => parameters.KeywordDefaultName(
                parameters.Defaults[parameters.PositionalDefaultCount + index].KeywordIndex),
            cancellationToken, span);
    }

    private FunctionParameterLayout(string[] names, Dictionary<string, int> indices, DefaultSlot[] defaults,
        int positionalDefaultCount,
        int positionalCount, int keywordOnlyCount, int positionalOnlyCount,
        int variadicListIndex, int variadicDictionaryIndex, CancellationToken cancellationToken)
    {
        _names = names;
        Defaults = defaults;
        PositionalDefaultCount = positionalDefaultCount;
        LayoutParameterIndex = indices;
        PositionalCount = positionalCount;
        KeywordOnlyCount = keywordOnlyCount;
        PositionalOnlyCount = positionalOnlyCount;
        NamedLayoutCount = positionalCount + keywordOnlyCount;
        VariadicListIndex = variadicListIndex;
        VariadicDictionaryIndex = variadicDictionaryIndex;

        var keywordCount = NamedLayoutCount - positionalOnlyCount;
        _keywordDefaults = keywordOnlyCount == 0 ? [] : new KeywordDefault[keywordOnlyCount];
        _nativeKeywords = keywordCount == 0 ? [] : new NativeKeyword[keywordCount];
        var nextDefault = positionalDefaultCount;
        for (var i = positionalOnlyCount; i < NamedLayoutCount; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i - positionalOnlyCount);
            var key = PyString.FromString(names[i]);
            _nativeKeywords[i - positionalOnlyCount] = new(key, i);
            if (i >= positionalCount)
            {
                var keyword = i - positionalCount;
                var position = nextDefault < defaults.Length && defaults[nextDefault].KeywordIndex == keyword
                    ? nextDefault++ - positionalDefaultCount : -1;
                _keywordDefaults[keyword] = new(key, position);
            }
        }
        if (keywordCount > 1)
            Array.Sort(_nativeKeywords, static (left, right) => PyString.CompareOrdinal(left.Key, right.Key));
        PythonTokenization.CheckCancellation(cancellationToken, 0);
    }

    public static FunctionParameterLayout Create(IReadOnlyList<GeneratedFunctionParameter> parameters,
        CancellationToken cancellationToken = default)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind, parameter.DefaultValue is not null),
            cancellationToken);

    internal static FunctionParameterLayout FromSyntax(IReadOnlyList<FunctionParameterSyntax> parameters,
        CancellationToken cancellationToken)
        => Create(parameters, static parameter => (parameter.Name, parameter.Kind, parameter.DefaultValue is not null),
            cancellationToken);

    private static FunctionParameterLayout Create<T>(IReadOnlyList<T> parameters,
        Func<T, (string Name, FunctionParameterKind Kind, bool HasDefault)> shape, CancellationToken cancellationToken)
    {
        var positionalCount = 0;
        var keywordOnlyCount = 0;
        var positionalOnlyCount = 0;
        var hasVariadicList = false;
        var hasVariadicDictionary = false;
        var positionalDefaults = 0;
        var keywordDefaults = 0;
        foreach (var item in parameters)
        {
            var parameter = shape(item);
            switch (parameter.Kind)
            {
                case FunctionParameterKind.PositionalOnly:
                    positionalCount++;
                    positionalOnlyCount++;
                    if (parameter.HasDefault) positionalDefaults++;
                    break;
                case FunctionParameterKind.Positional:
                    positionalCount++;
                    if (parameter.HasDefault) positionalDefaults++;
                    break;
                case FunctionParameterKind.KeywordOnly:
                    keywordOnlyCount++;
                    if (parameter.HasDefault) keywordDefaults++;
                    break;
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
        var defaults = positionalDefaults + keywordDefaults == 0 ? [] : new DefaultSlot[positionalDefaults + keywordDefaults];
        var positionalIndex = 0;
        var keywordOnlyIndex = positionalCount;
        var defaultIndex = 0;
        for (var i = 0; i < parameters.Count; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            var parameter = shape(parameters[i]);
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
            if (parameter.HasDefault)
                defaults[defaultIndex++] = new(i, parameter.Kind == FunctionParameterKind.KeywordOnly
                    ? slot - positionalCount : -1);
        }
        return new(names, indices, defaults, positionalDefaults, positionalCount, keywordOnlyCount, positionalOnlyCount,
            variadicListIndex, variadicDictionaryIndex, cancellationToken);
    }

    // Compiled metadata uses the coarse source estimate. Generated definitions
    // still admit this conservative estimate before constructing their tables.
    public static long EstimateBytes(IReadOnlyList<GeneratedFunctionParameter> parameters)
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

    internal KeywordDefault KeywordDefaultAt(int index) => _keywordDefaults[index];
    internal PyString KeywordDefaultName(int index) => KeywordDefaultAt(index).Name;
    internal DefaultSlot[] Defaults { get; }
    internal bool HasKeywordDefaults => Defaults.Length > PositionalDefaultCount;
    internal int DefaultStorageCount => (PositionalDefaultCount == 0 ? 0 : 1) + (HasKeywordDefaults ? 1 : 0);
    internal int PositionalDefaultCount { get; }

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
