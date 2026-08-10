using static Lokad.Lython.Frontend.AbstractValue;

namespace Lokad.Lython.Frontend;

internal static class AbstractValueJoin
{
    private static readonly IEqualityComparer<AbstractValue> LiteralKeyComparer = new AbstractLiteralKeyComparer();

    public static AbstractValue Join(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        // Join is the least upper bound used at control-flow merges. Preserve exact
        // literals only when both paths agree, then widen within a Python value family;
        // unrelated families deliberately become Unknown instead of inventing a union.
        if (left.Kind == AbstractValueKind.Never)
        {
            return right.WithSpan(span);
        }

        if (right.Kind == AbstractValueKind.Never)
        {
            return left.WithSpan(span);
        }

        if (left.Kind == AbstractValueKind.Unknown || right.Kind == AbstractValueKind.Unknown)
        {
            return Unknown(span);
        }

        if (left.Kind == AbstractValueKind.None)
        {
            return right.Kind switch
            {
                AbstractValueKind.None => None(span),
                AbstractValueKind.MaybeRegexMatch => right.WithSpan(span),
                _ => MaybeNone(right, span),
            };
        }

        if (right.Kind == AbstractValueKind.None)
        {
            return left.Kind == AbstractValueKind.MaybeRegexMatch
                ? left.WithSpan(span)
                : MaybeNone(left, span);
        }

        if (left.Kind == AbstractValueKind.MaybeNone || right.Kind == AbstractValueKind.MaybeNone)
        {
            var leftValue = left.Kind == AbstractValueKind.MaybeNone ? left.RequireNestedValue() : left;
            var rightValue = right.Kind == AbstractValueKind.MaybeNone ? right.RequireNestedValue() : right;
            return MaybeNone(Join(leftValue, rightValue, span), span);
        }

        if (left.Kind == right.Kind)
        {
            return left.Kind switch
            {
                AbstractValueKind.String => left.HasSamePayload(right) ? left.WithSpan(span) : StringType(span),
                AbstractValueKind.Bytes => LiteralValuesEqual(left, right) ? left.WithSpan(span) : BytesType(span),
                AbstractValueKind.Integer => left.HasSamePayload(right) ? left.WithSpan(span) : IntegerType(span),
                AbstractValueKind.Float => left.HasSamePayload(right) ? left.WithSpan(span) : FloatType(span),
                AbstractValueKind.Boolean => left.HasSamePayload(right) ? left.WithSpan(span) : BooleanType(span),
                AbstractValueKind.MaybeNone => MaybeNone(Join(left.RequireNestedValue(), right.RequireNestedValue(), span), span),
                AbstractValueKind.List => JoinLiteralLists(left, right, span),
                AbstractValueKind.ListType => ListOf(Join(left.RequireNestedValue(), right.RequireNestedValue(), span), span),
                AbstractValueKind.SetType => SetOf(Join(left.RequireNestedValue(), right.RequireNestedValue(), span), span),
                AbstractValueKind.Dict => JoinLiteralDictionaries(left, right, span),
                AbstractValueKind.TextFileHandle => TextFileHandle(JoinTextFileModes(left.RequireTextFileMode(), right.RequireTextFileMode()), span),
                AbstractValueKind.Module => left.HasSamePayload(right) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.KnownCallable => left.HasSamePayload(right) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.RegexPattern => JoinRegexPatterns(left, right, span),
                AbstractValueKind.MaybeRegexMatch => JoinRegexMatches(left, right, span, maybe: true),
                AbstractValueKind.RegexMatch => JoinRegexMatches(left, right, span, maybe: false),
                AbstractValueKind.ArgparseParser => ArgparseParser(JoinArgparseParserSummaries(left.RequireArgparseParserSummary(), right.RequireArgparseParserSummary(), span), span),
                AbstractValueKind.ArgparseNamespace => ArgparseNamespace(JoinArgparseNamespaceSummaries(left.RequireArgparseNamespaceSummary(), right.RequireArgparseNamespaceSummary(), span), span),
                AbstractValueKind.ArgparseMutuallyExclusiveGroup => JoinArgparseGroups(left, right, span),
                AbstractValueKind.DataclassField => JoinDataclassFields(left, right, span),
                AbstractValueKind.Function => left.HasSamePayload(right) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.UserClass => left.HasSamePayload(right) ? left.WithSpan(span) : Unknown(span),
                AbstractValueKind.UserInstance => JoinUserInstances(left, right, span),
                _ => left.WithSpan(span),
            };
        }

        if (left.IsStringLike && right.IsStringLike)
        {
            return StringType(span);
        }

        if (IsIntegerLike(left) && IsIntegerLike(right))
        {
            return IntegerType(span);
        }

        if (IsFloatLike(left) && IsFloatLike(right))
        {
            return FloatType(span);
        }

        if (IsBooleanLike(left) && IsBooleanLike(right))
        {
            return BooleanType(span);
        }

        if (IsBytesLike(left) && IsBytesLike(right))
        {
            return BytesType(span);
        }

        if (TryGetListElement(left, out var leftItem) && TryGetListElement(right, out var rightItem))
        {
            return ListOf(Join(leftItem, rightItem, span), span);
        }

        if (TryGetSetElement(left, out var leftSetItem) && TryGetSetElement(right, out var rightSetItem))
        {
            return SetOf(Join(leftSetItem, rightSetItem, span), span);
        }

        return Unknown(span);
    }

    private static AbstractValue JoinLiteralDictionaries(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftPairs = left.RequireDictionaryItems();
        var rightPairs = right.RequireDictionaryItems();
        if (leftPairs.Count != rightPairs.Count)
        {
            return Unknown(span);
        }

        // A literal dictionary remains useful only when both paths have the same
        // comparable keys. Different shapes widen to Unknown because the analyzer
        // does not model optional dictionary entries.
        var rightValues = new Dictionary<AbstractValue, AbstractValue>(rightPairs.Count, LiteralKeyComparer);
        foreach (var rightPair in rightPairs)
        {
            if (IsComparableLiteralKey(rightPair.Key))
            {
                rightValues.TryAdd(rightPair.Key, rightPair.Value);
            }
        }

        var joinedPairs = new List<KeyValuePair<AbstractValue, AbstractValue>>(leftPairs.Count);
        foreach (var leftPair in leftPairs)
        {
            if (!IsComparableLiteralKey(leftPair.Key) ||
                !rightValues.TryGetValue(leftPair.Key, out var rightValue))
            {
                return Unknown(span);
            }

            joinedPairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                leftPair.Key.WithSpan(span),
                Join(leftPair.Value, rightValue, span)));
        }

        return Dict(joinedPairs, span);
    }

    private static AbstractArgparseParserSummary JoinArgparseParserSummaries(
        AbstractArgparseParserSummary left,
        AbstractArgparseParserSummary right,
        LythonSourceSpan span)
        => new(JoinMemberMaps(left.Members, right.Members, span), left.IsSealed && right.IsSealed && HaveSameKeys(left.Members, right.Members));

    private static AbstractArgparseNamespaceSummary JoinArgparseNamespaceSummaries(
        AbstractArgparseNamespaceSummary left,
        AbstractArgparseNamespaceSummary right,
        LythonSourceSpan span)
        => new(JoinMemberMaps(left.Members, right.Members, span), left.IsSealed && right.IsSealed && HaveSameKeys(left.Members, right.Members));

    private static IReadOnlyDictionary<string, AbstractValue> JoinMemberMaps(
        IReadOnlyDictionary<string, AbstractValue> left,
        IReadOnlyDictionary<string, AbstractValue> right,
        LythonSourceSpan span)
    {
        var members = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        foreach (var (name, leftValue) in left)
        {
            if (right.TryGetValue(name, out var rightValue))
            {
                members[name] = Join(leftValue, rightValue, span);
            }
        }

        return members;
    }

    private static bool HaveSameKeys(
        IReadOnlyDictionary<string, AbstractValue> left,
        IReadOnlyDictionary<string, AbstractValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var name in left.Keys)
        {
            if (!right.ContainsKey(name))
            {
                return false;
            }
        }

        return true;
    }

    private static AbstractValue JoinDataclassFields(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftField = left.RequireDataclassFieldSummary();
        var rightField = right.RequireDataclassFieldSummary();
        return string.Equals(leftField.Name, rightField.Name, StringComparison.Ordinal)
            ? DataclassField(leftField.Name, span)
            : Unknown(span);
    }

    private static AbstractValue JoinArgparseGroups(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftGroup = left.RequireArgparseGroupSummary();
        var rightGroup = right.RequireArgparseGroupSummary();
        return string.Equals(leftGroup.ParserName, rightGroup.ParserName, StringComparison.Ordinal)
            ? ArgparseMutuallyExclusiveGroup(leftGroup.ParserName, span)
            : ArgparseMutuallyExclusiveGroup(span);
    }

    private static AbstractValue JoinUserInstances(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftInstance = left.RequireInstanceSummary();
        var rightInstance = right.RequireInstanceSummary();
        if (!Equals(leftInstance.Class, rightInstance.Class))
        {
            return Unknown(span);
        }

        var fields = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        foreach (var name in leftInstance.Fields.Keys.Concat(rightInstance.Fields.Keys).Distinct(StringComparer.Ordinal))
        {
            if (leftInstance.Fields.TryGetValue(name, out var leftField) &&
                rightInstance.Fields.TryGetValue(name, out var rightField))
            {
                fields[name] = Join(leftField, rightField, span);
            }
        }

        return fields.Count == 0
            ? Unknown(span)
            : UserInstance(new AbstractInstanceSummary(leftInstance.Class, fields, span), span);
    }

    private static AbstractValue JoinLiteralLists(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftItems = left.RequireSequenceItems();
        var rightItems = right.RequireSequenceItems();
        if (leftItems.Count == rightItems.Count)
        {
            var allSame = true;
            for (var i = 0; i < leftItems.Count; i++)
            {
                if (!Equals(leftItems[i], rightItems[i]))
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                return left.WithSpan(span);
            }
        }

        return ListOf(JoinListItems(leftItems, rightItems, span), span);
    }

    private static AbstractValue JoinRegexPatterns(AbstractValue left, AbstractValue right, LythonSourceSpan span)
    {
        var leftSummary = left.RequireRegexPatternSummary();
        var rightSummary = right.RequireRegexPatternSummary();
        return RegexPatternSummariesEqual(leftSummary, rightSummary)
            ? RegexPattern(leftSummary, span)
            : RegexPattern(span);
    }

    private static AbstractValue JoinRegexMatches(AbstractValue left, AbstractValue right, LythonSourceSpan span, bool maybe)
    {
        var leftSummary = left.RequireRegexMatchSummary();
        var rightSummary = right.RequireRegexMatchSummary();
        if (!RegexMatchSummariesEqual(leftSummary, rightSummary))
        {
            return maybe ? MaybeRegexMatch(span) : RegexMatch(span);
        }

        return maybe ? MaybeRegexMatch(leftSummary, span) : RegexMatch(leftSummary, span);
    }

    private static AbstractValue JoinListItems(IReadOnlyList<AbstractValue> leftItems, IReadOnlyList<AbstractValue> rightItems, LythonSourceSpan span)
    {
        var result = Never(span);
        foreach (var item in leftItems)
        {
            result = Join(result, item, span);
        }

        foreach (var item in rightItems)
        {
            result = Join(result, item, span);
        }

        return result.Kind == AbstractValueKind.Never ? Unknown(span) : result;
    }

    private static bool TryGetListElement(AbstractValue value, out AbstractValue item)
    {
        if (value.Kind == AbstractValueKind.ListType)
        {
            item = value.RequireNestedValue();
            return true;
        }

        if (value.Kind == AbstractValueKind.List)
        {
            item = JoinListItems(value.RequireSequenceItems(), Array.Empty<AbstractValue>(), value.Span);
            return true;
        }

        item = default;
        return false;
    }

    private static bool TryGetSetElement(AbstractValue value, out AbstractValue item)
    {
        if (value.Kind == AbstractValueKind.SetType)
        {
            item = value.RequireNestedValue();
            return true;
        }

        if (value.Kind == AbstractValueKind.Set)
        {
            item = JoinListItems(value.RequireSequenceItems(), Array.Empty<AbstractValue>(), value.Span);
            return true;
        }

        item = default;
        return false;
    }

    private static AbstractTextFileMode JoinTextFileModes(AbstractTextFileMode left, AbstractTextFileMode right)
        => left == right ? left : AbstractTextFileMode.Unknown;

    private static bool IsIntegerLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType;

    private static bool IsFloatLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Float or AbstractValueKind.FloatType;

    private static bool IsBooleanLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Boolean or AbstractValueKind.BooleanType;

    private static bool IsBytesLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType;

    private static bool RegexPatternSummariesEqual(AbstractRegexPatternSummary left, AbstractRegexPatternSummary right)
        => string.Equals(left.PatternText, right.PatternText, StringComparison.Ordinal) &&
           left.CaptureSlotCount == right.CaptureSlotCount &&
           IntegerMapsEqual(left.NamedGroups, right.NamedGroups);

    private static bool RegexMatchSummariesEqual(AbstractRegexMatchSummary left, AbstractRegexMatchSummary right)
        => left.CaptureSlotCount == right.CaptureSlotCount &&
           IntegerMapsEqual(left.NamedGroups, right.NamedGroups);

    private static bool IntegerMapsEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var rightValue) || value != rightValue)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsComparableLiteralKey(AbstractValue value)
        => value.Kind is AbstractValueKind.String or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean or
            AbstractValueKind.Bytes or
            AbstractValueKind.None;

    private sealed class AbstractLiteralKeyComparer : IEqualityComparer<AbstractValue>
    {
        public bool Equals(AbstractValue left, AbstractValue right) => LiteralValuesEqual(left, right);

        public int GetHashCode(AbstractValue value)
        {
            var hash = new HashCode();
            hash.Add(value.Kind);
            switch (value.Kind)
            {
                case AbstractValueKind.Bytes:
                    hash.AddBytes(value.RequireBytes());
                    break;
                case AbstractValueKind.String:
                case AbstractValueKind.Integer:
                case AbstractValueKind.Float:
                    hash.Add(value.RequireText());
                    break;
                case AbstractValueKind.Boolean:
                    hash.Add(value.RequireBoolean());
                    break;
            }

            return hash.ToHashCode();
        }
    }
}
