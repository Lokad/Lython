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
    private readonly Dictionary<ExpressionSyntax, CachedAbstractValue> _abstractValueCache;
    private readonly Dictionary<string, AbstractSequenceLengthBounds> _sequenceLengths;
    private readonly Dictionary<string, AbstractValue> _values;
    private int _version;

    public AbstractState()
    {
        _values = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        _sequenceLengths = new Dictionary<string, AbstractSequenceLengthBounds>(StringComparer.Ordinal);
        _abstractValueCache = new Dictionary<ExpressionSyntax, CachedAbstractValue>();
    }

    private AbstractState(
        Dictionary<string, AbstractValue> values,
        Dictionary<string, AbstractSequenceLengthBounds> sequenceLengths)
    {
        _values = values;
        _sequenceLengths = sequenceLengths;
        _abstractValueCache = new Dictionary<ExpressionSyntax, CachedAbstractValue>();
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

    public void InvalidateMutableSequenceFacts()
    {
        var changed = false;
        foreach (var name in _values.Keys.ToArray())
        {
            var value = _values[name];
            if (value.Kind == AbstractValueKind.List)
            {
                var items = (IReadOnlyList<AbstractValue>)value.Value;
                var item = items.Count == 0
                    ? AbstractValue.Unknown(value.Span)
                    : items.Skip(1).Aggregate(items[0], (joined, next) => AbstractValue.Join(joined, next, value.Span));
                _values[name] = AbstractValue.ListOf(item, value.Span);
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

    public AbstractState Clone() => new(
        new Dictionary<string, AbstractValue>(_values, StringComparer.Ordinal),
        new Dictionary<string, AbstractSequenceLengthBounds>(_sequenceLengths, StringComparer.Ordinal));

    public void ReplaceWith(AbstractState other)
    {
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
        var merged = new AbstractState();
        merged.MergeFrom(left, right);
        return merged;
    }

    public bool TryGetCachedAbstractValue(ExpressionSyntax expression, out AbstractValueResolution resolution)
    {
        if (_abstractValueCache.TryGetValue(expression, out var cached) && cached.Version == _version)
        {
            resolution = cached.Resolution;
            return true;
        }

        resolution = default;
        return false;
    }

    public void SetCachedAbstractValue(ExpressionSyntax expression, AbstractValueResolution resolution)
    {
        _abstractValueCache[expression] = new CachedAbstractValue(_version, resolution);
    }

    private void InvalidateCachedFacts()
    {
        _version++;
        _abstractValueCache.Clear();
    }

    private readonly record struct CachedAbstractValue(int Version, AbstractValueResolution Resolution);
}
