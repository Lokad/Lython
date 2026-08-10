using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    private static HashSet<string> CollectMutatedReceiverNames(StatementSyntax statement, AbstractState bindings)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        switch (statement)
        {
            case AssignmentStatementSyntax assignment:
                CollectMutatedReceiverNames(assignment.Expression, bindings, names);
                break;

            case ChainedAssignmentStatementSyntax chained:
                CollectMutatedReceiverNames(chained.Expression, bindings, names);
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                CollectMutatedReceiverNames(annotated.Annotation, bindings, names);
                CollectMutatedReceiverNames(annotated.Expression, bindings, names);
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                CollectMutatedReceiverNames(subscript.Expression, bindings, names);
                break;

            case SliceAssignmentStatementSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                CollectMutatedReceiverNames(slice.Expression, bindings, names);
                break;

            case MemberAssignmentStatementSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                CollectMutatedReceiverNames(member.Expression, bindings, names);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                CollectMutatedReceiverNames(augmented.Target, bindings, names);
                CollectMutatedReceiverNames(augmented.Expression, bindings, names);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                CollectMutatedReceiverNames(unpacking.Expression, bindings, names);
                break;

            case ExpressionStatementSyntax expressionStatement:
                CollectMutatedReceiverNames(expressionStatement.Expression, bindings, names);
                break;

            case WithStatementSyntax withStatement:
                CollectMutatedReceiverNames(withStatement.ContextExpression, bindings, names);
                break;

            case IfStatementSyntax ifStatement:
                CollectMutatedReceiverNames(ifStatement.Condition, bindings, names);
                break;

            case ForStatementSyntax forStatement:
                CollectMutatedReceiverNames(forStatement.Iterable, bindings, names);
                break;

            case WhileStatementSyntax whileStatement:
                CollectMutatedReceiverNames(whileStatement.Condition, bindings, names);
                break;

            case MatchStatementSyntax matchStatement:
                CollectMutatedReceiverNames(matchStatement.Subject, bindings, names);
                foreach (var matchCase in matchStatement.Cases)
                {
                    CollectMutatedReceiverNames(matchCase.Guard, bindings, names);
                }
                break;

            case AssertStatementSyntax assertStatement:
                CollectMutatedReceiverNames(assertStatement.Condition, bindings, names);
                CollectMutatedReceiverNames(assertStatement.Message, bindings, names);
                break;

            case DeleteStatementSyntax deleteStatement:
                CollectMutatedReceiverNames(deleteStatement.Target, bindings, names);
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators)
                {
                    CollectMutatedReceiverNames(decorator, bindings, names);
                }

                foreach (var parameter in functionDefinition.Parameters)
                {
                    CollectMutatedReceiverNames(parameter.Annotation, bindings, names);
                    CollectMutatedReceiverNames(parameter.DefaultValue, bindings, names);
                }

                CollectMutatedReceiverNames(functionDefinition.ReturnAnnotation, bindings, names);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators)
                {
                    CollectMutatedReceiverNames(decorator, bindings, names);
                }

                foreach (var @base in classDefinition.Bases)
                {
                    CollectMutatedReceiverNames(@base, bindings, names);
                }

                foreach (var keywordArgument in classDefinition.KeywordArguments)
                {
                    CollectMutatedReceiverNames(keywordArgument.Value, bindings, names);
                }
                break;

            case ReturnStatementSyntax returnStatement:
                CollectMutatedReceiverNames(returnStatement.Expression, bindings, names);
                break;

            case RaiseStatementSyntax raiseStatement:
                CollectMutatedReceiverNames(raiseStatement.Expression, bindings, names);
                break;
        }

        return names;
    }

    private static void CollectMutatedReceiverNames(AssignmentTargetSyntax target, AbstractState bindings, HashSet<string> names)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                break;

            case SliceAssignmentTargetSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                break;

            case MemberAssignmentTargetSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                break;
        }
    }

    private static void CollectMutatedReceiverNames(ExpressionSyntax? expression, AbstractState bindings, HashSet<string> names)
    {
        if (expression is null)
        {
            return;
        }

        switch (expression)
        {
            case FormattedStringExpressionSyntax formatted:
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    CollectMutatedReceiverNames(nestedExpression, bindings, names);
                }
                break;

            case ListLiteralExpressionSyntax list:
                CollectMutatedReceiverNames(list.Items, bindings, names);
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                foreach (var clause in listComprehension.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(listComprehension.ItemExpression, bindings, names);
                break;

            case GeneratorExpressionSyntax generator:
                foreach (var clause in generator.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(generator.ItemExpression, bindings, names);
                break;

            case SetComprehensionExpressionSyntax setComprehension:
                foreach (var clause in setComprehension.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(setComprehension.ItemExpression, bindings, names);
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                foreach (var clause in dictComprehension.Clauses)
                {
                    CollectMutatedReceiverNames(clause.Iterable, bindings, names);
                    CollectMutatedReceiverNames(clause.Condition, bindings, names);
                }
                CollectMutatedReceiverNames(dictComprehension.KeyExpression, bindings, names);
                CollectMutatedReceiverNames(dictComprehension.ValueExpression, bindings, names);
                break;

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    CollectMutatedReceiverNames(item.Key, bindings, names);
                    if (!item.IsUnpacking)
                    {
                        CollectMutatedReceiverNames(item.Value, bindings, names);
                    }
                }
                break;

            case SetLiteralExpressionSyntax set:
                CollectMutatedReceiverNames(set.Items, bindings, names);
                break;

            case TupleLiteralExpressionSyntax tuple:
                CollectMutatedReceiverNames(tuple.Items, bindings, names);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                CollectMutatedReceiverNames(parenthesized.Inner, bindings, names);
                break;

            case MemberExpressionSyntax member:
                CollectMutatedReceiverNames(member.Target, bindings, names);
                break;

            case CallExpressionSyntax call:
                CollectMutatingCallReceiverName(call, bindings, names);
                CollectMutatedReceiverNames(call.Target, bindings, names);
                foreach (var argument in call.Arguments)
                {
                    CollectMutatedReceiverNames(argument.Expression, bindings, names);
                }
                break;

            case SubscriptExpressionSyntax subscript:
                CollectMutatedReceiverNames(subscript.Target, bindings, names);
                CollectMutatedReceiverNames(subscript.Index, bindings, names);
                break;

            case SliceExpressionSyntax slice:
                CollectMutatedReceiverNames(slice.Target, bindings, names);
                CollectMutatedReceiverNames(slice.Start, bindings, names);
                CollectMutatedReceiverNames(slice.End, bindings, names);
                CollectMutatedReceiverNames(slice.Step, bindings, names);
                break;

            case BinaryExpressionSyntax binary:
                CollectMutatedReceiverNames(binary.Left, bindings, names);
                CollectMutatedReceiverNames(binary.Right, bindings, names);
                break;

            case ChainedComparisonExpressionSyntax chained:
                CollectMutatedReceiverNames(chained.Operands, bindings, names);
                break;

            case UnaryExpressionSyntax unary:
                CollectMutatedReceiverNames(unary.Operand, bindings, names);
                break;

            case ConditionalExpressionSyntax conditional:
                CollectMutatedReceiverNames(conditional.Condition, bindings, names);
                CollectMutatedReceiverNames(conditional.Consequent, bindings, names);
                CollectMutatedReceiverNames(conditional.Alternative, bindings, names);
                break;

            case AssignmentExpressionSyntax assignment:
                CollectMutatedReceiverNames(assignment.Expression, bindings, names);
                break;
        }
    }

    private static void CollectMutatedReceiverNames(IReadOnlyList<ExpressionSyntax> expressions, AbstractState bindings, HashSet<string> names)
    {
        foreach (var expression in expressions)
        {
            CollectMutatedReceiverNames(expression, bindings, names);
        }
    }

    private static void CollectMutatedReceiverNames(IReadOnlyList<CollectionDisplayItemSyntax> items, AbstractState bindings, HashSet<string> names)
    {
        foreach (var item in items)
        {
            CollectMutatedReceiverNames(item.Expression, bindings, names);
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
