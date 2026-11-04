namespace Lokad.Lython.Frontend;

internal readonly record struct AbstractSequenceLengthBounds(
    int? MinimumLength,
    int? MaximumLength,
    bool IsImpossible)
{
    public AbstractSequenceLengthBounds(int? MinimumLength, int? MaximumLength)
        : this(MinimumLength, MaximumLength, false)
    {
    }

    public static AbstractSequenceLengthBounds Exact(int length) => new(length, length);

    public static AbstractSequenceLengthBounds Impossible => new(null, null, IsImpossible: true);

    public static AbstractSequenceLengthBounds Union(
        AbstractSequenceLengthBounds left,
        AbstractSequenceLengthBounds right)
    {
        if (left.IsImpossible)
        {
            return right;
        }

        if (right.IsImpossible)
        {
            return left;
        }

        return new AbstractSequenceLengthBounds(
            left.MinimumLength is int leftMinimum && right.MinimumLength is int rightMinimum
                ? Math.Min(leftMinimum, rightMinimum)
                : null,
            left.MaximumLength is int leftMaximum && right.MaximumLength is int rightMaximum
                ? Math.Max(leftMaximum, rightMaximum)
                : null);
    }
}

internal readonly struct AbstractValueResolution
{
    private readonly AbstractValue _value;

    private AbstractValueResolution(AbstractValue value)
    {
        _value = value;
        IsResolved = true;
    }

    public static AbstractValueResolution Unresolved => default;

    public static AbstractValueResolution Resolved(AbstractValue value) => new(value);

    public bool IsResolved { get; }

    public bool TryGetValue(out AbstractValue value)
    {
        value = _value;
        return IsResolved;
    }
}

internal sealed class AbstractState
{
    // Resolutions belong to this binding state and this exact immutable syntax
    // node. Every mutation clears the cache; no version or structural hash is
    // needed, including for recursively nested syntax records.
    private readonly Dictionary<ExpressionSyntax, AbstractValueResolution> _abstractValueCache;
    private readonly Dictionary<string, AbstractSequenceLengthBounds> _sequenceLengths;
    private readonly Dictionary<string, AbstractValue> _values;
    private bool _containerFactsInvalidated;

    internal StaticAnalysisContext? AnalysisContext { get; }

    public AbstractState(StaticAnalysisContext? analysisContext = null)
    {
        AnalysisContext = analysisContext;
        _values = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        _sequenceLengths = new Dictionary<string, AbstractSequenceLengthBounds>(StringComparer.Ordinal);
        _abstractValueCache = new(ReferenceEqualityComparer.Instance);
    }

    private AbstractState(
        Dictionary<string, AbstractValue> values,
        Dictionary<string, AbstractSequenceLengthBounds> sequenceLengths,
        StaticAnalysisContext? analysisContext)
    {
        AnalysisContext = analysisContext;
        _values = values;
        _sequenceLengths = sequenceLengths;
        _abstractValueCache = new(ReferenceEqualityComparer.Instance);
    }

    public bool TryGet(string name, out AbstractValue value) => _values.TryGetValue(name, out value);

    public IReadOnlyCollection<string> Names => _values.Keys;

    public void Set(string name, AbstractValue value)
    {
        _values[name] = value;
        if (AbstractValue.TryGetExactSequenceLength(value, out var length))
        {
            _sequenceLengths[name] = AbstractSequenceLengthBounds.Exact(length);
        }
        else
        {
            _sequenceLengths.Remove(name);
        }

        InvalidateCachedFacts();
    }

    public bool TryGetSequenceLength(string name, out AbstractSequenceLengthBounds bounds)
        => _sequenceLengths.TryGetValue(name, out bounds);

    public void SetSequenceLength(string name, AbstractSequenceLengthBounds bounds)
    {
        _sequenceLengths[name] = bounds;
        InvalidateCachedFacts();
    }

    public void Remove(string name)
    {
        if (_values.Remove(name) | _sequenceLengths.Remove(name))
        {
            InvalidateCachedFacts();
        }
    }

    public bool IsKnownMutableSequence(string name)
        => _values.TryGetValue(name, out var value) &&
           value.Kind is AbstractValueKind.List or AbstractValueKind.ListType or AbstractValueKind.CollectionsDeque;

    public void InvalidateDictionaryFacts()
    {
        _containerFactsInvalidated = true;
        // Mutable mapping aliases are not tracked. Calls and mutations can
        // change any reachable mapping, including one captured by a function.
        foreach (var name in _values.Keys.ToArray())
        {
            if (MatchesFactQuery(_values[name], FactQuery.Dictionary, AnalysisContext))
            {
                Remove(name);
            }
        }
    }

    public void InvalidateMutableSequenceFacts()
    {
        _containerFactsInvalidated = true;
        var changed = false;
        foreach (var name in _values.Keys.ToArray())
        {
            var value = _values[name];
            var remainingTupleWork = Parser.MaxNestingDepth;
            var widened = ForgetMutableSequenceContents(value, ref remainingTupleWork);
            if (widened.Kind != value.Kind || !widened.HasSamePayload(value))
            {
                _values[name] = widened;
                changed = true;
            }

            if (value.Kind is AbstractValueKind.List or AbstractValueKind.ListType or AbstractValueKind.CollectionsDeque)
            {
                changed |= _sequenceLengths.Remove(name);
            }
        }

        if (changed)
        {
            InvalidateCachedFacts();
        }
    }

    private static AbstractValue ForgetMutableSequenceContents(AbstractValue value, ref int remainingTupleWork)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.List:
            case AbstractValueKind.ListType:
                // An alias or callback may replace every item, including its type.
                return value.Kind == AbstractValueKind.ListType && value.RequireNestedValue().Kind == AbstractValueKind.Unknown
                    ? value
                    : AbstractValue.ListOf(AbstractValue.Unknown(value.Span), value.Span);

            case AbstractValueKind.Tuple:
                // Repeated assignments can build deeper facts than parsed syntax.
                // Bound normalization work and recursion; only static precision is lost.
                if (remainingTupleWork == 0) return AbstractValue.Unknown(value.Span);
                remainingTupleWork--;
                var items = value.RequireSequenceItems();
                AbstractValue[]? replacement = null;
                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    var widened = ForgetMutableSequenceContents(item, ref remainingTupleWork);
                    if (widened.Kind != item.Kind || !widened.HasSamePayload(item))
                    {
                        replacement ??= items.ToArray();
                        replacement[index] = widened;
                    }
                }
                return replacement is null ? value : AbstractValue.Tuple(replacement, value.Span);

            case AbstractValueKind.MaybeNone:
                if (remainingTupleWork == 0) return AbstractValue.Unknown(value.Span);
                remainingTupleWork--;
                var nested = value.RequireNestedValue();
                var widenedNested = ForgetMutableSequenceContents(nested, ref remainingTupleWork);
                return widenedNested.Kind == nested.Kind && widenedNested.HasSamePayload(nested)
                    ? value
                    : AbstractValue.MaybeNone(widenedNested, value.Span);

            default:
                return value;
        }
    }

    public void ApplyContainerInvalidation(AbstractState other)
    {
        // A lexical child can mutate an outer container even when its own
        // binding shadows that container. Carry the effect, not its bindings.
        if (other._containerFactsInvalidated)
        {
            InvalidateDictionaryFacts();
            InvalidateMutableSequenceFacts();
        }
    }

    public AbstractState CloneForDeferredBody()
    {
        var clone = Clone();
        // Captured collections can change before a def or lambda runs. Forget
        // their contents, including through tuples and other captured values;
        // new local collections will establish their own facts in the body.
        foreach (var (name, value) in _values)
        {
            if (ContainsMutableCollectionFacts(value, AnalysisContext))
            {
                clone.Remove(name);
            }
        }
        return clone;
    }

    public static bool ContainsMutableCollectionFacts(AbstractValue value, StaticAnalysisContext? context)
        => MatchesFactQuery(value, FactQuery.MutableCollection, context);

    public static bool IsCallbackFreeValue(AbstractValue value, StaticAnalysisContext? context)
        => !MatchesFactQuery(value, FactQuery.Callback, context);

    private enum FactQuery { Dictionary, MutableCollection, Callback }

    private static bool MatchesFactQuery(AbstractValue value, FactQuery query, StaticAnalysisContext? context)
    {
        // These are temporary compiler facts, never runtime guest roots. Shared
        // tuples can describe an exponentially larger tree than their source;
        // inspect each payload once, without consuming the CLR call stack.
        Stack<AbstractValue>? pending = null;
        HashSet<object>? visited = null;
        var callbacks = query == FactQuery.Callback;
        while (true)
        {
            CheckFactQuery(context);
            if (callbacks && value.Kind is not (
                AbstractValueKind.String or AbstractValueKind.StringType or
                AbstractValueKind.Bytes or AbstractValueKind.BytesType or
                AbstractValueKind.Integer or AbstractValueKind.IntegerType or
                AbstractValueKind.Float or AbstractValueKind.FloatType or
                AbstractValueKind.Boolean or AbstractValueKind.BooleanType or
                AbstractValueKind.None or AbstractValueKind.List or AbstractValueKind.Tuple or
                AbstractValueKind.Set or AbstractValueKind.Dict))
                return true;
            if (!callbacks && (value.Kind == AbstractValueKind.Dict || query == FactQuery.MutableCollection && value.Kind is
                AbstractValueKind.List or AbstractValueKind.ListType or AbstractValueKind.Set or
                AbstractValueKind.SetType or AbstractValueKind.CollectionsDeque or
                AbstractValueKind.CollectionsDefaultDict or AbstractValueKind.CollectionsCounter or
                AbstractValueKind.CollectionsChainMap))
                return true;

            if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set or
                AbstractValueKind.ListType or AbstractValueKind.MaybeNone or AbstractValueKind.UserInstance or
                AbstractValueKind.UserClass or AbstractValueKind.Function or AbstractValueKind.Dict)
            {
                visited ??= new(ReferenceEqualityComparer.Instance);
                var identity = value.Kind == AbstractValueKind.Function
                    ? (object)value.RequireFunctionSummary().CapturedBindings : value.PayloadIdentity;
                if (visited.Add(identity))
                {
                    pending ??= new();
                    switch (value.Kind)
                    {
                        case AbstractValueKind.List:
                        case AbstractValueKind.Tuple:
                        case AbstractValueKind.Set:
                            foreach (var item in value.RequireSequenceItems())
                            {
                                CheckFactQuery(context);
                                pending.Push(item);
                            }
                            break;
                        case AbstractValueKind.ListType:
                        case AbstractValueKind.MaybeNone:
                            pending.Push(value.RequireNestedValue());
                            break;
                        case AbstractValueKind.Dict:
                            foreach (var pair in value.RequireDictionaryItems())
                            {
                                CheckFactQuery(context);
                                pending.Push(pair.Key);
                                pending.Push(pair.Value);
                            }
                            break;
                        case AbstractValueKind.UserInstance:
                            foreach (var field in value.RequireInstanceSummary().Fields.Values)
                            {
                                CheckFactQuery(context);
                                pending.Push(field);
                            }
                            break;
                        case AbstractValueKind.UserClass:
                            foreach (var field in value.RequireClassSummary().Fields)
                            {
                                CheckFactQuery(context);
                                pending.Push(field.DefaultValue);
                            }
                            break;
                        case AbstractValueKind.Function:
                            foreach (var captured in ((AbstractState)identity)._values.Values)
                            {
                                CheckFactQuery(context);
                                pending.Push(captured);
                            }
                            break;
                    }
                }
            }

            if (pending is null || !pending.TryPop(out value)) return false;
        }
    }

    private static void CheckFactQuery(StaticAnalysisContext? context)
    {
        context?.MemoryGovernor?.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(context?.CancellationToken ?? default, 0);
    }

    // Each clone records new invalidation requests independently. Deferred
    // bodies do not publish them; evaluated branches join them below.
    public AbstractState Clone() => new(
        new Dictionary<string, AbstractValue>(_values, StringComparer.Ordinal),
        new Dictionary<string, AbstractSequenceLengthBounds>(_sequenceLengths, StringComparer.Ordinal), AnalysisContext);

    public void ReplaceWith(AbstractState other)
    {
        _containerFactsInvalidated |= other._containerFactsInvalidated;
        _values.Clear();
        foreach (var (name, value) in other._values)
        {
            _values[name] = value;
        }

        _sequenceLengths.Clear();
        foreach (var (name, bounds) in other._sequenceLengths)
        {
            _sequenceLengths[name] = bounds;
        }

        InvalidateCachedFacts();
    }

    public void MergeFrom(AbstractState left, AbstractState right)
    {
        _containerFactsInvalidated |= left._containerFactsInvalidated || right._containerFactsInvalidated;
        _values.Clear();
        foreach (var name in left._values.Keys.Concat(right._values.Keys).Distinct(StringComparer.Ordinal))
        {
            if (left._values.TryGetValue(name, out var leftValue) &&
                right._values.TryGetValue(name, out var rightValue))
            {
                _values[name] = AbstractValue.Join(leftValue, rightValue);
            }
        }

        _sequenceLengths.Clear();
        foreach (var name in left._sequenceLengths.Keys.Intersect(right._sequenceLengths.Keys, StringComparer.Ordinal))
        {
            _sequenceLengths[name] = AbstractSequenceLengthBounds.Union(
                left._sequenceLengths[name],
                right._sequenceLengths[name]);
        }

        InvalidateCachedFacts();
    }

    public static AbstractState Merge(AbstractState left, AbstractState right)
    {
        var merged = new AbstractState(left.AnalysisContext);
        merged.MergeFrom(left, right);
        return merged;
    }

    public bool TryGetCachedAbstractValue(ExpressionSyntax expression, out AbstractValueResolution resolution)
        => _abstractValueCache.TryGetValue(expression, out resolution);

    public void SetCachedAbstractValue(ExpressionSyntax expression, AbstractValueResolution resolution)
        => _abstractValueCache[expression] = resolution;

    private void InvalidateCachedFacts()
        => _abstractValueCache.Clear();
}
