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
    private Dictionary<string, AbstractSequenceLengthBounds> _sequenceLengths;
    private Dictionary<string, AbstractValue> _values;
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
        if (AbstractValue.TryGetExactSequenceLength(value, AnalysisContext, out var length))
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

    public void InvalidateDictionaryFacts() => InvalidateContainerFacts(dictionaries: true, sequences: false);

    public void InvalidateMutableSequenceFacts() => InvalidateContainerFacts(dictionaries: false, sequences: true);

    public void InvalidateCollectionFacts() => InvalidateContainerFacts(dictionaries: true, sequences: true);

    private void InvalidateContainerFacts(bool dictionaries, bool sequences)
    {
        _containerFactsInvalidated = true;
        var changed = false;
        try
        {
            foreach (var name in _values.Keys.ToArray())
            {
                AnalysisContext?.CheckExecution();
                var value = _values[name];
                if (dictionaries && (value.Facts & AbstractValueFacts.Dictionary) != 0)
                {
                    _values.Remove(name);
                    _sequenceLengths.Remove(name);
                    changed = true;
                    continue;
                }

                if (!sequences) continue;
                var remainingTupleWork = Parser.MaxNestingDepth;
                var widened = ForgetMutableSequenceContents(value, ref remainingTupleWork);
                if (widened.Kind != value.Kind || !widened.HasSamePayload(value))
                {
                    _values[name] = widened;
                    changed = true;
                }
                if (value.Kind is AbstractValueKind.List or AbstractValueKind.ListType or AbstractValueKind.CollectionsDeque)
                    changed |= _sequenceLengths.Remove(name);
            }
        }
        finally
        {
            if (changed) InvalidateCachedFacts();
        }
    }

    private AbstractValue ForgetMutableSequenceContents(AbstractValue value, ref int remainingTupleWork)
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
                    AnalysisContext?.CheckExecution();
                    var item = items[index];
                    var widened = ForgetMutableSequenceContents(item, ref remainingTupleWork);
                    if (widened.Kind != item.Kind || !widened.HasSamePayload(item))
                    {
                        replacement ??= items.ToArray();
                        replacement[index] = widened;
                    }
                }
                return replacement is null ? value : AbstractValue.Tuple(replacement, value.Span, AnalysisContext);

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
        if (other._containerFactsInvalidated) InvalidateCollectionFacts();
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
    {
        context?.CheckExecution();
        return (value.Facts & AbstractValueFacts.MutableCollection) != 0;
    }

    public static bool IsCallbackFreeValue(AbstractValue value, StaticAnalysisContext? context)
    {
        context?.CheckExecution();
        return (value.Facts & AbstractValueFacts.Callback) == 0;
    }

    // Each clone records new invalidation requests independently. Deferred
    // bodies do not publish them; evaluated branches join them below.
    public AbstractState Clone() => new(
        new Dictionary<string, AbstractValue>(_values, StringComparer.Ordinal),
        new Dictionary<string, AbstractSequenceLengthBounds>(_sequenceLengths, StringComparer.Ordinal), AnalysisContext);

    // The caller relinquishes this branch. Exchange maps so the two states
    // retain independent mutable bindings.
    public void TakeBranchResult(AbstractState branch)
    {
        var priorInvalidation = _containerFactsInvalidated;
        _containerFactsInvalidated |= branch._containerFactsInvalidated;
        branch._containerFactsInvalidated = priorInvalidation;

        (_values, branch._values) = (branch._values, _values);
        (_sequenceLengths, branch._sequenceLengths) = (branch._sequenceLengths, _sequenceLengths);
        InvalidateCachedFacts();
        branch.InvalidateCachedFacts();
    }

    // The left branch owns these maps. Keep its intersection with the
    // completed right branch and preserve the left binding's diagnostic span.
    internal void JoinBranch(AbstractState right)
    {
        _containerFactsInvalidated |= right._containerFactsInvalidated;
        InvalidateCachedFacts();
        var kept = 0;
        foreach (var (name, rightValue) in right._values)
        {
            if (_values.TryGetValue(name, out var leftValue))
            {
                _values[name] = AbstractValue.Join(leftValue, rightValue, leftValue.Span, AnalysisContext);
                kept++;
            }
        }
        if (kept != _values.Count)
        {
            // Dictionary.Remove preserves active enumerators on .NET 10/11.
            foreach (var name in _values.Keys)
            {
                AnalysisContext?.CheckExecution();
                if (!right._values.ContainsKey(name)) _values.Remove(name);
            }
        }

        kept = 0;
        foreach (var (name, rightBounds) in right._sequenceLengths)
        {
            if (_sequenceLengths.TryGetValue(name, out var leftBounds))
            {
                _sequenceLengths[name] = AbstractSequenceLengthBounds.Union(leftBounds, rightBounds);
                kept++;
            }
        }
        if (kept != _sequenceLengths.Count)
        {
            foreach (var name in _sequenceLengths.Keys)
            {
                AnalysisContext?.CheckExecution();
                if (!right._sequenceLengths.ContainsKey(name)) _sequenceLengths.Remove(name);
            }
        }
    }

    // The left continuation is complete. Its maps become the destination's
    // maps after the join. The right branch keeps its original facts.
    public void TakeJoinedBranches(AbstractState left, AbstractState right)
    {
        left.JoinBranch(right);
        TakeBranchResult(left);
    }

    public bool TryGetCachedAbstractValue(ExpressionSyntax expression, out AbstractValueResolution resolution)
        => _abstractValueCache.TryGetValue(expression, out resolution);

    public void SetCachedAbstractValue(ExpressionSyntax expression, AbstractValueResolution resolution)
        => _abstractValueCache[expression] = resolution;

    internal void InvalidateCachedFacts()
        => _abstractValueCache.Clear();
}
