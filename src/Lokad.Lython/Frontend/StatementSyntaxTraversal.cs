namespace Lokad.Lython.Frontend;

/// <summary>Provides the direct statement bodies shared by syntax-oriented frontend passes.</summary>
internal static class StatementSyntaxTraversal
{
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
                if (tryStatement.ExceptBody is not null) yield return tryStatement.ExceptBody;
                if (tryStatement.ElseBody is not null) yield return tryStatement.ElseBody;
                if (tryStatement.FinallyBody is not null) yield return tryStatement.FinallyBody;
                break;

            case MatchStatementSyntax matchStatement:
                foreach (var matchCase in matchStatement.Cases) yield return matchCase.Body;
                break;
        }
    }
}
