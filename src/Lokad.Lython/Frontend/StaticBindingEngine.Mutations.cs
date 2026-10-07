namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    public static bool CallMayMutateDictionaryFacts(CallExpressionSyntax call, AbstractState bindings)
    {
        if (!call.Arguments.All(argument =>
            StaticAbstractValueResolver.TryResolve(argument.Expression, bindings, out var value) && IsCallbackFreeValue(value)))
        {
            return true;
        }

        if (StaticContractEngine.TryResolveKnownCallableTarget(call.Target, bindings, out _))
        {
            // Module helpers can mutate supplied containers (operator.ior,
            // heapq operations, etc.). Their member contracts do not describe
            // this effect, so keep only calls without reachable mapping facts.
            return call.Arguments.Any(argument =>
                AbstractState.ContainsDictionaryFacts(StaticAbstractValueResolver.ResolveOrUnknown(argument.Expression, bindings)));
        }

        return call.Target is not MemberExpressionSyntax member ||
            !StaticAbstractValueResolver.TryResolve(member.Target, bindings, out var receiver) ||
            !IsCallbackFreeValue(receiver) ||
            !StaticContracts.TryGetCallableContract(receiver, member.MemberName, out var contract) ||
            contract.Mutation == StaticMutationKind.MutatesReceiver;
    }

    private static bool IsCallbackFreeValue(AbstractValue value)
        => value.Kind switch
        {
            AbstractValueKind.String or AbstractValueKind.StringType or
            AbstractValueKind.Bytes or AbstractValueKind.BytesType or
            AbstractValueKind.Integer or AbstractValueKind.IntegerType or
            AbstractValueKind.Float or AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or AbstractValueKind.BooleanType or
            AbstractValueKind.None => true,
            AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set =>
                value.RequireSequenceItems().All(IsCallbackFreeValue),
            AbstractValueKind.Dict => value.RequireDictionaryItems().All(pair =>
                IsCallbackFreeValue(pair.Key) && IsCallbackFreeValue(pair.Value)),
            _ => false,
        };

    private static HashSet<string> CollectMutatedReceiverNames(StatementSyntax statement, AbstractState bindings)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
        {
            CollectMutatedReceiverNames(expression, bindings, names);
        }

        return names;
    }

    private static void CollectMutatedReceiverNames(ExpressionSyntax? expression, AbstractState bindings, HashSet<string> names)
    {
        if (expression is null)
        {
            return;
        }

        // Lambda bodies execute in a later scope, so structural traversal must stop
        // here even though the shared child inventory exposes that body to other passes.
        if (expression is LambdaExpressionSyntax)
        {
            return;
        }

        if (expression is CallExpressionSyntax call)
        {
            CollectMutatingCallReceiverName(call, bindings, names);
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            CollectMutatedReceiverNames(child, bindings, names);
        }

        static void CollectMutatingCallReceiverName(CallExpressionSyntax call, AbstractState bindings, HashSet<string> names)
        {
            if (call.Target is MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "operator" },
                    MemberName: "setitem"
                } &&
                StaticCallArguments.TryGetConcreteArguments(call, bindings, out var operatorArguments) &&
                operatorArguments.Positional.Count > 0 &&
                operatorArguments.Positional[0] is IdentifierExpressionSyntax operatorReceiver)
            {
                names.Add(operatorReceiver.Name);
                return;
            }

            if (call.Target is not MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax receiverIdentifier,
                    MemberName: var memberName
                } ||
                !bindings.TryGet(receiverIdentifier.Name, out var receiver) ||
                !StaticContracts.IsMutatingMember(receiver, memberName))
            {
                return;
            }

            if (StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) &&
                StaticContracts.TryGetCallableContract(receiver, memberName, out var contract) &&
                !contract.AcceptsArgumentShape(arguments))
            {
                return;
            }

            names.Add(receiverIdentifier.Name);
        }
    }

}
