using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using ExecutableSlot = Lokad.Lython.Runtime.LythonRuntime.ExecutableSlot;

namespace Lokad.Lython.Runtime;

internal readonly record struct FunctionDefaults(ExecutableSlot Positional, ExecutableSlot Keywords)
{
    internal bool InlinePositional => Positional.HasPrivatePositionalDefault;
    internal bool InlineKeyword => Keywords.HasPrivateKeywordDefault;

    internal void RefundUnpublished(MemoryGovernor governor)
    {
        var positional = Positional;
        var keywords = Keywords;
        try { positional.RetireTemporary(governor); }
        finally { keywords.RetireTemporary(governor); }
    }

    internal static long InlinePositionalBytes(ExecutableSlot value)
        => PyTuple.EstimateDefaultStorage(1) + PositionalScalarExcess(value.ScalarStorageBytes);

    internal static ExecutableSlot PreparePositionalValue(ExecutableSlot value,
        ExecutionState state, LythonSourceSpan span, out long extra)
    {
        extra = PositionalScalarExcess(value.ScalarStorageBytes);
        var governor = state.MemoryGovernor;
        if (extra != 0) governor.Reserve(extra, span);
        try
        {
            state.CheckExecution(span);
            value = PrepareInlineValue(value);
            if (extra != 0) governor.Commit(extra);
            return value;
        }
        catch
        {
            if (extra != 0) governor.ReleaseReserved(extra);
            throw;
        }
    }

    internal static ExecutableSlot PrepareInlineValue(ExecutableSlot value)
        => value.IsUnpublishedInteger ? value : ExecutableSlot.FromObject(LythonRuntime.RuntimeValue(value.Object));

    private static long PositionalScalarExcess(long bytes) => Math.Max(0, bytes - RuntimeMemoryEstimates.ScalarStorageBytes);
}

// Ordinary functions own this state directly. Generated callables can own a
// standalone plan. Binding keeps the live default objects in either case.
internal abstract class FunctionBindingPlan
{
    private readonly FunctionParameterLayout _layout;
    private ExecutableSlot _positionalDefaults;
    private ExecutableSlot _keywordDefaults;

    protected FunctionBindingPlan(FunctionParameterLayout layout, FunctionDefaults defaults)
    {
        _layout = layout;
        _positionalDefaults = defaults.Positional;
        _keywordDefaults = defaults.Keywords;
    }

    public abstract string CallableName { get; }

    public abstract PythonCallableKind CallableKind { get; }

    public int PositionalCount => _layout.PositionalCount;
    internal bool SupportsPositionalBinding => _layout.SupportsPositionalBinding;

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

    internal bool SupportsPreparedPositionalDefaults => _positionalDefaults.Reference is null or PyTuple ||
        _positionalDefaults.HasPrivatePositionalDefault;
    public PyString KeywordDefaultName(int index) => _layout.KeywordDefaultName(index);

    internal bool HasPreparedOmittedArguments(int supplied)
        => (KeywordOnlyCount == 0 ||
                (_keywordDefaults.HasPrivateKeywordDefault || _keywordDefaults.Reference is object?[]) &&
                _layout.Defaults.Length - _layout.PositionalDefaultCount == KeywordOnlyCount) &&
            supplied <= PositionalCount &&
            supplied >= PositionalCount - (_positionalDefaults.HasPrivatePositionalDefault ? 1 : (_positionalDefaults.Reference as PyTuple)?.Count ?? 0);

    internal bool TryReadPreparedDefaultInteger(int index, out long value)
    {
        if (index == PositionalCount - 1 && _positionalDefaults.HasPrivatePositionalDefault)
            return _positionalDefaults.ReadDefaultOperand().TryGetInteger(out value);
        if (index >= PositionalCount && _keywordDefaults.HasPrivateKeywordDefault)
            return _keywordDefaults.ReadDefaultOperand().TryGetInteger(out value);
        return ReadPreparedDefault(index).TryGetInteger(out value);
    }

    internal LythonRuntime.ExecutableSlot ReadPreparedDefault(int index)
    {
        if (index >= PositionalCount)
        {
            var keywordPosition = _layout.KeywordDefaultAt(index - PositionalCount).RecordPosition;
            var stored = _keywordDefaults.HasPrivateKeywordDefault ? _keywordDefaults.PublishInlineDefault()
                : ((object?[])_keywordDefaults.Reference!)[keywordPosition];
            return LythonRuntime.ExecutableSlot.FromObject(stored);
        }
        TryGetPositionalDefault(index, out var slot);
        return slot;
    }

    internal bool TryFillPreparedKeywordDefaults(Span<LythonRuntime.ExecutableSlot> bound,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var storage = _keywordDefaults.Reference;
        var inline = _keywordDefaults.HasPrivateKeywordDefault;
        if (!inline && storage is not object?[]) return false;
        // The original slots cannot invoke guest code. Borrow them after
        // supplied-argument binding, which can replace defaults on reentry.
        for (var k = 0; k < KeywordOnlyCount; k++)
        {
            ref var target = ref bound[PositionalCount + k];
            if (!target.IsUnassigned) continue;
            context.CheckExecution(span);
            var position = _layout.KeywordDefaultAt(k).RecordPosition;
            if (position < 0)
                throw CallErrors.MissingArgument(CallableKind, CallableName, NameAt(PositionalCount + k), span);
            target.Object = (inline ? _keywordDefaults.PublishInlineDefault() : ((object?[])storage!)[position]).RequireNotNull();
        }
        return true;
    }

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

    public PyDict? KeywordDefaultStorage => _keywordDefaults.Reference switch
    {
        PyDict dictionary when !_keywordDefaults.HasPrivateKeywordDefault => dictionary,
        PyDefaultDict dictionary when !_keywordDefaults.HasPrivateKeywordDefault => dictionary.InnerDict,
        PyCounter dictionary when !_keywordDefaults.HasPrivateKeywordDefault => dictionary.InnerDict,
        _ => null,
    };

    public bool TryGetPositionalDefault(int parameterIndex, out LythonRuntime.ExecutableSlot value)
    {
        var distance = PositionalCount - parameterIndex;
        if (_positionalDefaults.HasPrivatePositionalDefault)
        {
            // The generic binder can alias the default, including across calls.
            // Publish it in its owner before copying a slot to the invocation.
            value = distance == 1 ? ExecutableSlot.FromObject(_positionalDefaults.PublishInlineDefault()) : default;
            return distance == 1;
        }
        var defaults = _positionalDefaults.Reference;
        if (defaults is PyTuple tuple) return tuple.TryReadTrailingSlot(distance, out value);
        if (defaults is not null && PyTupleLike.TryGetItems(defaults, out var items))
        {
            var index = items.Count - distance;
            if (index >= 0 && index < items.Count)
            {
                value = LythonRuntime.ExecutableSlot.FromObject(items[index]);
                return true;
            }
        }
        value = default;
        return false;
    }

    public bool TryGetDefaultMetadata(string name, LythonRuntime.ExecutionContext context, out object value)
    {
        value = name switch
        {
            "__defaults__" => ReadPositionalDefaults(context),
            "__kwdefaults__" => ReadKeywordDefaults(context),
            _ => PyNone.Instance,
        };
        return name is "__defaults__" or "__kwdefaults__";
    }

    private object ReadPositionalDefaults(LythonRuntime.ExecutionContext context)
    {
        if (_positionalDefaults.HasPrivatePositionalDefault)
        {
            var span = PyStructuralGuard.AmbientSpan;
            context.CheckExecution(span);
            var value = _positionalDefaults.PublishInlineDefault();
            // The reservation predates evaluation. Transfer it to the
            // canonical tuple without a second admission or a value copy.
            _positionalDefaults.Object = PyTuple.CreateAdmittedSingletonDefault(value, context.MemoryGovernor,
                span, FunctionDefaults.InlinePositionalBytes(_positionalDefaults.ReadDefaultOperand()));
        }
        return _positionalDefaults.Reference ?? PyNone.Instance;
    }

    private object ReadKeywordDefaults(LythonRuntime.ExecutionContext context)
    {
        if (_keywordDefaults.HasPrivateKeywordDefault)
        {
            var span = PyStructuralGuard.AmbientSpan;
            context.CheckExecution(span);
            _keywordDefaults.Object = _layout.PreparedKeywordDefaultKeys!.AdoptPreparedValue(
                _keywordDefaults.PublishInlineDefault(), context.MemoryGovernor, span);
        }
        else if (_keywordDefaults.Reference is object?[] values)
        {
            var span = PyStructuralGuard.AmbientSpan;
            context.CheckExecution(span);
            _keywordDefaults.Object = _layout.PreparedKeywordDefaultKeys!.AdoptPreparedValues(values, context.MemoryGovernor, span);
        }
        return _keywordDefaults.Reference ?? PyNone.Instance;
    }

    public bool TrySetDefaultMetadata(string name, object value, MemoryGovernor governor, LythonSourceSpan? span = null)
    {
        if (name == "__defaults__")
        {
            if (value is not PyNone && !PyTupleLike.TryGetItems(value, out _))
                throw new LythonRuntimeException("TypeError", "__defaults__ must be set to a tuple object", span);
            // A prior call can retain the original value. Keep its admission
            // with the execution owner, just as for an observed tuple.
            _positionalDefaults.Object = value is PyNone ? null : value;
            return true;
        }
        if (name == "__kwdefaults__")
        {
            if (value is not (PyNone or PyDict or PyDefaultDict or PyCounter))
                throw new LythonRuntimeException("TypeError", "__kwdefaults__ must be set to a dict object", span);
            var previous = _keywordDefaults;
            _keywordDefaults.Object = value is PyNone ? null : value;
            if (previous.HasPrivateKeywordDefault)
            {
                // A call can retain an observed scalar without exposing its
                // dictionary. Refund the backing and keep its scalar charge.
                governor.Release(PyDict.RecordKeys.SinglePreparedValueBytes - RuntimeMemoryEstimates.EstimateNumericSlotBytes(previous.Reference));
            }
            else if (previous.Reference is object?[] values) PyDict.RecordKeys.RefundPreparedValues(values, governor);
            return true;
        }
        return false;
    }
}

// Generated methods share common parameter layouts across declaring types.
internal sealed class GeneratedFunctionBindingPlan(string name, FunctionParameterLayout layout, FunctionDefaults defaults)
    : FunctionBindingPlan(layout, defaults)
{
    public override string CallableName { get; } = name;
    public override PythonCallableKind CallableKind => PythonCallableKind.Function;
}
