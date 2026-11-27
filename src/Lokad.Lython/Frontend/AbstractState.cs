using Lokad.Lython.Runtime;

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
    // Computed resolutions belong to this binding state and this exact syntax
    // node. Every mutation clears the cache. Identifiers use the binding map.
    private Dictionary<ExpressionSyntax, AbstractValueResolution>? _abstractValueCache;
    private Dictionary<string, AbstractSequenceLengthBounds> _sequenceLengths;
    private Dictionary<string, AbstractValue> _values;
    // An upper bound on the collection effects carried by the value map. The
    // existing filtering and join walks can also discard stale bits.
    private AbstractValueFacts _bindingFacts;
    private bool _containerFactsInvalidated;
    private bool _deferred;
    private bool _valuesShared;
    private bool _sequenceLengthsShared;

    internal StaticAnalysisContext? AnalysisContext { get; }

    public AbstractState(StaticAnalysisContext? analysisContext = null)
    {
        AnalysisContext = analysisContext;
        _values = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        _sequenceLengths = new Dictionary<string, AbstractSequenceLengthBounds>(StringComparer.Ordinal);
    }

    private AbstractState(
        Dictionary<string, AbstractValue> values,
        Dictionary<string, AbstractSequenceLengthBounds> sequenceLengths,
        StaticAnalysisContext? analysisContext)
    {
        AnalysisContext = analysisContext;
        _values = values;
        _sequenceLengths = sequenceLengths;
    }

    public bool TryGet(string name, out AbstractValue value) => _values.TryGetValue(name, out value);

    public string? UnshadowedBuiltinName(ExpressionSyntax target)
        => target is IdentifierExpressionSyntax identifier && !_values.ContainsKey(identifier.Name) &&
            CanUseUnboundBuiltin(identifier.Name) ? identifier.Name : null;

    // Callers have already established that there is no flow binding. Deferred
    // bodies can observe module writes that have not happened at definition.
    public bool CanUseUnboundBuiltin(string name)
        => !_deferred || AnalysisContext?.Script.PreparedScopeFacts?.LocalNames.Contains(name) != true;

    public void BindUnknown(string name)
    {
        if (ExecutionState.BuiltinNames.Contains(name))
            Set(name, AbstractValue.Unknown());
        else
            Remove(name);
    }

    public void ForgetValue(string name)
    {
        if (ExecutionState.BuiltinNames.Contains(name) && _values.ContainsKey(name))
            Set(name, AbstractValue.Unknown());
        else
            Remove(name);
    }

    public void ShadowLocalBuiltins(ScopeDirectiveFacts scope)
    {
        foreach (var name in scope.LocalNames)
        {
            AnalysisContext?.CheckExecution();
            if (ExecutionState.BuiltinNames.Contains(name))
                Set(name, AbstractValue.Unknown());
        }
    }

    public void DeleteBinding(string name)
    {
        if (_deferred)
            BindUnknown(name);
        else
            Remove(name);
    }

    public void Set(string name, AbstractValue value)
    {
        WritableValues()[name] = value;
        _bindingFacts |= value.Facts;
        if (AbstractValue.TryGetExactSequenceLength(value, AnalysisContext, out var length))
        {
            WritableSequenceLengths()[name] = AbstractSequenceLengthBounds.Exact(length);
        }
        else
        {
            RemoveSequenceLength(name);
        }

        InvalidateCachedFacts();
    }

    public bool TryGetSequenceLength(string name, out AbstractSequenceLengthBounds bounds)
        => _sequenceLengths.TryGetValue(name, out bounds);

    public void SetSequenceLength(string name, AbstractSequenceLengthBounds bounds)
    {
        WritableSequenceLengths()[name] = bounds;
        InvalidateCachedFacts();
    }

    public void Remove(string name)
    {
        if (RemoveValue(name) | RemoveSequenceLength(name))
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
        var effects = (dictionaries ? AbstractValueFacts.Dictionary : AbstractValueFacts.None) |
            (sequences ? AbstractValueFacts.MutableSequence : AbstractValueFacts.None);
        if ((_bindingFacts & effects) == 0) return;

        var retainedFacts = AbstractValueFacts.None;
        var changed = false;
        List<(string Name, AbstractValue Value)>? rewrites = null;
        try
        {
            foreach (var (name, value) in _values)
            {
                AnalysisContext?.CheckExecution();
                if (dictionaries && (value.Facts & AbstractValueFacts.Dictionary) != 0)
                {
                    if (ExecutionState.BuiltinNames.Contains(name))
                    {
                        (rewrites ??= []).Add((name, AbstractValue.Unknown()));
                    }
                    else
                    {
                        // Remove preserves active dictionary enumerators on .NET 10/11.
                        WritableValues().Remove(name);
                    }
                    RemoveSequenceLength(name);
                    changed = true;
                    continue;
                }

                var retained = value;
                if (sequences && (value.Facts & AbstractValueFacts.MutableSequence) != 0)
                {
                    var remainingTupleWork = Parser.MaxNestingDepth;
                    retained = ForgetMutableSequenceContents(value, ref remainingTupleWork);
                    if (retained.Kind != value.Kind || !retained.HasSamePayload(value))
                        (rewrites ??= []).Add((name, retained));
                    if (value.Kind is AbstractValueKind.List or AbstractValueKind.ListType or AbstractValueKind.CollectionsDeque)
                        changed |= RemoveSequenceLength(name);
                }
                retainedFacts |= retained.Facts;
            }

            // Apply actual value replacements after enumeration.
            if (rewrites is not null)
            {
                foreach (var (name, value) in rewrites)
                {
                    AnalysisContext?.CheckExecution();
                    WritableValues()[name] = value;
                    changed = true;
                }
            }
            _bindingFacts = retainedFacts;
        }
        finally
        {
            if (changed) InvalidateCachedFacts();
        }
    }

    private AbstractValue ForgetMutableSequenceContents(AbstractValue value, ref int remainingTupleWork)
    {
        if ((value.Facts & AbstractValueFacts.MutableSequence) == 0) return value;
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
        clone._deferred = true;
        if ((_bindingFacts & AbstractValueFacts.MutableCollection) == 0) return clone;
        // Captured collections can change before a def or lambda runs. Forget
        // their contents, including through tuples and other captured values;
        // new local collections will establish their own facts in the body.
        var retainedFacts = AbstractValueFacts.None;
        foreach (var (name, value) in _values)
        {
            if (ContainsMutableCollectionFacts(value, AnalysisContext))
            {
                clone.BindUnknown(name);
            }
            else retainedFacts |= value.Facts;
        }
        clone._bindingFacts = retainedFacts;
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
    public AbstractState Clone()
    {
        _valuesShared = _sequenceLengthsShared = true;
        return new(_values, _sequenceLengths, AnalysisContext)
        {
            _bindingFacts = _bindingFacts,
            _deferred = _deferred,
            _valuesShared = true,
            _sequenceLengthsShared = true,
        };
    }

    // Each fact map detaches on its first change. A branch can refine lengths
    // without copying name values, and vice versa. Resolution caches stay local.
    private Dictionary<string, AbstractValue> WritableValues()
    {
        if (_valuesShared)
        {
            AnalysisContext?.CheckExecution();
            _values = new(_values, StringComparer.Ordinal);
            _valuesShared = false;
        }
        return _values;
    }

    private Dictionary<string, AbstractSequenceLengthBounds> WritableSequenceLengths()
    {
        if (_sequenceLengthsShared)
        {
            AnalysisContext?.CheckExecution();
            _sequenceLengths = new(_sequenceLengths, StringComparer.Ordinal);
            _sequenceLengthsShared = false;
        }
        return _sequenceLengths;
    }

    private bool RemoveValue(string name)
        => (!_valuesShared || _values.ContainsKey(name)) && WritableValues().Remove(name);

    private bool RemoveSequenceLength(string name)
        => (!_sequenceLengthsShared || _sequenceLengths.ContainsKey(name)) && WritableSequenceLengths().Remove(name);

    // The caller relinquishes this branch. Exchange maps so the two states
    // retain independent mutable bindings.
    public void TakeBranchResult(AbstractState branch)
    {
        var priorInvalidation = _containerFactsInvalidated;
        _containerFactsInvalidated |= branch._containerFactsInvalidated;
        branch._containerFactsInvalidated = priorInvalidation;

        (_values, branch._values) = (branch._values, _values);
        (_bindingFacts, branch._bindingFacts) = (branch._bindingFacts, _bindingFacts);
        (_sequenceLengths, branch._sequenceLengths) = (branch._sequenceLengths, _sequenceLengths);
        (_valuesShared, branch._valuesShared) = (branch._valuesShared, _valuesShared);
        (_sequenceLengthsShared, branch._sequenceLengthsShared) = (branch._sequenceLengthsShared, _sequenceLengthsShared);
        InvalidateCachedFacts();
        branch.InvalidateCachedFacts();
    }

    // Values require facts on both paths, but a write on either path is
    // enough to prevent assuming a builtin.
    internal void JoinBranch(AbstractState right)
    {
        _containerFactsInvalidated |= right._containerFactsInvalidated;
        InvalidateCachedFacts();
        var kept = 0;
        var joinedFacts = AbstractValueFacts.None;
        foreach (var (name, rightValue) in right._values)
        {
            if (_values.TryGetValue(name, out var leftValue))
            {
                var joined = AbstractValue.Join(leftValue, rightValue, leftValue.Span, AnalysisContext);
                WritableValues()[name] = joined;
                joinedFacts |= joined.Facts;
                kept++;
            }
            else if (ExecutionState.BuiltinNames.Contains(name))
            {
                WritableValues()[name] = AbstractValue.Unknown();
                kept++;
            }
        }
        if (kept != _values.Count)
        {
            // Dictionary.Remove preserves active enumerators on .NET 10/11.
            foreach (var name in _values.Keys)
            {
                AnalysisContext?.CheckExecution();
                if (right._values.ContainsKey(name)) continue;
                if (ExecutionState.BuiltinNames.Contains(name))
                    WritableValues()[name] = AbstractValue.Unknown();
                else
                    WritableValues().Remove(name);
            }
        }
        _bindingFacts = joinedFacts;

        if (ReferenceEquals(_sequenceLengths, right._sequenceLengths)) return;
        kept = 0;
        foreach (var (name, rightBounds) in right._sequenceLengths)
        {
            if (_sequenceLengths.TryGetValue(name, out var leftBounds))
            {
                WritableSequenceLengths()[name] = AbstractSequenceLengthBounds.Union(leftBounds, rightBounds);
                kept++;
            }
        }
        if (kept != _sequenceLengths.Count)
        {
            foreach (var name in _sequenceLengths.Keys)
            {
                AnalysisContext?.CheckExecution();
                if (!right._sequenceLengths.ContainsKey(name)) WritableSequenceLengths().Remove(name);
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
    {
        if (_abstractValueCache is not null) return _abstractValueCache.TryGetValue(expression, out resolution);
        resolution = default;
        return false;
    }

    public void SetCachedAbstractValue(ExpressionSyntax expression, AbstractValueResolution resolution)
        => (_abstractValueCache ??= new(ReferenceEqualityComparer.Instance))[expression] = resolution;

    internal void InvalidateCachedFacts()
        => _abstractValueCache?.Clear();
}
