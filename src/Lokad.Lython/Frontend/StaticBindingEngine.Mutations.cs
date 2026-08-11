namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
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
    }

    private static void CollectMutatingCallReceiverName(CallExpressionSyntax call, AbstractState bindings, HashSet<string> names)
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
