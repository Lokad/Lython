namespace Lokad.Lython.Frontend;

internal static class StaticStructuralDiagnostics
{
    public static void AnalyzeMemberAccess(MemberExpressionSyntax member, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiver) ||
            !StaticContracts.IsKnownSealedMemberSurface(receiver) ||
            StaticContracts.HasKnownMember(receiver, member.MemberName))
        {
            return;
        }

        AddDiagnostic(
            diagnostics,
            "LA3113",
            $"{DescribeValue(receiver)} has no member '{member.MemberName}'.",
            member.Span);
    }

    public static void AnalyzeSubscriptAccess(SubscriptExpressionSyntax subscript, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(subscript.Target, bindings, out var target))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyNonSubscriptable(target))
        {
            AddDiagnostic(diagnostics, "LA3115", "Object is not subscriptable.", subscript.Target.Span);
            return;
        }

        if (target.Kind == AbstractValueKind.Dict)
        {
            AnalyzeDictionarySubscriptAccess(target, subscript, diagnostics, bindings);
            return;
        }

        if (!StaticAbstractFacts.RequiresIntegerIndex(target))
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolve(subscript.Index, bindings, out var index))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(index))
        {
            AddDiagnostic(diagnostics, "LA3116", "Indices must be integers.", subscript.Index.Span);
            return;
        }

        if (TryGetExactSequenceLength(target, out var length) &&
            StaticAbstractFacts.TryGetNonNegativeInt32(index, out var indexValue) &&
            indexValue >= length)
        {
            AddDiagnostic(diagnostics, "LA3117", "Index is out of range.", subscript.Index.Span);
        }
    }

    private static void AnalyzeDictionarySubscriptAccess(
        AbstractValue target,
        SubscriptExpressionSyntax subscript,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(subscript.Index, bindings, out var key) ||
            !key.IsLiteralLike)
        {
            return;
        }

        var pairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)target.Value;
        foreach (var pair in pairs)
        {
            if (!pair.Key.IsLiteralLike)
            {
                return;
            }

            if (!TryAbstractValuesEqual(pair.Key, key, out var equal))
            {
                return;
            }

            if (equal)
            {
                return;
            }
        }

        AddDiagnostic(
            diagnostics,
            "LA3157",
            $"Dictionary has no key {DescribeDictionaryKey(key)}.",
            subscript.Index.Span);
    }

    public static void AnalyzeSliceAccess(SliceExpressionSyntax slice, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(slice.Target, bindings, out var target))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyNonSliceable(target))
        {
            AddDiagnostic(diagnostics, "LA3118", "Object does not support slicing.", slice.Target.Span);
            return;
        }

        AnalyzeSliceBound(slice.Start, diagnostics, bindings);
        AnalyzeSliceBound(slice.End, diagnostics, bindings);

        if (slice.Step is null)
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolve(slice.Step, bindings, out var step))
        {
            return;
        }

        if (step.Kind == AbstractValueKind.None)
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(step))
        {
            AddDiagnostic(diagnostics, "LA3119", "Slice indices must be integers or None.", slice.Step.Span);
            return;
        }

        if (StaticAbstractFacts.TryGetNonNegativeInt32(step, out var stepValue) && stepValue == 0)
        {
            AddDiagnostic(diagnostics, "LA3120", "slice step cannot be zero", slice.Step.Span);
        }
    }

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
            if (!StaticAbstractFacts.IsNumericLike(operand))
            {
                AddDiagnostic(diagnostics, "LA3144", "Operand is not numeric.", unary.Span);
            }

            return;
        }

        if (unary.Operator == UnaryOperatorSyntax.BitwiseNot && !StaticAbstractFacts.IsIntegerLike(operand))
        {
            AddDiagnostic(diagnostics, "LA3145", "Operand is not an integer.", unary.Span);
        }
    }

    private static void AnalyzeSliceBound(ExpressionSyntax? bound, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (bound is null || !StaticAbstractValueResolver.TryResolve(bound, bindings, out var value))
        {
            return;
        }

        if (value.Kind == AbstractValueKind.None)
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(value))
        {
            AddDiagnostic(diagnostics, "LA3119", "Slice indices must be integers or None.", bound.Span);
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
                AddDiagnostic(diagnostics, "LA3143", "Values are not comparable.", span);
            }

            return;
        }

        if (!CanApplyBinaryOperator(op, left, right))
        {
            AddDiagnostic(diagnostics, "LA3141", $"Operands are not compatible with '{DescribeBinaryOperator(op)}'.", span);
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
            BinaryOperatorSyntax.Subtract => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) || IsSetLike(left) && IsSetLike(right),
            BinaryOperatorSyntax.Multiply => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                left.IsStringLike && StaticAbstractFacts.IsIntegerLike(right) ||
                StaticAbstractFacts.IsIntegerLike(left) && right.IsStringLike,
            BinaryOperatorSyntax.Divide => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
                left.Kind == AbstractValueKind.Path && (right.Kind == AbstractValueKind.Path || right.IsStringLike),
            BinaryOperatorSyntax.FloorDivide or
            BinaryOperatorSyntax.Modulo or
            BinaryOperatorSyntax.Power => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right),
            BinaryOperatorSyntax.BitwiseOr or
            BinaryOperatorSyntax.BitwiseXor or
            BinaryOperatorSyntax.BitwiseAnd => StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right) || IsSetLike(left) && IsSetLike(right),
            BinaryOperatorSyntax.LeftShift or
            BinaryOperatorSyntax.RightShift => StaticAbstractFacts.IsIntegerLike(left) && StaticAbstractFacts.IsIntegerLike(right),
            _ => true
        };
    }

    private static bool CanApplyAdd(AbstractValue left, AbstractValue right)
        => left.IsStringLike && right.IsStringLike ||
           StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
           IsListLike(left) && IsListLike(right) ||
           left.Kind == AbstractValueKind.Tuple && right.Kind == AbstractValueKind.Tuple ||
           IsSetLike(left) && IsSetLike(right);

    private static bool CanApplyOrderedComparison(AbstractValue left, AbstractValue right)
        => StaticAbstractFacts.IsNumericLike(left) && StaticAbstractFacts.IsNumericLike(right) ||
           left.IsStringLike && right.IsStringLike ||
           left.Kind == AbstractValueKind.Path && right.Kind == AbstractValueKind.Path;

    private static bool CanApplyMembership(AbstractValue candidate, AbstractValue container)
    {
        if (container.IsStringLike)
        {
            return candidate.IsStringLike;
        }

        return container.Kind is AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.Set;
    }

    private static bool IsListLike(AbstractValue value)
        => value.Kind is AbstractValueKind.List or AbstractValueKind.ListType;

    private static bool IsSetLike(AbstractValue value)
        => value.Kind == AbstractValueKind.Set;

    private static bool TryGetExactSequenceLength(AbstractValue value, out int length)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.String:
                length = ((string)value.Value).Length;
                return true;
            case AbstractValueKind.Bytes:
                length = ((byte[])value.Value).Length;
                return true;
            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                length = ((IReadOnlyList<AbstractValue>)value.Value).Count;
                return true;
            default:
                length = 0;
                return false;
        }
    }

    private static bool TryAbstractValuesEqual(AbstractValue left, AbstractValue right, out bool equal)
    {
        if (left.Kind != right.Kind)
        {
            equal = false;
            return false;
        }

        switch (left.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.Integer:
            case AbstractValueKind.Float:
            case AbstractValueKind.Boolean:
                equal = Equals(left.Value, right.Value);
                return true;

            case AbstractValueKind.Bytes:
                equal = ((byte[])left.Value).AsSpan().SequenceEqual((byte[])right.Value);
                return true;

            case AbstractValueKind.None:
                equal = true;
                return true;

            case AbstractValueKind.Tuple:
                return TrySequenceValuesEqual(
                    (IReadOnlyList<AbstractValue>)left.Value,
                    (IReadOnlyList<AbstractValue>)right.Value,
                    out equal);

            default:
                equal = false;
                return false;
        }
    }

    private static bool TrySequenceValuesEqual(
        IReadOnlyList<AbstractValue> left,
        IReadOnlyList<AbstractValue> right,
        out bool equal)
    {
        if (left.Count != right.Count)
        {
            equal = false;
            return true;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!TryAbstractValuesEqual(left[i], right[i], out var itemEqual))
            {
                equal = false;
                return false;
            }

            if (!itemEqual)
            {
                equal = false;
                return true;
            }
        }

        equal = true;
        return true;
    }

    private static string DescribeDictionaryKey(AbstractValue key)
        => key.Kind == AbstractValueKind.String
            ? $"'{key.Value}'"
            : StaticAbstractFacts.DescribeLiteralType(key);

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

    private static string DescribeValue(AbstractValue value)
    {
        return value.Kind switch
        {
            AbstractValueKind.String or AbstractValueKind.StringType => "str",
            AbstractValueKind.Bytes or AbstractValueKind.BytesType => "bytes",
            AbstractValueKind.Integer or AbstractValueKind.IntegerType => "int",
            AbstractValueKind.Float or AbstractValueKind.FloatType => "float",
            AbstractValueKind.Boolean or AbstractValueKind.BooleanType => "bool",
            AbstractValueKind.None => "NoneType",
            AbstractValueKind.List or AbstractValueKind.ListType => "list",
            AbstractValueKind.Tuple => "tuple",
            AbstractValueKind.Dict => "dict",
            AbstractValueKind.Set => "set",
            AbstractValueKind.Path => "pathlib.Path",
            AbstractValueKind.TextFileHandle => "file",
            AbstractValueKind.Module => $"module '{value.Value}'",
            AbstractValueKind.KnownCallable => $"callable '{value.Value}'",
            AbstractValueKind.RegexPattern => "re.Pattern",
            AbstractValueKind.MaybeRegexMatch => "re.Match | None",
            AbstractValueKind.RegexMatch => "re.Match",
            AbstractValueKind.ArgparseParser => "argparse.ArgumentParser",
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => "argparse._MutuallyExclusiveGroup",
            AbstractValueKind.ArgparseNamespace => "argparse.Namespace",
            AbstractValueKind.CsvWriter => "csv.writer",
            AbstractValueKind.SubprocessCompletedProcess => "subprocess.CompletedProcess",
            AbstractValueKind.DataclassField => "dataclasses.Field",
            AbstractValueKind.Function => "function",
            AbstractValueKind.UserClass => ((AbstractClassSummary)value.Value).Name,
            AbstractValueKind.UserInstance => ((AbstractInstanceSummary)value.Value).Class.Name,
            _ => "object"
        };
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
