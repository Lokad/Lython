namespace Lokad.Lython.Frontend;

internal static class StaticHostRequirementDiagnostics
{
    public static void Analyze(StaticAnalysisContext context, ILythonHost host)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(host);
        AnalyzeHostExecutableStatements(context.Script.Statements, context, host);
    }

    private static void AnalyzeHostExecutableStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        ILythonHost host)
    {
        foreach (var statement in statements)
        {
            AnalyzeHostExecutableStatement(statement, context, host);
        }
    }

    private static void AnalyzeHostExecutableStatement(StatementSyntax statement, StaticAnalysisContext context, ILythonHost host)
    {
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                AnalyzeHostExecutableImport(importStatement, context, host);
                break;

            case AssignmentStatementSyntax assignment:
                AnalyzeHostExecutableExpression(assignment.Expression, context, host);
                break;

            case ChainedAssignmentStatementSyntax chained:
                AnalyzeHostExecutableExpression(chained.Expression, context, host);
                break;

            case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                AnalyzeHostExecutableExpression(annotated.Expression, context, host);
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                AnalyzeHostExecutableExpression(subscript.Target, context, host);
                AnalyzeHostExecutableExpression(subscript.Index, context, host);
                AnalyzeHostExecutableExpression(subscript.Expression, context, host);
                break;

            case SliceAssignmentStatementSyntax slice:
                AnalyzeHostExecutableExpression(slice.Target, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.Start, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.End, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.Step, context, host);
                AnalyzeHostExecutableExpression(slice.Expression, context, host);
                break;

            case MemberAssignmentStatementSyntax member:
                AnalyzeHostExecutableExpression(member.Target, context, host);
                AnalyzeHostExecutableExpression(member.Expression, context, host);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                AnalyzeHostExecutableAssignmentTarget(augmented.Target, context, host);
                AnalyzeHostExecutableExpression(augmented.Expression, context, host);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                AnalyzeHostExecutableExpression(unpacking.Expression, context, host);
                break;

            case ExpressionStatementSyntax expressionStatement:
                AnalyzeHostExecutableExpression(expressionStatement.Expression, context, host);
                break;

            case WithStatementSyntax withStatement:
                AnalyzeHostExecutableExpression(withStatement.ContextExpression, context, host);
                AnalyzeHostExecutableStatements(withStatement.Body, context, host);
                break;

            case IfStatementSyntax ifStatement:
                AnalyzeHostExecutableExpression(ifStatement.Condition, context, host);
                if (TryGetBooleanLiteral(ifStatement.Condition, out var ifCondition))
                {
                    AnalyzeHostExecutableStatements(
                        ifCondition ? ifStatement.ThenStatements : ifStatement.ElseStatements ?? [],
                        context,
                        host);
                }
                break;

            case WhileStatementSyntax whileStatement:
                AnalyzeHostExecutableExpression(whileStatement.Condition, context, host);
                if (TryGetBooleanLiteral(whileStatement.Condition, out var whileCondition) && whileCondition)
                {
                    AnalyzeHostExecutableStatements(whileStatement.Body, context, host);
                }
                break;

            case AssertStatementSyntax assertStatement:
                AnalyzeHostExecutableExpression(assertStatement.Condition, context, host);
                if (assertStatement.Message is not null)
                {
                    AnalyzeHostExecutableExpression(assertStatement.Message, context, host);
                }
                break;

            case DeleteStatementSyntax deleteStatement:
                AnalyzeHostExecutableExpression(deleteStatement.Target, context, host);
                break;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null:
                AnalyzeHostExecutableExpression(returnStatement.Expression, context, host);
                break;

            case RaiseStatementSyntax raiseStatement:
                AnalyzeHostExecutableExpression(raiseStatement.Expression, context, host);
                break;
        }
    }

    private static void AnalyzeHostExecutableImport(ImportStatementSyntax statement, StaticAnalysisContext context, ILythonHost host)
    {
        if (StaticContracts.TryGetHostImportRequirement(statement, out var requirement))
        {
            StaticContractEngine.TryAddUnsatisfiedHostRequirement(context, requirement, host);
        }
    }

    private static void AnalyzeHostExecutableExpressionIfPresent(ExpressionSyntax? expression, StaticAnalysisContext context, ILythonHost host)
    {
        if (expression is not null)
        {
            AnalyzeHostExecutableExpression(expression, context, host);
        }
    }

    private static void AnalyzeHostExecutableAssignmentTarget(AssignmentTargetSyntax target, StaticAnalysisContext context, ILythonHost host)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeHostExecutableExpression(subscript.Target, context, host);
                AnalyzeHostExecutableExpression(subscript.Index, context, host);
                break;

            case SliceAssignmentTargetSyntax slice:
                AnalyzeHostExecutableExpression(slice.Target, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.Start, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.End, context, host);
                AnalyzeHostExecutableExpressionIfPresent(slice.Step, context, host);
                break;

            case MemberAssignmentTargetSyntax member:
                AnalyzeHostExecutableExpression(member.Target, context, host);
                break;
        }
    }

    private static void AnalyzeHostExecutableExpression(ExpressionSyntax expression, StaticAnalysisContext context, ILythonHost host)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                AnalyzeHostExecutableExpression(parenthesized.Inner, context, host);
                break;

            case FormattedStringExpressionSyntax formatted:
                foreach (var part in formatted.Parts)
                {
                    if (part is FormattedStringExpressionPartSyntax expressionPart)
                    {
                        AnalyzeHostExecutableExpression(expressionPart.Expression, context, host);
                    }
                }
                break;

            case ListLiteralExpressionSyntax list:
                foreach (var item in list.Items)
                {
                    AnalyzeHostExecutableExpression(item, context, host);
                }
                break;

            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items)
                {
                    AnalyzeHostExecutableExpression(item, context, host);
                }
                break;

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    AnalyzeHostExecutableExpression(item.Key, context, host);
                    AnalyzeHostExecutableExpression(item.Value, context, host);
                }
                break;

            case SetLiteralExpressionSyntax set:
                foreach (var item in set.Items)
                {
                    AnalyzeHostExecutableExpression(item, context, host);
                }
                break;

            case MemberExpressionSyntax member:
                AnalyzeHostExecutableExpression(member.Target, context, host);
                break;

            case SubscriptExpressionSyntax subscript:
                AnalyzeHostExecutableExpression(subscript.Target, context, host);
                AnalyzeHostExecutableExpression(subscript.Index, context, host);
                break;

            case SliceExpressionSyntax slice:
                AnalyzeHostExecutableExpression(slice.Target, context, host);
                if (slice.Start is not null) AnalyzeHostExecutableExpression(slice.Start, context, host);
                if (slice.End is not null) AnalyzeHostExecutableExpression(slice.End, context, host);
                if (slice.Step is not null) AnalyzeHostExecutableExpression(slice.Step, context, host);
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeHostExecutableExpression(binary.Left, context, host);
                AnalyzeHostExecutableExpression(binary.Right, context, host);
                break;

            case UnaryExpressionSyntax unary:
                AnalyzeHostExecutableExpression(unary.Operand, context, host);
                break;

            case ConditionalExpressionSyntax conditional:
                AnalyzeHostExecutableExpression(conditional.Condition, context, host);
                if (TryGetBooleanLiteral(conditional.Condition, out var condition))
                {
                    AnalyzeHostExecutableExpression(condition ? conditional.Consequent : conditional.Alternative, context, host);
                }
                break;

            case AssignmentExpressionSyntax assignment:
                AnalyzeHostExecutableExpression(assignment.Expression, context, host);
                break;

            case CallExpressionSyntax call:
                AnalyzeHostExecutableCall(call, context, host);
                AnalyzeHostExecutableExpression(call.Target, context, host);
                foreach (var argument in call.Arguments)
                {
                    AnalyzeHostExecutableExpression(argument.Expression, context, host);
                }
                break;
        }
    }

    private static void AnalyzeHostExecutableCall(CallExpressionSyntax call, StaticAnalysisContext context, ILythonHost host)
    {
        if (StaticContracts.TryGetHostCallRequirement(call, out var requirement))
        {
            StaticContractEngine.TryAddUnsatisfiedHostRequirement(context, requirement, host);
        }
    }

    private static bool TryGetBooleanLiteral(ExpressionSyntax expression, out bool value)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionSyntax boolean:
                value = boolean.Value;
                return true;
            case ParenthesizedExpressionSyntax parenthesized:
                return TryGetBooleanLiteral(parenthesized.Inner, out value);
            default:
                value = false;
                return false;
        }
    }

}
