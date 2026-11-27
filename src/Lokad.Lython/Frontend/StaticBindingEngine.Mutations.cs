namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    public static bool CallMayMutateContainerFacts(CallExpressionSyntax call, AbstractState bindings)
    {
        var callbackFreeArguments = call.Arguments.All(argument =>
            StaticAbstractValueResolver.TryResolve(argument.Expression, bindings, out var value) &&
            AbstractState.IsCallbackFreeValue(value, bindings.AnalysisContext));
        if (!callbackFreeArguments)
        {
            return true;
        }

        if (bindings.UnshadowedBuiltinName(call.Target) is "len" or "abs" or "chr" or "ord" or "hex" or "oct" or "bin" or
                "int" or "float" or "bool" or "str" or "repr" or "type")
        {
            return false;
        }

        if (StaticContractEngine.TryResolveKnownCallableTarget(call.Target, bindings, out _))
        {
            // Module helpers can mutate supplied containers (operator.ior,
            // heapq operations, etc.). Their member contracts do not describe
            // this effect, so keep only calls without reachable container facts.
            return call.Arguments.Any(argument =>
                AbstractState.ContainsMutableCollectionFacts(
                    StaticAbstractValueResolver.ResolveOrUnknown(argument.Expression, bindings), bindings.AnalysisContext));
        }

        return call.Target is not MemberExpressionSyntax member ||
            !StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiver) ||
            !AbstractState.IsCallbackFreeValue(receiver, bindings.AnalysisContext) ||
            !StaticContracts.TryGetCallableContract(receiver, member.MemberName, out var contract) ||
            contract.Mutation == StaticMutationKind.MutatesReceiver;
    }

    private static HashSet<string>? CollectMutatedReceiverNames(StatementSyntax statement, AbstractState bindings)
    {
        var calls = bindings.AnalysisContext?.GetPotentialMutations(statement);
        if (calls is null) return null;
        HashSet<string>? names = null;
        foreach (var call in calls)
        {
            if (GetMutatingReceiverName(call, bindings) is { } name)
                (names ??= new(StringComparer.Ordinal)).Add(name);
        }
        return names;
    }

    private static string? GetMutatingReceiverName(CallExpressionSyntax call, AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax { Target: IdentifierExpressionSyntax receiver, MemberName: var member })
            return null;
        if (receiver.Name == "operator" && member == "setitem" &&
            StaticCallArguments.TryGetConcreteArguments(call, bindings, out var operatorArguments) &&
            operatorArguments.PositionalCount > 0 && operatorArguments.GetPositional(0) is IdentifierExpressionSyntax itemReceiver)
        {
            return itemReceiver.Name;
        }

        if (!bindings.TryGet(receiver.Name, out var value) || !StaticContracts.IsMutatingMember(value, member))
            return null;
        if (StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) &&
            StaticContracts.TryGetCallableContract(value, member, out var contract) &&
            !contract.AcceptsArgumentShape(arguments))
        {
            return null;
        }
        return receiver.Name;
    }
}
