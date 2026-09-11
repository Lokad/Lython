namespace Lokad.Lython.Frontend;

internal static partial class StaticStructuralDiagnostics
{
    public static void AnalyzeBinaryOperation(BinaryExpressionSyntax binary, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeBinaryOperation(binary.Operator, binary.Left, binary.Right, binary.Span, diagnostics, bindings);

    public static void AnalyzeChainedComparisonOperations(ChainedComparisonExpressionSyntax chained, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        for (var i = 0; i < chained.Operators.Count; i++)
        {
            AnalyzeBinaryOperation(chained.Operators[i], chained.Operands[i], chained.Operands[i + 1], chained.Span, diagnostics, bindings);
        }
    }

    public static void AnalyzeUnaryOperation(UnaryExpressionSyntax unary, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (unary.Operator == UnaryOperatorSyntax.Not ||
            !StaticAbstractValueResolver.TryResolve(unary.Operand, bindings, out var operand) ||
            !IsKnownOperatorOperand(operand))
        {
            return;
        }

        if (unary.Operator is UnaryOperatorSyntax.Plus or UnaryOperatorSyntax.Minus)
        {
            if (!StaticAbstractFacts.IsNumericLike(operand) &&
                operand.Kind != AbstractValueKind.DateTimeTimedelta &&
                operand.Kind != AbstractValueKind.StatisticsNormalDist &&
                operand.Kind != AbstractValueKind.CollectionsCounter)
            {
                AddDiagnostic(diagnostics, "LA3144", UnaryOperandMessage(unary.Operator == UnaryOperatorSyntax.Plus ? "+" : "-", operand), unary.Span);
            }

            return;
        }

        if (unary.Operator == UnaryOperatorSyntax.BitwiseNot && !StaticAbstractFacts.IsIntegerLike(operand))
        {
            AddDiagnostic(diagnostics, "LA3145", UnaryOperandMessage("~", operand), unary.Span);
        }
    }

    private static void AnalyzeBinaryOperation(
        BinaryOperatorSyntax op,
        ExpressionSyntax leftExpression,
        ExpressionSyntax rightExpression,
        LythonSourceSpan span,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (op is BinaryOperatorSyntax.Or or
            BinaryOperatorSyntax.And or
            BinaryOperatorSyntax.Equal or
            BinaryOperatorSyntax.NotEqual or
            BinaryOperatorSyntax.Is or
            BinaryOperatorSyntax.IsNot)
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolve(leftExpression, bindings, out var left) ||
            !StaticAbstractValueResolver.TryResolve(rightExpression, bindings, out var right) ||
            !IsKnownOperatorOperand(left) ||
            !IsKnownOperatorOperand(right))
        {
            return;
        }

        if (op is BinaryOperatorSyntax.In or BinaryOperatorSyntax.NotIn)
        {
            if (!CanApplyMembership(left, right))
            {
                AddDiagnostic(diagnostics, "LA3142", "Right operand does not support membership testing.", span);
            }

            return;
        }

        if (op is BinaryOperatorSyntax.Less or BinaryOperatorSyntax.LessEqual or BinaryOperatorSyntax.Greater or BinaryOperatorSyntax.GreaterEqual)
        {
            if (!CanApplyOrderedComparison(left, right))
            {
                AddDiagnostic(diagnostics, "LA3143", ComparisonMessage(op, left, right), span);
            }

            return;
        }

        if (!CanApplyBinaryOperator(op, left, right))
        {
            AddDiagnostic(diagnostics, "LA3141", BinaryOperandsMessage(op, left, right), span);
        }
    }

    private static bool IsKnownOperatorOperand(AbstractValue value)
        => value.Kind is not AbstractValueKind.Unknown and
            not AbstractValueKind.Never and
            not AbstractValueKind.UserClass and
            not AbstractValueKind.UserInstance;

    private static bool CanApplyBinaryOperator(BinaryOperatorSyntax op, AbstractValue left, AbstractValue right)
    {
        return op switch
        {
            BinaryOperatorSyntax.Add => CanApplyAdd(left, right),
            BinaryOperatorSyntax.Subtract => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out _) ||
                StaticAbstractFacts.IsNormalDistAdditivePair(left, right) ||
                StaticAbstractFacts.IsSetLike(left) && StaticAbstractFacts.IsSetLike(right) ||
                left.Kind == AbstractValueKind.CollectionsCounter && right.Kind == AbstractValueKind.CollectionsCounter,
            BinaryOperatorSyntax.Multiply => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out _) ||
                StaticAbstractFacts.IsNormalDistNumericPair(left, right) ||
                left.IsStringLike && StaticAbstractFacts.IsIntegerLike(right) ||
                StaticAbstractFacts.IsIntegerLike(left) && right.IsStringLike ||
                StaticAbstractFacts.IsListLike(left) && StaticAbstractFacts.IsIntegerLike(right) ||
                StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsListLike(right) ||
                StaticAbstractFacts.IsTupleLike(left) && StaticAbstractFacts.IsIntegerLike(right) ||
                StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsTupleLike(right),
            BinaryOperatorSyntax.Divide => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out _) ||
                left.Kind == AbstractValueKind.StatisticsNormalDist && StaticAbstractFacts.IsNumericLike(right) ||
                left.Kind == AbstractValueKind.Path && (right.Kind == AbstractValueKind.Path || right.IsStringLike),
            BinaryOperatorSyntax.FloorDivide => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out _),
            BinaryOperatorSyntax.Modulo => CanApplyStringModulo(left, right) ||
                StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                StaticAbstractFacts.TryGetDateTimeBinaryResultKind(op, left, right, out _),
            BinaryOperatorSyntax.Power => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right),
            BinaryOperatorSyntax.BitwiseOr or
            BinaryOperatorSyntax.BitwiseAnd => StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right) || StaticAbstractFacts.IsSetLike(left) && StaticAbstractFacts.IsSetLike(right) ||
            left.Kind == AbstractValueKind.CollectionsCounter && right.Kind == AbstractValueKind.CollectionsCounter,
            BinaryOperatorSyntax.BitwiseXor => StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right) || StaticAbstractFacts.IsSetLike(left) && StaticAbstractFacts.IsSetLike(right),
            BinaryOperatorSyntax.LeftShift or
            BinaryOperatorSyntax.RightShift => StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right),
            _ => true
        };
    }

    private static bool CanApplyStringModulo(AbstractValue left, AbstractValue right)
    {
        if (!left.IsStringLike)
        {
            return false;
        }

        if (left.Kind != AbstractValueKind.String)
        {
            return true;
        }

        if (!TryInspectPercentFormat(left.RequireText(), out var positionalCount, out var hasMapping))
        {
            return false;
        }

        if (right.Kind == AbstractValueKind.Tuple)
        {
            return !hasMapping && (right.RequireSequenceItems()).Count == positionalCount;
        }

        if (hasMapping && positionalCount == 0)
        {
            return right.Kind is AbstractValueKind.Dict or
                AbstractValueKind.CollectionsDefaultDict or
                AbstractValueKind.CollectionsCounter or
                AbstractValueKind.CollectionsChainMap;
        }

        return positionalCount == 1 || hasMapping;
    }

    private static bool TryInspectPercentFormat(string format, out int positionalCount, out bool hasMapping)
    {
        positionalCount = 0;
        hasMapping = false;
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%')
            {
                continue;
            }

            index++;
            if (index >= format.Length)
            {
                return false;
            }

            if (format[index] == '%')
            {
                continue;
            }

            var mapping = false;
            if (format[index] == '(')
            {
                mapping = true;
                hasMapping = true;
                var depth = 1;
                while (++index < format.Length)
                {
                    if (format[index] == '(')
                    {
                        depth++;
                    }
                    else if (format[index] == ')' && --depth == 0)
                    {
                        break;
                    }
                }

                if (index >= format.Length)
                {
                    return false;
                }

                index++;
            }

            while (index < format.Length && format[index] is '#' or '0' or '-' or '+' or ' ')
            {
                index++;
            }

            if (index < format.Length && format[index] == '*')
            {
                positionalCount++;
                index++;
            }
            else
            {
                while (index < format.Length && char.IsAsciiDigit(format[index]))
                {
                    index++;
                }
            }

            if (index < format.Length && format[index] == '.')
            {
                index++;
                if (index < format.Length && format[index] == '*')
                {
                    positionalCount++;
                    index++;
                }
                else
                {
                    while (index < format.Length && char.IsAsciiDigit(format[index]))
                    {
                        index++;
                    }
                }
            }

            while (index < format.Length && format[index] is 'h' or 'l' or 'L')
            {
                index++;
            }

            if (index >= format.Length || format[index] is not ('s' or 'r' or 'a' or 'd' or 'i' or 'u' or 'o' or 'x' or 'X' or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or 'c'))
            {
                return false;
            }

            if (!mapping)
            {
                positionalCount++;
            }
        }

        return true;
    }

    private static bool CanApplyAdd(AbstractValue left, AbstractValue right)
        => left.IsStringLike && right.IsStringLike ||
           StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
           StaticAbstractFacts.IsNormalDistAdditivePair(left, right) ||
           StaticAbstractFacts.TryGetDateTimeBinaryResultKind(BinaryOperatorSyntax.Add, left, right, out _) ||
           StaticAbstractFacts.IsListLike(left) && StaticAbstractFacts.IsListLike(right) ||
           left.Kind == AbstractValueKind.Tuple && right.Kind == AbstractValueKind.Tuple ||
           left.Kind == AbstractValueKind.CollectionsCounter && right.Kind == AbstractValueKind.CollectionsCounter;

    private static bool CanApplyOrderedComparison(AbstractValue left, AbstractValue right)
        => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
           left.IsStringLike && right.IsStringLike ||
           left.Kind == AbstractValueKind.DateTimeTimedelta && right.Kind == AbstractValueKind.DateTimeTimedelta ||
           left.Kind == AbstractValueKind.DateTimeDate && right.Kind == AbstractValueKind.DateTimeDate ||
           left.Kind == AbstractValueKind.DateTimeDateTime && right.Kind == AbstractValueKind.DateTimeDateTime ||
           left.Kind == AbstractValueKind.DateTimeTime && right.Kind == AbstractValueKind.DateTimeTime ||
           left.Kind == AbstractValueKind.Path && right.Kind == AbstractValueKind.Path ||
           StaticAbstractFacts.IsListLike(left) && StaticAbstractFacts.IsListLike(right) ||
           left.Kind == AbstractValueKind.Tuple && right.Kind == AbstractValueKind.Tuple ||
           StaticAbstractFacts.IsSetLike(left) && StaticAbstractFacts.IsSetLike(right);

    private static bool CanApplyMembership(AbstractValue candidate, AbstractValue container)
    {
        if (container.IsStringLike)
        {
            return candidate.IsStringLike;
        }

        return container.Kind is AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.StatisticsLinearRegression or
            AbstractValueKind.OpenPyxlWorkbook or
            AbstractValueKind.Dict or
            AbstractValueKind.Set or
            AbstractValueKind.SetType;
    }

    private static string ComparisonMessage(BinaryOperatorSyntax op, AbstractValue left, AbstractValue right)
    {
        var operation = op switch
        {
            BinaryOperatorSyntax.Less => "<",
            BinaryOperatorSyntax.LessEqual => "<=",
            BinaryOperatorSyntax.Greater => ">",
            _ => ">=",
        };

        if (TryOperandTypeName(left, out var lhs) && TryOperandTypeName(right, out var rhs))
        {
            return $"'{operation}' not supported between instances of '{lhs}' and '{rhs}'";
        }

        return "Values are not comparable.";
    }

    private static string BinaryOperandsMessage(BinaryOperatorSyntax op, AbstractValue left, AbstractValue right)
    {
        if (op == BinaryOperatorSyntax.Add && TrySequenceOperand(left, out var concat) &&
            TryOperandTypeName(right, out var other))
        {
            return concat == "bytes"
                ? $"can't concat {other} to bytes"
                : $"can only concatenate {concat} (not \"{other}\") to {concat}";
        }

        if (op == BinaryOperatorSyntax.Multiply &&
            (TryMultiplySequenceOperand(left, right, out var multiplied) ||
             TryMultiplySequenceOperand(right, left, out multiplied)))
        {
            return $"can't multiply sequence by non-int of type '{multiplied}'";
        }

        var operation = op == BinaryOperatorSyntax.Power ? "** or pow()" : DescribeBinaryOperator(op);
        if (TryOperandTypeName(left, out var lhs) && TryOperandTypeName(right, out var rhs))
        {
            return $"unsupported operand type(s) for {operation}: '{lhs}' and '{rhs}'";
        }

        return $"Operands are not compatible with '{DescribeBinaryOperator(op)}'.";
    }

    private static bool TrySequenceOperand(AbstractValue value, out string name)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.List:
                name = "list";
                return true;
            case AbstractValueKind.Tuple:
                name = "tuple";
                return true;
            case AbstractValueKind.String:
                name = "str";
                return true;
            case AbstractValueKind.Bytes:
                name = "bytes";
                return true;
            default:
                name = string.Empty;
                return false;
        }
    }

    private static bool TryMultiplySequenceOperand(AbstractValue sequence, AbstractValue other, out string name)
    {
        if (TrySequenceOperand(sequence, out _) &&
            other.Kind is not (AbstractValueKind.Integer or AbstractValueKind.Boolean) &&
            TryOperandTypeName(other, out name))
        {
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static string UnaryOperandMessage(string operation, AbstractValue operand)
        => TryOperandTypeName(operand, out var name)
            ? $"bad operand type for unary {operation}: '{name}'"
            : operation == "~" ? "Operand is not an integer." : "Operand is not numeric.";

    internal static bool TryOperandTypeName(AbstractValue value, out string name)
    {
        name = value.Kind switch
        {
            AbstractValueKind.String => "str",
            AbstractValueKind.Bytes => "bytes",
            AbstractValueKind.Integer => "int",
            AbstractValueKind.Float => "float",
            AbstractValueKind.Boolean => "bool",
            AbstractValueKind.None => "NoneType",
            AbstractValueKind.Ellipsis => "ellipsis",
            AbstractValueKind.List => "list",
            AbstractValueKind.Tuple => "tuple",
            AbstractValueKind.Dict => "dict",
            AbstractValueKind.Set => "set",
            AbstractValueKind.CollectionsDefaultDict => "collections.defaultdict",
            AbstractValueKind.CollectionsCounter => "Counter",
            AbstractValueKind.CollectionsDeque => "deque",
            AbstractValueKind.CollectionsChainMap => "ChainMap",
            AbstractValueKind.Decimal => "decimal.Decimal",
            AbstractValueKind.DateTimeTimedelta => "datetime.timedelta",
            AbstractValueKind.DateTimeDate => "datetime.date",
            AbstractValueKind.DateTimeTime => "datetime.time",
            AbstractValueKind.DateTimeDateTime => "datetime.datetime",
            AbstractValueKind.DateTimeTimezone => "datetime.timezone",
            AbstractValueKind.StatisticsNormalDist => "NormalDist",
            AbstractValueKind.Random => "Random",
            AbstractValueKind.StringType or
            AbstractValueKind.BytesType or
            AbstractValueKind.IntegerType or
            AbstractValueKind.FloatType or
            AbstractValueKind.BooleanType or
            AbstractValueKind.ListType or
            AbstractValueKind.SetType => "type",
            _ => null,
        };

        return name is not null;
    }

    private static string DescribeBinaryOperator(BinaryOperatorSyntax op)
        => op switch
        {
            BinaryOperatorSyntax.Add => "+",
            BinaryOperatorSyntax.Subtract => "-",
            BinaryOperatorSyntax.Multiply => "*",
            BinaryOperatorSyntax.Divide => "/",
            BinaryOperatorSyntax.FloorDivide => "//",
            BinaryOperatorSyntax.Modulo => "%",
            BinaryOperatorSyntax.Power => "**",
            BinaryOperatorSyntax.BitwiseOr => "|",
            BinaryOperatorSyntax.BitwiseXor => "^",
            BinaryOperatorSyntax.BitwiseAnd => "&",
            BinaryOperatorSyntax.LeftShift => "<<",
            BinaryOperatorSyntax.RightShift => ">>",
            _ => op.ToString()
        };
}
