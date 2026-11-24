namespace Lokad.Lython.Frontend;

internal static class StaticDictionaryFacts
{
    public static bool TryContainsKey(AbstractValue dictionary, AbstractValue key, out bool contains)
    {
        contains = false;
        if (dictionary.Kind != AbstractValueKind.Dict || !IsKnownHashableKey(key))
        {
            return false;
        }

        var uncertain = false;
        foreach (var pair in dictionary.RequireDictionaryItems())
        {
            if (!TryKeysEqual(pair.Key, key, out var equal))
            {
                uncertain = true;
            }
            else if (equal)
            {
                contains = true;
                return true;
            }
        }

        return !uncertain;
    }

    private static bool IsKnownHashableKey(AbstractValue key)
        => key.Kind is AbstractValueKind.String or AbstractValueKind.Bytes or
            AbstractValueKind.Integer or AbstractValueKind.Float or
            AbstractValueKind.Boolean or AbstractValueKind.None ||
            key.Kind == AbstractValueKind.Tuple && key.RequireSequenceItems().All(IsKnownHashableKey);

    private static bool TryKeysEqual(AbstractValue left, AbstractValue right, out bool equal)
    {
        equal = false;
        if (left.Kind != right.Kind)
        {
            // Numeric kinds can compare equal in Python. Defer mixed kinds
            // rather than proving a miss from their different abstract tags.
            return false;
        }

        switch (left.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.Boolean:
                equal = left.HasSamePayload(right);
                return true;
            case AbstractValueKind.Integer:
                if (left.TryGetDecimalInteger(out var leftInteger) && right.TryGetDecimalInteger(out var rightInteger))
                {
                    equal = leftInteger == rightInteger;
                    return true;
                }
                return false;
            case AbstractValueKind.Float:
                equal = left.RequireFloat() == right.RequireFloat();
                return true;
            case AbstractValueKind.Bytes:
                equal = left.RequireBytes().SequenceEqual(right.RequireBytes());
                return true;
            case AbstractValueKind.None:
                equal = true;
                return true;
            case AbstractValueKind.Tuple:
                var leftItems = left.RequireSequenceItems();
                var rightItems = right.RequireSequenceItems();
                if (leftItems.Count != rightItems.Count)
                {
                    return true;
                }
                for (var i = 0; i < leftItems.Count; i++)
                {
                    if (!TryKeysEqual(leftItems[i], rightItems[i], out var itemEqual))
                    {
                        return false;
                    }
                    if (!itemEqual)
                    {
                        return true;
                    }
                }
                equal = true;
                return true;
            default:
                return false;
        }
    }
}
