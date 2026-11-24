namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static bool TryResolveCallAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is IdentifierExpressionSyntax identifier)
        {
            if (bindings.TryGet(identifier.Name, out var target))
            {
                switch (target.Kind)
                {
                    case AbstractValueKind.Function when StaticCallArguments.TryGetConcreteArguments(call, bindings, out var functionArguments):
                        if (StaticBindingEngine.TryResolveFunctionCallReturn(
                                target.RequireFunctionSummary(), functionArguments, bindings, call.Span, out value))
                            return true;
                        break;
                    case AbstractValueKind.UserClass when StaticCallArguments.TryGetConcreteArguments(call, bindings, out var classArguments):
                        if (StaticBindingEngine.TryInstantiateUserClass(
                                target.RequireClassSummary(), classArguments, bindings, call.Span, out value))
                            return true;
                        break;
                }
            }
            else if (bindings.CanUseUnboundBuiltin(identifier.Name) &&
                TryResolveBuiltinCallReturn(identifier.Name, call, bindings, out value))
            {
                return true;
            }
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
            value = ResolveFileHandle(pathOpenArguments, modePosition: 0, bindings, call.Span);
            return true;
        }

        if (StaticContractEngine.TryResolveCallReturn(call, bindings, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveBuiltinCallReturn(string name, CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        switch (name)
        {
            case "str":
                value = AbstractValue.StringType(call.Span);
                return true;
            case "open" when StaticCallArguments.TryGetConcreteArguments(call, bindings, out var openArguments):
                value = ResolveFileHandle(openArguments, modePosition: 1, bindings, call.Span);
                return true;
            case "len":
                return TryResolveLenAbstractValue(call, bindings, out value);
            case "bool":
                value = AbstractValue.BooleanType(call.Span);
                return true;
            case "int":
                value = AbstractValue.IntegerType(call.Span);
                return true;
            case "float":
                value = AbstractValue.FloatType(call.Span);
                return true;
            case "bytes":
                value = AbstractValue.BytesType(call.Span);
                return true;
            case "list" or "tuple":
                return TryResolveSequenceConstructorAbstractValue(name, call, bindings, out value);
            case "sorted":
                return TryResolveSortedAbstractValue(call, bindings, out value);
            default:
                value = default;
                return false;
        }
    }

    private static AbstractValue ResolveFileHandle(ConcreteCallArguments arguments, int modePosition,
        AbstractState bindings, LythonSourceSpan span)
    {
        if (!arguments.TryGetValue(modePosition, "mode", out var expression))
            return AbstractValue.TextFileHandle(AbstractTextFileMode.Read, span);
        if (!TryResolveKnownString(expression, bindings, out var mode))
            return AbstractValue.Unknown(span);
        var operation = mode switch
        {
            "r" or "rt" or "rb" or "br" => AbstractTextFileMode.Read,
            "w" or "wt" or "wb" or "bw" => AbstractTextFileMode.Write,
            "a" or "at" or "ab" or "ba" => AbstractTextFileMode.Append,
            _ => AbstractTextFileMode.Unknown,
        };
        return mode.Contains('b', StringComparison.Ordinal)
            ? AbstractValue.BinaryFileHandle(operation, span) : AbstractValue.TextFileHandle(operation, span);
    }

    private static bool TryGetListElementAbstractValue(AbstractValue value, StaticAnalysisContext? context, out AbstractValue item)
    {
        switch (value.Kind)
        {
            case AbstractValueKind.ListType:
                item = value.RequireNestedValue();
                return true;
            case AbstractValueKind.List:
                item = StaticBindingEngine.JoinSequenceItems(value.RequireSequenceItems(), value.Span, context);
                return true;
            default:
                item = default;
                return false;
        }
    }

}
