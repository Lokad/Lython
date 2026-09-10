namespace Lokad.Lython.Frontend;

/// <summary>Provides statement expressions and child bodies shared by syntax-oriented frontend passes.</summary>
internal static class StatementSyntaxTraversal
{
    /// <summary>
    /// Enumerates expressions evaluated directly by <paramref name="statement"/>, excluding expressions in child bodies.
    /// </summary>
    public static IEnumerable<ExpressionSyntax> EnumerateDirectExpressions(StatementSyntax statement)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax assignment:
                yield return assignment.Expression;
                break;

            case ChainedAssignmentStatementSyntax chained:
                yield return chained.Expression;
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                yield return annotated.Annotation;
                if (annotated.Expression is not null) yield return annotated.Expression;
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                yield return subscript.Target;
                yield return subscript.Index;
                yield return subscript.Expression;
                break;

            case SliceAssignmentStatementSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                yield return slice.Expression;
                break;

            case MemberAssignmentStatementSyntax member:
                yield return member.Target;
                yield return member.Expression;
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                foreach (var expression in EnumerateTargetExpressions(augmented.Target)) yield return expression;
                yield return augmented.Expression;
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                yield return unpacking.Expression;
                break;

            case ExpressionStatementSyntax expressionStatement:
                yield return expressionStatement.Expression;
                break;

            case WithStatementSyntax withStatement:
                yield return withStatement.ContextExpression;
                break;

            case IfStatementSyntax ifStatement:
                yield return ifStatement.Condition;
                break;

            case ForStatementSyntax forStatement:
                yield return forStatement.Iterable;
                break;

            case WhileStatementSyntax whileStatement:
                yield return whileStatement.Condition;
                break;

            case MatchStatementSyntax matchStatement:
                yield return matchStatement.Subject;
                foreach (var matchCase in matchStatement.Cases)
                {
                    if (matchCase.Guard is not null) yield return matchCase.Guard;
                }
                break;

            case AssertStatementSyntax assertStatement:
                yield return assertStatement.Condition;
                if (assertStatement.Message is not null) yield return assertStatement.Message;
                break;

            case DeleteStatementSyntax deleteStatement:
                yield return deleteStatement.Target;
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators) yield return decorator;
                foreach (var parameter in functionDefinition.Parameters)
                {
                    if (parameter.Annotation is not null) yield return parameter.Annotation;
                    if (parameter.DefaultValue is not null) yield return parameter.DefaultValue;
                }
                if (functionDefinition.ReturnAnnotation is not null) yield return functionDefinition.ReturnAnnotation;
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators) yield return decorator;
                foreach (var @base in classDefinition.Bases) yield return @base;
                foreach (var keywordArgument in classDefinition.KeywordArguments) yield return keywordArgument.Value;
                break;

            case ReturnStatementSyntax { Expression: not null } returnStatement:
                yield return returnStatement.Expression;
                break;

            case RaiseStatementSyntax { Expression: not null } raiseStatement:
                yield return raiseStatement.Expression;
                break;
        }

        static IEnumerable<ExpressionSyntax> EnumerateTargetExpressions(AssignmentTargetSyntax target)
        {
            switch (target)
            {
                case SubscriptAssignmentTargetSyntax subscript:
                    yield return subscript.Target;
                    yield return subscript.Index;
                    break;

                case SliceAssignmentTargetSyntax slice:
                    yield return slice.Target;
                    if (slice.Start is not null) yield return slice.Start;
                    if (slice.End is not null) yield return slice.End;
                    if (slice.Step is not null) yield return slice.Step;
                    break;

                case MemberAssignmentTargetSyntax member:
                    yield return member.Target;
                    break;
            }
        }
    }

    public static IEnumerable<IReadOnlyList<StatementSyntax>> EnumerateChildBodies(StatementSyntax statement)
    {
        switch (statement)
        {
            case FunctionDefinitionStatementSyntax functionDefinition:
                yield return functionDefinition.Body;
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                yield return classDefinition.Body;
                break;

            case IfStatementSyntax ifStatement:
                yield return ifStatement.ThenStatements;
                if (ifStatement.ElseStatements is not null) yield return ifStatement.ElseStatements;
                break;

            case ForStatementSyntax forStatement:
                yield return forStatement.Body;
                if (forStatement.ElseStatements is not null) yield return forStatement.ElseStatements;
                break;

            case WhileStatementSyntax whileStatement:
                yield return whileStatement.Body;
                if (whileStatement.ElseStatements is not null) yield return whileStatement.ElseStatements;
                break;

            case WithStatementSyntax withStatement:
                yield return withStatement.Body;
                break;

            case TryStatementSyntax tryStatement:
                yield return tryStatement.TryBody;
                foreach (var exceptClause in tryStatement.ExceptClauses) yield return exceptClause.Body;
                if (tryStatement.ElseBody is not null) yield return tryStatement.ElseBody;
                if (tryStatement.FinallyBody is not null) yield return tryStatement.FinallyBody;
                break;

            case MatchStatementSyntax matchStatement:
                foreach (var matchCase in matchStatement.Cases) yield return matchCase.Body;
                break;
        }
    }

}
