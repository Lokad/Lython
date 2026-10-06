namespace Lokad.Lython.Frontend;

internal static class ModernTypeSyntaxFacts
{
    public static IEnumerable<LythonDiagnostic> Validate(ScriptSyntax script)
    {
        var diagnostics = new List<LythonDiagnostic>();
        Statements(script.Statements, new HashSet<string>(StringComparer.Ordinal));
        return diagnostics;

        void Statements(IReadOnlyList<StatementSyntax> body, HashSet<string> typeNames)
        {
            foreach (var statement in body)
            {
                var parameters = statement switch
                {
                    TypeAliasStatementSyntax alias => alias.TypeParameters,
                    FunctionDefinitionStatementSyntax function => function.TypeParameters,
                    ClassDefinitionStatementSyntax type => type.TypeParameters,
                    _ => null,
                };
                if (parameters is not null)
                    foreach (var parameter in parameters)
                    {
                        if (parameter.Bound is not null) Annotation(parameter.Bound);
                        if (parameter.Default is not null) Annotation(parameter.Default);
                    }
                if (statement is TypeAliasStatementSyntax aliasStatement) Annotation(aliasStatement.Value);
                if (statement is FunctionDefinitionStatementSyntax { TypeParameters: not null } generic)
                {
                    foreach (var parameter in generic.Parameters)
                        if (parameter.Annotation is not null) Annotation(parameter.Annotation);
                    if (generic.ReturnAnnotation is not null) Annotation(generic.ReturnAnnotation);
                }
                if (statement is ClassDefinitionStatementSyntax { TypeParameters: not null } genericClass)
                {
                    if (genericClass.HeaderArguments is { } headers)
                        foreach (var header in headers) Annotation(header.Expression);
                    else
                    {
                        foreach (var header in genericClass.Bases) Annotation(header);
                        foreach (var header in genericClass.KeywordArguments) Annotation(header.Value);
                    }
                }
                if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Nonlocal } directive && directive.Names.Any(typeNames.Contains))
                    Error("Type parameters cannot be rebound with nonlocal.", directive.Span);
                var nestedNames = new HashSet<string>(typeNames, StringComparer.Ordinal);
                if (parameters is not null) nestedNames.UnionWith(parameters.Select(parameter => parameter.Name));
                foreach (var nested in StatementSyntaxTraversal.EnumerateChildBodies(statement)) Statements(nested, nestedNames);
            }
        }

        void Annotation(ExpressionSyntax expression)
        {
            if (expression is YieldExpressionSyntax or AssignmentExpressionSyntax)
                Error("Yield and assignment expressions are forbidden in annotation scopes.", expression.Span);
            if (expression is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is not null) Annotation(parameter.DefaultValue);
                return;
            }
            foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression)) Annotation(child);
        }

        void Error(string message, LythonSourceSpan span)
            => diagnostics.Add(new LythonDiagnostic("LA1100", message, LythonDiagnosticSeverity.Error, span));
    }
}
