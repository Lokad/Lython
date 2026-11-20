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

// Ordinary functions own this state directly. Generated callables can own a
// standalone plan. Binding keeps the live default objects in either case.
internal class FunctionBindingPlan
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

    internal bool TryGetKeywordDefault(PyDict dictionary, int index, [MaybeNullWhen(false)] out object value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var (name, position) = _layout.KeywordDefaultAt(index);
        return dictionary.TryGetDefinitionValue(name, position, out value, context, span);
    }

    internal bool TryGetKeywordIndex(CallArgumentValue argument, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, out int index)
    {
        if (argument.NativeKeyword is { } key)
            return TryGetKeywordIndex(key, context, span, out index);
        return LayoutParameterIndex.TryGetValue(argument.KeywordName, out index) &&
            index >= PositionalOnlyCount && index < NamedLayoutCount;
    }

    internal bool TryGetKeywordIndex(PyString key, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, out int index)
        => _layout.TryGetNativeKeywordIndex(key, context, span, out index);

    public PyDict? KeywordDefaultStorage => KeywordDefaults switch
    {
        PyDict dictionary => dictionary,
        PyDefaultDict dictionary => dictionary.InnerDict,
        PyCounter dictionary => dictionary.InnerDict,
        _ => null,
    };

    public bool TryGetPositionalDefault(int parameterIndex, out LythonRuntime.ExecutableSlot value)
    {
        var defaults = PositionalDefaults;
        if (defaults is PyTuple tuple) return tuple.TryReadTrailingSlot(PositionalCount - parameterIndex, out value);
        if (defaults is not null && PyTupleLike.TryGetItems(defaults, out var items))
        {
            var index = parameterIndex - (PositionalCount - items.Count);
            if (index >= 0 && index < items.Count)
            {
                value = LythonRuntime.ExecutableSlot.FromObject(items[index]);
                return true;
            }
        }
        value = default;
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
