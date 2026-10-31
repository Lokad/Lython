namespace Lokad.Lython.Frontend;

/// <summary>Provides statement expressions and child bodies shared by syntax-oriented frontend passes.</summary>
internal static class StatementSyntaxTraversal
{
    public static DirectExpressions EnumerateDirectExpressions(StatementSyntax statement) => new(statement);
    public static ChildBodies EnumerateChildBodies(StatementSyntax statement) => new(statement);

    internal readonly struct DirectExpressions(StatementSyntax statement)
    {
        public ExpressionEnumerator GetEnumerator() => new(statement);
        public bool Any(Func<ExpressionSyntax, bool> predicate)
        {
            foreach (var child in this) if (predicate(child)) return true;
            return false;
        }
    }

    internal struct ExpressionEnumerator(StatementSyntax statement) : IDisposable
    {
        private int _index;
        private IEnumerator<ExpressionSyntax>? _nested;
        public ExpressionSyntax Current { get; private set; } = null!;

        public bool MoveNext()
        {
            if (_index < 0) return false;
            if (statement is ChainedAssignmentStatementSyntax or AnnotatedAssignmentStatementSyntax or
                SliceAssignmentStatementSyntax or AugmentedAssignmentStatementSyntax or UnpackingAssignmentStatementSyntax or
                WithStatementSyntax or ForStatementSyntax or MatchStatementSyntax or
                FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax or TryStatementSyntax)
            {
                _nested ??= EnumerateComplexExpressions(statement).GetEnumerator();
                if (!_nested.MoveNext()) { _index = -1; return false; }
                Current = _nested.Current;
                return true;
            }
            var index = _index++;
            Current = (statement switch
            {
                AssignmentStatementSyntax item when index == 0 => item.Expression,
                SubscriptAssignmentStatementSyntax item when index == 0 => item.Target,
                SubscriptAssignmentStatementSyntax item when index == 1 => item.Index,
                SubscriptAssignmentStatementSyntax item when index == 2 => item.Expression,
                MemberAssignmentStatementSyntax item when index == 0 => item.Target,
                MemberAssignmentStatementSyntax item when index == 1 => item.Expression,
                ExpressionStatementSyntax item when index == 0 => item.Expression,
                IfStatementSyntax item when index == 0 => item.Condition,
                WhileStatementSyntax item when index == 0 => item.Condition,
                AssertStatementSyntax item when index == 0 => item.Condition,
                AssertStatementSyntax item when index == 1 => item.Message,
                DeleteStatementSyntax item when index == 0 => item.Target,
                ReturnStatementSyntax item when index == 0 => item.Expression,
                RaiseStatementSyntax item when index == 0 => item.Expression ?? item.CauseExpression,
                RaiseStatementSyntax item when index == 1 && item.Expression is not null => item.CauseExpression,
                _ => null
            })!;
            if (Current is null) { _index = -1; return false; }
            return true;
        }

        public void Dispose()
        {
            _index = -1;
            _nested?.Dispose();
            _nested = null;
            Current = null!;
        }
    }

    internal readonly struct ChildBodies(StatementSyntax statement)
    {
        public BodyEnumerator GetEnumerator() => new(statement);
        public bool Any(Func<IReadOnlyList<StatementSyntax>, bool> predicate)
        {
            foreach (var body in this) if (predicate(body)) return true;
            return false;
        }
    }

    internal struct BodyEnumerator(StatementSyntax statement)
    {
        private int _index;
        public IReadOnlyList<StatementSyntax> Current { get; private set; } = null!;
        public bool MoveNext()
        {
            if (_index < 0) return false;
            var index = _index++;
            Current = (statement switch
            {
                FunctionDefinitionStatementSyntax item when index == 0 => item.Body,
                ClassDefinitionStatementSyntax item when index == 0 => item.Body,
                IfStatementSyntax item when index == 0 => item.ThenStatements,
                IfStatementSyntax item when index == 1 => item.ElseStatements,
                ForStatementSyntax item when index == 0 => item.Body,
                ForStatementSyntax item when index == 1 => item.ElseStatements,
                WhileStatementSyntax item when index == 0 => item.Body,
                WhileStatementSyntax item when index == 1 => item.ElseStatements,
                WithStatementSyntax item when index == 0 => item.Body,
                TryStatementSyntax item when index == 0 => item.TryBody,
                TryStatementSyntax item when index <= item.ExceptClauses.Count => item.ExceptClauses[index - 1].Body,
                TryStatementSyntax item when index == item.ExceptClauses.Count + 1 => item.ElseBody ?? item.FinallyBody,
                TryStatementSyntax item when index == item.ExceptClauses.Count + 2 && item.ElseBody is not null => item.FinallyBody,
                MatchStatementSyntax item when index < item.Cases.Count => item.Cases[index].Body,
                _ => null
            })!;
            if (Current is null) { _index = -1; return false; }
            return true;
        }
    }

    /// <summary>
    /// Enumerates expressions evaluated directly by <paramref name="statement"/>, excluding expressions in child bodies.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> EnumerateComplexExpressions(StatementSyntax statement)
    {
        switch (statement)
        {
            case ChainedAssignmentStatementSyntax chained:
                yield return chained.Expression;
                foreach (var target in chained.Targets)
                    foreach (var read in AssignmentTargetFacts.Reads(target)) yield return read;
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                yield return annotated.Annotation;
                if (annotated.Expression is not null) yield return annotated.Expression;
                foreach (var read in EnumerateTargetExpressions(annotated.Target)) yield return read;
                break;

            case SliceAssignmentStatementSyntax slice:
                yield return slice.Target;
                if (slice.Start is not null) yield return slice.Start;
                if (slice.End is not null) yield return slice.End;
                if (slice.Step is not null) yield return slice.Step;
                yield return slice.Expression;
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                foreach (var expression in EnumerateTargetExpressions(augmented.Target)) yield return expression;
                yield return augmented.Expression;
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                yield return unpacking.Expression;
                foreach (var target in unpacking.Targets)
                    foreach (var read in AssignmentTargetFacts.Reads(AssignmentTargetFacts.FromUnpacking(target))) yield return read;
                break;

            case WithStatementSyntax withStatement:
                yield return withStatement.ContextExpression;
                if (withStatement.Target is not null)
                    foreach (var read in AssignmentTargetFacts.Reads(withStatement.Target)) yield return read;
                break;

            case ForStatementSyntax forStatement:
                yield return forStatement.Iterable;
                foreach (var read in AssignmentTargetFacts.Reads(forStatement.Target)) yield return read;
                break;

            case MatchStatementSyntax matchStatement:
                yield return matchStatement.Subject;
                foreach (var matchCase in matchStatement.Cases)
                {
                    if (matchCase.Guard is not null) yield return matchCase.Guard;
                }
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
                if (classDefinition.HeaderArguments is { } arguments)
                {
                    foreach (var argument in arguments) yield return argument.Expression;
                }
                else
                {
                    foreach (var @base in classDefinition.Bases) yield return @base;
                    foreach (var keywordArgument in classDefinition.KeywordArguments) yield return keywordArgument.Value;
                }
                break;

            case TryStatementSyntax tryStatement:
                foreach (var clause in tryStatement.ExceptClauses)
                    if (clause.ExceptionTypeExpression is not null) yield return clause.ExceptionTypeExpression;
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

}
