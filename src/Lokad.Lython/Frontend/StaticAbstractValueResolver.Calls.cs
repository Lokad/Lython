namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static bool TryResolveCallAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "str" })
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
                functionValue.RequireFunctionSummary(),
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
            StaticBindingEngine.TryInstantiateUserClass(classValue.RequireClassSummary(), classArguments, bindings, call.Span, out value))
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

    private static AbstractTextFileMode TryGetTextFileMode(ConcreteCallArguments arguments, int modePosition, string modeKeyword)
    {
        if (!arguments.TryGetValue(modePosition, modeKeyword, out var modeExpression))
        {
            return AbstractTextFileMode.Read;
        }

        return modeExpression switch
        {
            StringLiteralExpressionSyntax { Value: "r" or "rt" } => AbstractTextFileMode.Read,
            StringLiteralExpressionSyntax { Value: "w" or "wt" } => AbstractTextFileMode.Write,
            StringLiteralExpressionSyntax { Value: "a" or "at" } => AbstractTextFileMode.Append,
            _ => AbstractTextFileMode.Unknown
        };
    }

    private static bool TryGetListElementAbstractValue(AbstractValue value, out AbstractValue item)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.ListType:
                item = value.RequireNestedValue();
                return true;
            case AbstractValueKind.List:
                item = StaticBindingEngine.JoinSequenceItems(value.RequireSequenceItems(), value.Span);
                return true;
            default:
                item = default;
                return false;
        }
    }

}
