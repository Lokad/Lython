namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static bool TryResolveBinaryAbstractValue(BinaryExpressionSyntax binary, AbstractState bindings, out AbstractValue value)
    {
        if (binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
        {
            value = AbstractValue.Join(
                ResolveOrUnknown(binary.Left, bindings),
                ResolveOrUnknown(binary.Right, bindings),
                binary.Span);
            return true;
        }

        if (binary.Operator is BinaryOperatorSyntax.Equal or
            BinaryOperatorSyntax.NotEqual or
            BinaryOperatorSyntax.Less or
            BinaryOperatorSyntax.LessEqual or
            BinaryOperatorSyntax.Greater or
            BinaryOperatorSyntax.GreaterEqual or
            BinaryOperatorSyntax.Is or
            BinaryOperatorSyntax.IsNot or
            BinaryOperatorSyntax.In or
            BinaryOperatorSyntax.NotIn)
        {
            value = AbstractValue.BooleanType(binary.Span);
            return true;
        }

        if (binary.Operator is not (
                BinaryOperatorSyntax.Add or
                BinaryOperatorSyntax.Subtract or
                BinaryOperatorSyntax.Multiply or
                BinaryOperatorSyntax.Divide or
                BinaryOperatorSyntax.FloorDivide or
                BinaryOperatorSyntax.Modulo or
                BinaryOperatorSyntax.Power) ||
            !TryResolve(binary.Left, bindings, out var left) ||
            !TryResolve(binary.Right, bindings, out var right))
        {
            value = default;
            return false;
        }

        if (TryResolveDateTimeBinaryAbstractValue(binary.Operator, left, right, binary.Span, out value))
        {
            return true;
        }

        if (binary.Operator == BinaryOperatorSyntax.Modulo && left.IsStringLike)
        {
            value = AbstractValue.StringType(binary.Span);
            return true;
        }

        if (TryResolveStatisticsBinaryAbstractValue(binary.Operator, left, right, binary.Span, out value))
        {
            return true;
        }

        if (TryResolveDecimalBinaryAbstractValue(binary.Operator, left, right, binary.Span, out value))
        {
            return true;
        }

        if (binary.Operator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Multiply))
        {
            value = default;
            return false;
        }

        if (binary.Operator == BinaryOperatorSyntax.Multiply)
        {
            if (left.Kind == AbstractValueKind.CollectionsDeque && StaticAbstractFacts.IsIntegerLike(right) ||
                StaticAbstractFacts.IsIntegerLike(left) && right.Kind == AbstractValueKind.CollectionsDeque)
            {
                value = AbstractValue.CollectionsDeque(binary.Span);
                return true;
            }

            if (TryGetListElementAbstractValue(left, out var repeatedLeftItem) && StaticAbstractFacts.IsIntegerLike(right))
            {
                value = AbstractValue.ListOf(repeatedLeftItem.WithSpan(binary.Span), binary.Span);
                return true;
            }

            if (StaticAbstractFacts.IsIntegerLike(left) && TryGetListElementAbstractValue(right, out var repeatedRightItem))
            {
                value = AbstractValue.ListOf(repeatedRightItem.WithSpan(binary.Span), binary.Span);
                return true;
            }

            value = default;
            return false;
        }

        if (left.IsStringLike && right.IsStringLike)
        {
            value = left.Kind == AbstractValueKind.String && right.Kind == AbstractValueKind.String
                ? AbstractValue.String(left.RequireText() + right.RequireText(), binary.Span)
                : AbstractValue.StringType(binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.Bytes && right.Kind == AbstractValueKind.Bytes)
        {
            value = AbstractValue.Bytes(left.RequireBytes().Concat(right.RequireBytes()).ToArray(), binary.Span);
            return true;
        }

        if (StaticAbstractFacts.IsBytesLike(left) && StaticAbstractFacts.IsBytesLike(right))
        {
            value = AbstractValue.BytesType(binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.List && right.Kind == AbstractValueKind.List)
        {
            value = AbstractValue.List(
                (left.RequireSequenceItems()).Concat(right.RequireSequenceItems()).ToArray(),
                binary.Span);
            return true;
        }

        if (TryGetListElementAbstractValue(left, out var leftItem) &&
            TryGetListElementAbstractValue(right, out var rightItem))
        {
            value = AbstractValue.ListOf(AbstractValue.Join(leftItem, rightItem, binary.Span), binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.CollectionsDeque && right.Kind == AbstractValueKind.CollectionsDeque)
        {
            value = AbstractValue.CollectionsDeque(binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.Tuple && right.Kind == AbstractValueKind.Tuple)
        {
            value = AbstractValue.Tuple(
                (left.RequireSequenceItems()).Concat(right.RequireSequenceItems()).ToArray(),
                binary.Span);
            return true;
        }

        if (StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right))
        {
            value = AbstractValue.IntegerType(binary.Span);
            return true;
        }

        if (StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right))
        {
            value = AbstractValue.FloatType(binary.Span);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveDateTimeBinaryAbstractValue(
        BinaryOperatorSyntax op,
        AbstractValue left,
        AbstractValue right,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (!StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out var resultKind))
        {
            value = default;
            return false;
        }

        value = resultKind switch
        {
            AbstractValueKind.DateTimeTimedelta => AbstractValue.DateTimeTimedelta(span),
            AbstractValueKind.DateTimeDate => AbstractValue.DateTimeDate(span),
            AbstractValueKind.DateTimeDateTime => AbstractValue.DateTimeDateTime(span),
            AbstractValueKind.FloatType => AbstractValue.FloatType(span),
            AbstractValueKind.IntegerType => AbstractValue.IntegerType(span),
            _ => throw new InvalidOperationException($"Unsupported datetime operation result kind '{resultKind}'.")
        };
        return true;
    }

    private static bool TryResolveDecimalBinaryAbstractValue(
        BinaryOperatorSyntax op,
        AbstractValue left,
        AbstractValue right,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        // Decimal arithmetic over decimals and integers stays decimal like
        // the runtime; float mixes keep the historical fallback below since
        // they fail at runtime instead.
        if (op is not (
                BinaryOperatorSyntax.Add or
                BinaryOperatorSyntax.Subtract or
                BinaryOperatorSyntax.Multiply or
                BinaryOperatorSyntax.Divide or
                BinaryOperatorSyntax.FloorDivide or
                BinaryOperatorSyntax.Modulo or
                BinaryOperatorSyntax.Power) ||
            (left.Kind != AbstractValueKind.Decimal && right.Kind != AbstractValueKind.Decimal) ||
            StaticAbstractFacts.IsFloatLike(left) ||
            StaticAbstractFacts.IsFloatLike(right) ||
            !IsDecimalArithmeticPeer(left) ||
            !IsDecimalArithmeticPeer(right))
        {
            value = default;
            return false;
        }

        value = AbstractValue.Decimal(span);
        return true;

        static bool IsDecimalArithmeticPeer(AbstractValue value)
            => value.Kind == AbstractValueKind.Decimal || StaticAbstractFacts.IsIntegerLike(value);
    }

    private static bool TryResolveStatisticsBinaryAbstractValue(
        BinaryOperatorSyntax op,
        AbstractValue left,
        AbstractValue right,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        value = op switch
        {
            BinaryOperatorSyntax.Add when StaticAbstractFacts.IsNormalDistAdditivePair(left, right) => AbstractValue.StatisticsNormalDist(span),
            BinaryOperatorSyntax.Subtract when StaticAbstractFacts.IsNormalDistAdditivePair(left, right) => AbstractValue.StatisticsNormalDist(span),
            BinaryOperatorSyntax.Multiply when StaticAbstractFacts.IsNormalDistNumericPair(left, right) => AbstractValue.StatisticsNormalDist(span),
            BinaryOperatorSyntax.Divide when left.Kind == AbstractValueKind.StatisticsNormalDist && StaticAbstractFacts.IsNumericLike(right) => AbstractValue.StatisticsNormalDist(span),
            _ => default
        };

        return value.Kind != default;
    }

    private static bool TryResolveUnaryAbstractValue(UnaryExpressionSyntax unary, AbstractState bindings, out AbstractValue value)
    {
        if (unary.Operator == UnaryOperatorSyntax.Not)
        {
            value = AbstractValue.BooleanType(unary.Span);
            return true;
        }

        if (unary.Operator is not (UnaryOperatorSyntax.Plus or UnaryOperatorSyntax.Minus) ||
            !TryResolve(unary.Operand, bindings, out var operand))
        {
            value = default;
            return false;
        }

        if (StaticAbstractFacts.IsIntegerLike(operand))
        {
            value = AbstractValue.IntegerType(unary.Span);
            return true;
        }

        if (StaticAbstractFacts.IsFloatLike(operand))
        {
            value = AbstractValue.FloatType(unary.Span);
            return true;
        }

        if (operand.Kind == AbstractValueKind.Decimal)
        {
            value = AbstractValue.Decimal(unary.Span);
            return true;
        }

        if (operand.Kind == AbstractValueKind.DateTimeTimedelta)
        {
            value = AbstractValue.DateTimeTimedelta(unary.Span);
            return true;
        }

        if (operand.Kind == AbstractValueKind.StatisticsNormalDist)
        {
            value = AbstractValue.StatisticsNormalDist(unary.Span);
            return true;
        }

        value = default;
        return false;
    }
}
