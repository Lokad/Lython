namespace Lokad.Lython.Frontend;

internal static class StaticHostRequirementDiagnostics
{
    public static void Analyze(StaticAnalysisContext context, ILythonHost host)
    {
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

            case RaiseStatementSyntax { Expression: not null } raiseStatement:
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
            case ConditionalExpressionSyntax conditional:
                AnalyzeHostExecutableExpression(conditional.Condition, context, host);
                if (TryGetBooleanLiteral(conditional.Condition, out var condition))
                {
                    AnalyzeHostExecutableExpression(condition ? conditional.Consequent : conditional.Alternative, context, host);
                }
                return;

            case CallExpressionSyntax call:
                AnalyzeHostExecutableCall(call, context, host);
                break;

            // Creating a lambda is deferred; its body is not host-executable yet.
            // Creating a generator evaluates only its outermost iterable eagerly.
            case LambdaExpressionSyntax:
                return;

            case GeneratorExpressionSyntax generator:
                if (generator.Clauses.Count > 0)
                {
                    AnalyzeHostExecutableExpression(generator.Clauses[0].Iterable, context, host);
                }

                return;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            AnalyzeHostExecutableExpression(child, context, host);
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
