namespace Lokad.Lython.Frontend;

internal static partial class StaticStructuralDiagnostics
{
    public static void AnalyzeMemberAccess(MemberExpressionSyntax member, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiver) ||
            !StaticContracts.IsKnownMissingMember(receiver, member.MemberName))
        {
            return;
        }

        AddDiagnostic(
            diagnostics,
            "LA3113",
            $"{StaticAbstractFacts.DescribeValue(receiver)} has no member '{member.MemberName}'.",
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

        if (!TryGetKnownIndex(subscript.Index, index, bindings, out var indexValue))
        {
            return;
        }

        if (TryGetSequenceName(subscript.Target, out var sequenceName) &&
            bindings.TryGetSequenceLength(sequenceName, out var bounds))
        {
            if (bounds.IsImpossible || IsIndexDefinitelyValid(indexValue, bounds.MinimumLength))
            {
                return;
            }

            if (IsIndexDefinitelyInvalid(indexValue, bounds.MaximumLength))
            {
                AddDiagnostic(diagnostics, "LA3117", "Index is out of range.", subscript.Index.Span);
            }

            return;
        }

        if (AbstractValue.TryGetExactSequenceLength(target, out var length) &&
            IsIndexDefinitelyInvalid(indexValue, length))
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

        var pairs = target.RequireDictionaryItems();
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

    public static void AnalyzeSliceAssignment(SliceAssignmentStatementSyntax slice, List<LythonDiagnostic> diagnostics, AbstractState bindings)
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

        if (!StaticAbstractFacts.IsListLike(target))
        {
            AddDiagnostic(diagnostics, "LA3158", "Object does not support slice assignment.", slice.Target.Span);
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

        if (!TryGetSliceBoundObject(slice.Step, bindings, out var stepValue) ||
            stepValue is not System.Numerics.BigInteger stepInteger)
        {
            return;
        }

        if (stepInteger.IsZero)
        {
            AddDiagnostic(diagnostics, "LA3120", "slice step cannot be zero", slice.Step.Span);
            return;
        }

        if (stepInteger.IsOne)
        {
            return;
        }

        if (!AbstractValue.TryGetExactSequenceLength(target, out var targetLength) ||
            !StaticAbstractValueResolver.TryResolve(slice.Expression, bindings, out var replacement) ||
            !AbstractValue.TryGetExactSequenceLength(replacement, out var replacementLength) ||
            !TryGetSliceBoundObject(slice.Start, bindings, out var startValue) ||
            !TryGetSliceBoundObject(slice.End, bindings, out var endValue))
        {
            return;
        }

        var bounds = Lokad.Lython.Runtime.PyIndexing.NormalizeSliceBounds(targetLength, startValue, endValue, stepValue, slice.Span);
        var selectedLength = bounds.Count;
        if (selectedLength != replacementLength)
        {
            AddDiagnostic(
                diagnostics,
                "LA3158",
                $"Extended slice assignment expects {selectedLength} replacement items, got {replacementLength}.",
                slice.Expression.Span);
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

    private static bool TryGetSliceBoundObject(ExpressionSyntax? expression, AbstractState bindings, out object? value)
    {
        if (expression is null)
        {
            value = null;
            return true;
        }

        if (!StaticAbstractValueResolver.TryResolve(expression, bindings, out var resolved))
        {
            value = null;
            return false;
        }

        switch (resolved.Kind)
        {
            case AbstractValueKind.None:
                value = null;
                return true;
            case AbstractValueKind.Boolean:
                value = resolved.RequireBoolean() ? System.Numerics.BigInteger.One : System.Numerics.BigInteger.Zero;
                return true;
            case AbstractValueKind.Integer:
                if (System.Numerics.BigInteger.TryParse(
                    (resolved.RequireText()).Replace("_", string.Empty, StringComparison.Ordinal),
                    out var integer))
                {
                    value = integer;
                    return true;
                }
                break;
        }

        value = null;
        return false;
    }

    private static bool TryGetSequenceName(ExpressionSyntax expression, out string name)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (expression is IdentifierExpressionSyntax identifier)
        {
            name = identifier.Name;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static bool TryGetKnownIndex(
        ExpressionSyntax expression,
        AbstractValue value,
        AbstractState bindings,
        out int index)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (StaticAbstractFacts.TryGetInt32(value, out index))
        {
            return true;
        }

        if (expression is UnaryExpressionSyntax
            {
                Operator: UnaryOperatorSyntax.Minus,
                Operand: var operand
            } &&
            StaticAbstractValueResolver.TryResolve(operand, bindings, out var unsignedValue) &&
            StaticAbstractFacts.TryGetInt32(unsignedValue, out var unsignedIndex) &&
            unsignedIndex != int.MinValue)
        {
            index = -unsignedIndex;
            return true;
        }

        index = 0;
        return false;
    }

    private static bool IsIndexDefinitelyValid(int index, int? minimumLength)
        => minimumLength is int minimum && minimum >= RequiredLength(index);

    private static bool IsIndexDefinitelyInvalid(int index, int? maximumLength)
        => maximumLength is int maximum && maximum < RequiredLength(index);

    private static long RequiredLength(int index)
        => index >= 0 ? (long)index + 1 : -(long)index;

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
                equal = left.HasSamePayload(right);
                return true;

            case AbstractValueKind.Bytes:
                equal = (left.RequireBytes()).AsSpan().SequenceEqual(right.RequireBytes());
                return true;

            case AbstractValueKind.None:
                equal = true;
                return true;

            case AbstractValueKind.Tuple:
                return TrySequenceValuesEqual(
                    left.RequireSequenceItems(),
                    right.RequireSequenceItems(),
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
            ? $"'{key.RequireText()}'"
            : StaticAbstractFacts.DescribeValue(key);

}
