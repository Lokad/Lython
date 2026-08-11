namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    public static bool TryResolve(ExpressionSyntax expression, AbstractState bindings, out AbstractValue value)
    {
        if (bindings.TryGetCachedAbstractValue(expression, out var cachedResolution))
        {
            return cachedResolution.TryGetValue(out value);
        }

        var success = TryResolveDirect(expression, bindings, out value) ||
            TryResolveComputed(expression, bindings, out value);
        bindings.SetCachedAbstractValue(
            expression,
            success ? AbstractValueResolution.Resolved(value) : AbstractValueResolution.Unresolved);
        return success;
    }

    public static AbstractValue ResolveOrUnknown(ExpressionSyntax expression, AbstractState bindings)
        => TryResolve(expression, bindings, out var value)
            ? value
            : AbstractValue.Unknown(expression.Span);

    public static void UpdateBindings(StatementSyntax statement, AbstractState bindings)
        => StaticBindingEngine.UpdateBindings(statement, bindings);

    public static bool TryResolveKnownValue(ExpressionSyntax expression, AbstractState bindings, out AbstractValue value)
    {
        if (TryResolve(expression, bindings, out value) && value.IsLiteralLike)
        {
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryResolveKnownString(ExpressionSyntax expression, AbstractState bindings, out string text)
    {
        if (TryResolveKnownValue(expression, bindings, out var value) && value.Kind == AbstractValueKind.String)
        {
            text = value.RequireText();
            return true;
        }

        text = string.Empty;
        return false;
    }

    public static bool TryResolveKnownStringLike(ExpressionSyntax expression, AbstractState bindings)
        => TryResolve(expression, bindings, out var value) && value.IsStringLike;

    public static bool TryResolveKnownPath(ExpressionSyntax expression, AbstractState bindings)
        => TryResolve(expression, bindings, out var value) && value.Kind == AbstractValueKind.Path;

    public static bool TryResolveKnownTextFileHandle(ExpressionSyntax expression, AbstractState bindings, out AbstractTextFileMode mode)
    {
        if (TryResolve(expression, bindings, out var value) &&
            value.Kind == AbstractValueKind.TextFileHandle)
        {
            mode = value.RequireTextFileMode();
            return true;
        }

        mode = AbstractTextFileMode.Unknown;
        return false;
    }

    public static bool TryResolveKnownSequenceItems(ExpressionSyntax expression, AbstractState bindings, out IReadOnlyList<AbstractValue> items)
    {
        if (TryResolveKnownValue(expression, bindings, out var value) &&
            value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            var sequenceItems = value.RequireSequenceItems();
            if (sequenceItems.All(static item => item.IsLiteralLike))
            {
                items = sequenceItems;
                return true;
            }
        }

        items = Array.Empty<AbstractValue>();
        return false;
    }

    public static bool IsDefinitelyKnownLiteral(ExpressionSyntax expression, AbstractState bindings)
        => TryResolveKnownValue(expression, bindings, out _);

    public static bool IsDefinitelyKnownBytesLiteral(ExpressionSyntax expression, AbstractState bindings)
        => TryResolveKnownValue(expression, bindings, out var value) && value.Kind == AbstractValueKind.Bytes;

    public static bool IsDefinitelyKnownNonStringLiteral(ExpressionSyntax expression, AbstractState bindings)
        => TryResolveKnownValue(expression, bindings, out var value) &&
           value.IsLiteralLike &&
           value.Kind != AbstractValueKind.String &&
           value.Kind != AbstractValueKind.None;

    public static bool IsDefinitelyKnownNonStringLike(ExpressionSyntax expression, AbstractState bindings)
        => TryResolve(expression, bindings, out var value) && value.IsDefinitelyNonStringLike;

    public static bool IsDefinitelyKnownNonCallableLiteral(ExpressionSyntax expression, AbstractState bindings)
        => TryResolveKnownValue(expression, bindings, out var value) &&
           value.Kind is not AbstractValueKind.Unknown and not AbstractValueKind.Never;

    public static bool IsDefinitelyKnownNonIterableLiteral(ExpressionSyntax expression, AbstractState bindings)
        => TryResolveKnownValue(expression, bindings, out var value) &&
           value.Kind is AbstractValueKind.Integer or AbstractValueKind.Float or AbstractValueKind.Boolean or AbstractValueKind.None;

    private static bool TryResolveDirect(ExpressionSyntax expression, AbstractState bindings, out AbstractValue value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        switch (expression)
        {
            case StringLiteralExpressionSyntax text:
                value = AbstractValue.String(text.Value, text.Span);
                return true;
            case BytesLiteralExpressionSyntax bytes:
                value = AbstractValue.Bytes(bytes.Value, bytes.Span);
                return true;
            case IntegerLiteralExpressionSyntax integer:
                value = AbstractValue.Integer(integer.ValueText, integer.Span);
                return true;
            case FloatLiteralExpressionSyntax floating:
                value = AbstractValue.Float(floating.ValueText, floating.Span);
                return true;
            case BooleanLiteralExpressionSyntax boolean:
                value = AbstractValue.Boolean(boolean.Value, boolean.Span);
                return true;
            case NoneLiteralExpressionSyntax none:
                value = AbstractValue.None(none.Span);
                return true;
            case IdentifierExpressionSyntax identifier when bindings.TryGet(identifier.Name, out value):
                return true;
            case CallExpressionSyntax call when IsLikelyPathConstructor(call):
                value = AbstractValue.Path(call.Span);
                return true;
            case ListLiteralExpressionSyntax list:
                value = ResolveAbstractValueList(list.Items, list.Span, AbstractValueKind.List, bindings);
                return true;
            case TupleLiteralExpressionSyntax tuple:
                value = ResolveAbstractValueList(tuple.Items, tuple.Span, AbstractValueKind.Tuple, bindings);
                return true;
            case SetLiteralExpressionSyntax set:
                value = ResolveAbstractValueList(set.Items, set.Span, AbstractValueKind.Set, bindings);
                return true;
            case DictLiteralExpressionSyntax dict:
                value = ResolveAbstractDictValue(dict, bindings);
                return true;
            default:
                value = default;
                return false;
        }

        static bool IsLikelyPathConstructor(CallExpressionSyntax call)
        {
            var target = call.Target;
            while (target is ParenthesizedExpressionSyntax parenthesized)
            {
                target = parenthesized.Inner;
            }

            return target is IdentifierExpressionSyntax { Name: "Path" or "PurePath" or "PurePosixPath" or "PosixPath" } or
                MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "pathlib" },
                    MemberName: "Path" or "PurePath" or "PurePosixPath" or "PosixPath"
                };
        }
    }

    private static bool TryResolveComputed(ExpressionSyntax expression, AbstractState bindings, out AbstractValue value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        switch (expression)
        {
            case ListComprehensionExpressionSyntax listComprehension:
                value = ResolveListComprehensionValue(listComprehension, bindings);
                return true;

            case SetComprehensionExpressionSyntax setComprehension:
                value = ResolveSetComprehensionValue(setComprehension, bindings);
                return true;

            case ConditionalExpressionSyntax conditional:
                value = AbstractValue.Join(
                    ResolveOrUnknown(conditional.Consequent, bindings),
                    ResolveOrUnknown(conditional.Alternative, bindings),
                    conditional.Span);
                return true;

            case BinaryExpressionSyntax binary:
                return TryResolveBinaryAbstractValue(binary, bindings, out value);

            case ChainedComparisonExpressionSyntax chainedComparison:
                value = AbstractValue.BooleanType(chainedComparison.Span);
                return true;

            case UnaryExpressionSyntax unary:
                return TryResolveUnaryAbstractValue(unary, bindings, out value);

            case CallExpressionSyntax call:
                return TryResolveCallAbstractValue(call, bindings, out value);

            case MemberExpressionSyntax member:
                if (StaticContractEngine.TryResolveMemberValue(member, bindings, out value))
                {
                    return true;
                }

                if (TryResolve(member.Target, bindings, out var instanceValue) &&
                    instanceValue.Kind == AbstractValueKind.UserInstance &&
                    StaticBindingEngine.TryGetUserInstanceMemberValue(instanceValue, member.MemberName, member.Span, out value))
                {
                    return true;
                }

                value = default;
                return false;

            case SubscriptExpressionSyntax subscript:
                return TryResolveSubscriptAbstractValue(subscript, bindings, out value);

            case SliceExpressionSyntax slice:
                return TryResolveSliceAbstractValue(slice, bindings, out value);

            default:
                value = default;
                return false;
        }
    }
}
