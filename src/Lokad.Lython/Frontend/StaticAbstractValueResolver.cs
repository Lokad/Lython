namespace Lokad.Lython.Frontend;

internal static class StaticAbstractValueResolver
{
    public static bool TryResolve(ExpressionSyntax expression, AbstractState bindings, out AbstractValue value)
    {
        if (bindings.TryGetCachedAbstractValue(expression, out var cachedSuccess, out var cachedValue))
        {
            value = cachedValue;
            return cachedSuccess;
        }

        var success = TryResolveDirect(expression, bindings, out value) ||
            TryResolveComputed(expression, bindings, out value);
        bindings.SetCachedAbstractValue(expression, success, value);
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
            text = (string)value.Value;
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
            mode = (AbstractTextFileMode)value.Value;
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
            var sequenceItems = (IReadOnlyList<AbstractValue>)value.Value;
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

    private static AbstractValue ResolveAbstractValueList(
        IReadOnlyList<ExpressionSyntax> expressions,
        LythonSourceSpan span,
        AbstractValueKind kind,
        AbstractState bindings)
    {
        var items = new List<AbstractValue>(expressions.Count);
        foreach (var expression in expressions)
        {
            items.Add(ResolveOrUnknown(expression, bindings));
        }

        return new AbstractValue(kind, items, span);
    }

    private static AbstractValue ResolveAbstractDictValue(DictLiteralExpressionSyntax dict, AbstractState bindings)
    {
        var pairs = new List<KeyValuePair<AbstractValue, AbstractValue>>(dict.Items.Count);
        foreach (var item in dict.Items)
        {
            pairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                ResolveOrUnknown(item.Key, bindings),
                ResolveOrUnknown(item.Value, bindings)));
        }

        return AbstractValue.Dict(pairs, dict.Span);
    }

    private static AbstractValue ResolveListComprehensionValue(ListComprehensionExpressionSyntax listComprehension, AbstractState bindings)
    {
        var comprehensionBindings = StaticBindingEngine.BindComprehensionClauses(listComprehension.Clauses, bindings);
        return AbstractValue.ListOf(
            ResolveOrUnknown(listComprehension.ItemExpression, comprehensionBindings),
            listComprehension.Span);
    }

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

        if (binary.Operator is not (BinaryOperatorSyntax.Add or BinaryOperatorSyntax.Multiply) ||
            !TryResolve(binary.Left, bindings, out var left) ||
            !TryResolve(binary.Right, bindings, out var right))
        {
            value = default;
            return false;
        }

        if (binary.Operator == BinaryOperatorSyntax.Multiply)
        {
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
                ? AbstractValue.String((string)left.Value + (string)right.Value, binary.Span)
                : AbstractValue.StringType(binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.List && right.Kind == AbstractValueKind.List)
        {
            value = new AbstractValue(
                AbstractValueKind.List,
                ((IReadOnlyList<AbstractValue>)left.Value).Concat((IReadOnlyList<AbstractValue>)right.Value).ToArray(),
                binary.Span);
            return true;
        }

        if (TryGetListElementAbstractValue(left, out var leftItem) &&
            TryGetListElementAbstractValue(right, out var rightItem))
        {
            value = AbstractValue.ListOf(AbstractValue.Join(leftItem, rightItem, binary.Span), binary.Span);
            return true;
        }

        if (left.Kind == AbstractValueKind.Tuple && right.Kind == AbstractValueKind.Tuple)
        {
            value = new AbstractValue(
                AbstractValueKind.Tuple,
                ((IReadOnlyList<AbstractValue>)left.Value).Concat((IReadOnlyList<AbstractValue>)right.Value).ToArray(),
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

        value = default;
        return false;
    }

    private static bool TryResolveSequenceConstructorAbstractValue(
        string constructorName,
        CallExpressionSyntax call,
        AbstractState bindings,
        out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments))
        {
            value = default;
            return false;
        }

        if (arguments.Positional.Count == 0 && arguments.Keywords.Count == 0)
        {
            value = new AbstractValue(
                constructorName == "list" ? AbstractValueKind.List : AbstractValueKind.Tuple,
                Array.Empty<AbstractValue>(),
                call.Span);
            return true;
        }

        if (arguments.Positional.Count + arguments.Keywords.Count != 1 ||
            !arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            value = default;
            return false;
        }

        if (StaticBindingEngine.TryGetOrderedUnpackingItems(iterableExpression, bindings, out var items))
        {
            value = new AbstractValue(
                constructorName == "list" ? AbstractValueKind.List : AbstractValueKind.Tuple,
                items.Select(item => item.WithSpan(call.Span)).ToArray(),
                call.Span);
            return true;
        }

        if (constructorName == "list" &&
            StaticBindingEngine.TryGetIterableElementAbstractValue(iterableExpression, bindings, out var itemValue))
        {
            value = AbstractValue.ListOf(itemValue.WithSpan(call.Span), call.Span);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveLenAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            arguments.Positional.Count + arguments.Keywords.Count != 1 ||
            !arguments.TryGetValue(0, "value", out var valueExpression) ||
            !TryResolve(valueExpression, bindings, out var target) ||
            !StaticAbstractFacts.IsDefinitelySized(target))
        {
            value = default;
            return false;
        }

        value = AbstractValue.IntegerType(call.Span);
        return true;
    }

    private static bool TryResolveSortedAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !arguments.TryGetValue(0, "iterable", out var iterableExpression) ||
            !StaticBindingEngine.TryGetIterableElementAbstractValue(iterableExpression, bindings, out var itemValue))
        {
            value = default;
            return false;
        }

        value = AbstractValue.ListOf(itemValue.WithSpan(call.Span), call.Span);
        return true;
    }

    private static bool TryResolveSubscriptAbstractValue(SubscriptExpressionSyntax subscript, AbstractState bindings, out AbstractValue value)
    {
        if (!TryResolve(subscript.Target, bindings, out var target) ||
            StaticAbstractFacts.IsDefinitelyNonSubscriptable(target))
        {
            value = default;
            return false;
        }

        if (StaticAbstractFacts.RequiresIntegerIndex(target))
        {
            if (!TryResolve(subscript.Index, bindings, out var index))
            {
                return TryGetIndexedSequenceValue(target, null, subscript.Span, out value);
            }

            if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(index))
            {
                value = default;
                return false;
            }

            if (StaticAbstractFacts.TryGetNonNegativeInt32(index, out var indexValue))
            {
                return TryGetIndexedSequenceValue(target, indexValue, subscript.Span, out value);
            }

            return TryGetIndexedSequenceValue(target, null, subscript.Span, out value);
        }

        if (TryResolveOpenPyxlSubscriptValue(target, subscript, bindings, out value))
        {
            return true;
        }

        if (target.Kind == AbstractValueKind.Dict &&
            TryResolve(subscript.Index, bindings, out var key))
        {
            var pairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)target.Value;
            foreach (var pair in pairs)
            {
                if (AbstractValuesEqual(pair.Key, key))
                {
                    value = pair.Value.WithSpan(subscript.Span);
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryResolveOpenPyxlSubscriptValue(
        AbstractValue target,
        SubscriptExpressionSyntax subscript,
        AbstractState bindings,
        out AbstractValue value)
    {
        if (target.Kind == AbstractValueKind.OpenPyxlWorkbook)
        {
            if (!TryResolve(subscript.Index, bindings, out var index) ||
                index.Kind == AbstractValueKind.Unknown ||
                index.IsStringLike)
            {
                value = AbstractValue.OpenPyxlWorksheet(subscript.Span);
                return true;
            }
        }

        if (target.Kind == AbstractValueKind.OpenPyxlWorksheet &&
            TryResolveKnownString(subscript.Index, bindings, out var reference) &&
            IsOpenPyxlCellReference(reference))
        {
            value = AbstractValue.OpenPyxlCell(subscript.Span);
            return true;
        }

        if (target.Kind == AbstractValueKind.OpenPyxlTableCollection)
        {
            if (!TryResolve(subscript.Index, bindings, out var index) ||
                index.Kind == AbstractValueKind.Unknown ||
                index.IsStringLike)
            {
                value = AbstractValue.OpenPyxlTable(subscript.Span);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsOpenPyxlCellReference(string text)
    {
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var index = 0;
        while (index < normalized.Length && char.IsLetter(normalized[index]))
        {
            index++;
        }

        return index > 0 &&
            index < normalized.Length &&
            normalized.Skip(index).All(char.IsDigit);
    }

    private static bool TryResolveSliceAbstractValue(SliceExpressionSyntax slice, AbstractState bindings, out AbstractValue value)
    {
        if (!TryResolve(slice.Target, bindings, out var target) ||
            StaticAbstractFacts.IsDefinitelyNonSliceable(target) ||
            !AreValidSliceBounds(slice, bindings))
        {
            value = default;
            return false;
        }

        switch (target.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.StringType:
                value = AbstractValue.StringType(slice.Span);
                return true;
            case AbstractValueKind.Bytes:
            case AbstractValueKind.BytesType:
                value = AbstractValue.BytesType(slice.Span);
                return true;
            case AbstractValueKind.List:
                value = AbstractValue.ListOf(StaticBindingEngine.JoinSequenceItems((IReadOnlyList<AbstractValue>)target.Value, slice.Span), slice.Span);
                return true;
            case AbstractValueKind.ListType:
                value = AbstractValue.ListOf(((AbstractValue)target.Value).WithSpan(slice.Span), slice.Span);
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryGetIndexedSequenceValue(AbstractValue target, int? index, LythonSourceSpan span, out AbstractValue value)
    {
        switch (target.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.StringType:
                value = AbstractValue.StringType(span);
                return true;

            case AbstractValueKind.Bytes:
            case AbstractValueKind.BytesType:
                value = AbstractValue.IntegerType(span);
                return true;

            case AbstractValueKind.ListType:
                value = ((AbstractValue)target.Value).WithSpan(span);
                return true;

            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
            {
                var items = (IReadOnlyList<AbstractValue>)target.Value;
                if (index.HasValue && index.Value >= 0 && index.Value < items.Count)
                {
                    value = items[index.Value].WithSpan(span);
                    return true;
                }

                if (index is null)
                {
                    value = StaticBindingEngine.JoinSequenceItems(items, span);
                    return true;
                }

                value = default;
                return false;
            }

            default:
                value = default;
                return false;
        }
    }

    private static bool TryResolveCallAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "str" or "read_text" })
        {
            value = AbstractValue.StringType(call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "open" } &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var openArguments))
        {
            value = AbstractValue.TextFileHandle(TryGetTextFileMode(openArguments, modePosition: 1, "mode"), call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "len" } &&
            TryResolveLenAbstractValue(call, bindings, out value))
        {
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "bool" })
        {
            value = AbstractValue.BooleanType(call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "int" })
        {
            value = AbstractValue.IntegerType(call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "float" })
        {
            value = AbstractValue.FloatType(call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "bytes" })
        {
            value = AbstractValue.BytesType(call.Span);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "list" or "tuple" } constructor &&
            TryResolveSequenceConstructorAbstractValue(constructor.Name, call, bindings, out value))
        {
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "sorted" } &&
            TryResolveSortedAbstractValue(call, bindings, out value))
        {
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax functionIdentifier &&
            bindings.TryGet(functionIdentifier.Name, out var functionValue) &&
            functionValue.Kind == AbstractValueKind.Function &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var functionArguments) &&
            StaticBindingEngine.TryResolveFunctionCallReturn(
                (AbstractFunctionSummary)functionValue.Value,
                functionArguments,
                bindings,
                call.Span,
                out value))
        {
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax classIdentifier &&
            bindings.TryGet(classIdentifier.Name, out var classValue) &&
            classValue.Kind == AbstractValueKind.UserClass &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var classArguments) &&
            StaticBindingEngine.TryInstantiateUserClass((AbstractClassSummary)classValue.Value, classArguments, bindings, call.Span, out value))
        {
            return true;
        }

        if (call.Target is MemberExpressionSyntax { Target: var methodReceiver, MemberName: var methodName } &&
            TryResolve(methodReceiver, bindings, out var methodReceiverValue) &&
            methodReceiverValue.Kind == AbstractValueKind.UserInstance &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var methodArguments) &&
            StaticBindingEngine.TryResolveUserInstanceMethodReturn(methodReceiverValue, methodName, methodArguments, bindings, call.Span, out value))
        {
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var pathOpenReceiver,
                MemberName: "open"
            } &&
            TryResolveKnownPath(pathOpenReceiver, bindings) &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var pathOpenArguments))
        {
            value = AbstractValue.TextFileHandle(TryGetTextFileMode(pathOpenArguments, modePosition: 0, "mode"), call.Span);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: var parserName },
                MemberName: "add_mutually_exclusive_group"
            } &&
            bindings.TryGet(parserName, out var parserValue) &&
            parserValue.Kind == AbstractValueKind.ArgparseParser)
        {
            value = AbstractValue.ArgparseMutuallyExclusiveGroup(parserName, call.Span);
            return true;
        }

        if (StaticContractEngine.TryResolveCallReturn(call, bindings, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsLikelyPathConstructor(CallExpressionSyntax call)
    {
        var expression = call.Target;
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        return expression is IdentifierExpressionSyntax { Name: "Path" or "PurePath" or "PurePosixPath" or "PosixPath" } or
            MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "pathlib" },
                MemberName: "Path" or "PurePath" or "PurePosixPath" or "PosixPath"
            };
    }

    private static AbstractTextFileMode TryGetTextFileMode(ConcreteCallArguments arguments, int modePosition, string modeKeyword)
    {
        if (!arguments.TryGetValue(modePosition, modeKeyword, out var modeExpression) ||
            modeExpression is NoneLiteralExpressionSyntax)
        {
            return AbstractTextFileMode.Read;
        }

        return modeExpression switch
        {
            StringLiteralExpressionSyntax { Value: "r" } => AbstractTextFileMode.Read,
            StringLiteralExpressionSyntax { Value: "w" } => AbstractTextFileMode.Write,
            StringLiteralExpressionSyntax { Value: "a" } => AbstractTextFileMode.Append,
            _ => AbstractTextFileMode.Unknown
        };
    }

    private static bool TryGetListElementAbstractValue(AbstractValue value, out AbstractValue item)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.ListType:
                item = (AbstractValue)value.Value;
                return true;
            case AbstractValueKind.List:
                item = StaticBindingEngine.JoinSequenceItems((IReadOnlyList<AbstractValue>)value.Value, value.Span);
                return true;
            default:
                item = default;
                return false;
        }
    }

    private static bool AreValidSliceBounds(SliceExpressionSyntax slice, AbstractState bindings)
        => IsValidSliceBound(slice.Start, bindings) &&
           IsValidSliceBound(slice.End, bindings) &&
           IsValidSliceBound(slice.Step, bindings, rejectZero: true);

    private static bool IsValidSliceBound(ExpressionSyntax? expression, AbstractState bindings, bool rejectZero = false)
    {
        if (expression is null || !TryResolve(expression, bindings, out var value))
        {
            return true;
        }

        if (value.Kind == AbstractValueKind.None)
        {
            return true;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(value))
        {
            return false;
        }

        return !rejectZero || !StaticAbstractFacts.TryGetNonNegativeInt32(value, out var integer) || integer != 0;
    }

    private static bool AbstractValuesEqual(AbstractValue left, AbstractValue right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            AbstractValueKind.String or
            AbstractValueKind.Integer or
            AbstractValueKind.Float or
            AbstractValueKind.Boolean => Equals(left.Value, right.Value),
            AbstractValueKind.Bytes => ((byte[])left.Value).AsSpan().SequenceEqual((byte[])right.Value),
            AbstractValueKind.None => true,
            _ => false
        };
    }
}
