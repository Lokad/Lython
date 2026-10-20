namespace Lokad.Lython.Frontend;

internal sealed class StaticHostRequirementDiagnostics(
    bool mayRebindInput,
    Runtime.MemoryGovernor.TemporaryMemoryReservation? storage,
    CancellationToken cancellationToken)
{
    private readonly List<StaticHostRequirement> _requirements = [];
    private long _capacityBytes;

    internal static StaticHostRequirement[] Collect(ScriptSyntax script, ScopeDirectiveFacts scopeFacts,
        Runtime.MemoryGovernor? governor, CancellationToken cancellationToken)
    {
        using var working = governor?.ReserveTemporary(0, null);
        var collector = new StaticHostRequirementDiagnostics(
            scopeFacts.LocalNames.Contains("input") || scopeFacts.GlobalNames.Contains("input"), working, cancellationToken);
        collector.AnalyzeHostExecutableStatements(script.Statements);
        collector.CheckCancellation(null);
        if (collector._requirements.Count == 0) return [];
        var bytes = checked(64L + 64L * collector._requirements.Count);
        working?.Grow(bytes, null);
        collector.CheckCancellation(null);
        var result = collector._requirements.ToArray();
        collector.CheckCancellation(null);
        working?.Commit(bytes);
        return result;
    }

    private void CheckCancellation(LythonSourceSpan? span)
    {
        if (cancellationToken.IsCancellationRequested) throw Runtime.RuntimeErrors.Cancelled(span);
    }

    private void Add(StaticHostRequirement requirement)
    {
        if (_requirements.Count == _requirements.Capacity)
        {
            var capacity = _requirements.Capacity == 0 ? 4 : checked(2 * _requirements.Capacity);
            var bytes = checked(64L + 64L * capacity);
            storage?.Grow(bytes, requirement.Span);
            CheckCancellation(requirement.Span);
            _requirements.Capacity = capacity;
            storage?.Shrink(_capacityBytes);
            _capacityBytes = bytes;
        }
        _requirements.Add(requirement);
    }

    private void AnalyzeHostExecutableStatements(
        IReadOnlyList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            AnalyzeHostExecutableStatement(statement);
        }
    }

    private void AnalyzeHostExecutableStatement(StatementSyntax statement)
    {
        CheckCancellation(statement.Span);
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                AnalyzeHostExecutableImport(importStatement);
                break;

            case AssignmentStatementSyntax assignment:
                AnalyzeHostExecutableExpression(assignment.Expression);
                break;

            case ChainedAssignmentStatementSyntax chained:
                AnalyzeHostExecutableExpression(chained.Expression);
                break;

            case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                switch (annotated.Target)
                {
                    case SubscriptAssignmentTargetSyntax subscript:
                        AnalyzeHostExecutableExpression(subscript.Target);
                        AnalyzeHostExecutableExpression(subscript.Index);
                        break;

                    case SliceAssignmentTargetSyntax slice:
                        AnalyzeHostExecutableExpression(slice.Target);
                        AnalyzeHostExecutableExpressionIfPresent(slice.Start);
                        AnalyzeHostExecutableExpressionIfPresent(slice.End);
                        AnalyzeHostExecutableExpressionIfPresent(slice.Step);
                        break;

                    case MemberAssignmentTargetSyntax member:
                        AnalyzeHostExecutableExpression(member.Target);
                        break;
                }

                AnalyzeHostExecutableExpression(annotated.Expression);
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                AnalyzeHostExecutableExpression(subscript.Target);
                AnalyzeHostExecutableExpression(subscript.Index);
                AnalyzeHostExecutableExpression(subscript.Expression);
                break;

            case SliceAssignmentStatementSyntax slice:
                AnalyzeHostExecutableExpression(slice.Target);
                AnalyzeHostExecutableExpressionIfPresent(slice.Start);
                AnalyzeHostExecutableExpressionIfPresent(slice.End);
                AnalyzeHostExecutableExpressionIfPresent(slice.Step);
                AnalyzeHostExecutableExpression(slice.Expression);
                break;

            case MemberAssignmentStatementSyntax member:
                AnalyzeHostExecutableExpression(member.Target);
                AnalyzeHostExecutableExpression(member.Expression);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                AnalyzeHostExecutableAssignmentTarget(augmented.Target);
                AnalyzeHostExecutableExpression(augmented.Expression);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                AnalyzeHostExecutableExpression(unpacking.Expression);
                break;

            case ExpressionStatementSyntax expressionStatement:
                AnalyzeHostExecutableExpression(expressionStatement.Expression);
                break;

            case WithStatementSyntax withStatement:
                AnalyzeHostExecutableExpression(withStatement.ContextExpression);
                AnalyzeHostExecutableStatements(withStatement.Body);
                break;

            case IfStatementSyntax ifStatement:
                AnalyzeHostExecutableExpression(ifStatement.Condition);
                if (TryGetBooleanLiteral(ifStatement.Condition, out var ifCondition))
                {
                    AnalyzeHostExecutableStatements(
                        ifCondition ? ifStatement.ThenStatements : ifStatement.ElseStatements ?? []);
                }
                break;

            case WhileStatementSyntax whileStatement:
                AnalyzeHostExecutableExpression(whileStatement.Condition);
                if (TryGetBooleanLiteral(whileStatement.Condition, out var whileCondition) && whileCondition)
                {
                    AnalyzeHostExecutableStatements(whileStatement.Body);
                }
                break;

            case AssertStatementSyntax assertStatement:
                AnalyzeHostExecutableExpression(assertStatement.Condition);
                if (assertStatement.Message is not null)
                {
                    AnalyzeHostExecutableExpression(assertStatement.Message);
                }
                break;

            case DeleteStatementSyntax deleteStatement:
                AnalyzeHostExecutableExpression(deleteStatement.Target);
                break;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null:
                AnalyzeHostExecutableExpression(returnStatement.Expression);
                break;

            case RaiseStatementSyntax raiseStatement:
                if (raiseStatement.Expression is not null) AnalyzeHostExecutableExpression(raiseStatement.Expression);
                if (raiseStatement.CauseExpression is not null) AnalyzeHostExecutableExpression(raiseStatement.CauseExpression);
                break;
        }
    }

    private void AnalyzeHostExecutableImport(ImportStatementSyntax statement)
    {
        if (StaticContracts.TryGetHostImportRequirement(statement, out var requirement))
        {
            Add(requirement);
        }
    }

    private void AnalyzeHostExecutableExpressionIfPresent(ExpressionSyntax? expression)
    {
        if (expression is not null)
        {
            AnalyzeHostExecutableExpression(expression);
        }
    }

    private void AnalyzeHostExecutableAssignmentTarget(AssignmentTargetSyntax target)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeHostExecutableExpression(subscript.Target);
                AnalyzeHostExecutableExpression(subscript.Index);
                break;

            case SliceAssignmentTargetSyntax slice:
                AnalyzeHostExecutableExpression(slice.Target);
                AnalyzeHostExecutableExpressionIfPresent(slice.Start);
                AnalyzeHostExecutableExpressionIfPresent(slice.End);
                AnalyzeHostExecutableExpressionIfPresent(slice.Step);
                break;

            case MemberAssignmentTargetSyntax member:
                AnalyzeHostExecutableExpression(member.Target);
                break;
        }
    }

    private void AnalyzeHostExecutableExpression(ExpressionSyntax expression)
    {
        CheckCancellation(expression.Span);
        switch (expression)
        {
            case ConditionalExpressionSyntax conditional:
                AnalyzeHostExecutableExpression(conditional.Condition);
                if (TryGetBooleanLiteral(conditional.Condition, out var condition))
                {
                    AnalyzeHostExecutableExpression(condition ? conditional.Consequent : conditional.Alternative);
                }
                return;

            case CallExpressionSyntax call:
                AnalyzeHostExecutableCall(call);
                break;

            // Creating a lambda is deferred; its body is not host-executable yet.
            // Creating a generator evaluates only its outermost iterable eagerly.
            case LambdaExpressionSyntax:
                return;

            case GeneratorExpressionSyntax generator:
                if (generator.Clauses.Count > 0)
                {
                    AnalyzeHostExecutableExpression(generator.Clauses[0].Iterable);
                }

                return;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            AnalyzeHostExecutableExpression(child);
        }
    }

    private void AnalyzeHostExecutableCall(CallExpressionSyntax call)
    {
        // Potential module rebinding makes identity uncertain even before the
        // binding runs. The real builtin still authorizes stdin at runtime.
        if (mayRebindInput && call.Target is IdentifierExpressionSyntax { Name: "input" }) return;
        if (StaticContracts.TryGetHostCallRequirement(call, out var requirement))
        {
            Add(requirement);
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
