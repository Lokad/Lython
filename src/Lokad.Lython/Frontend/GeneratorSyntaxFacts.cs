namespace Lokad.Lython.Frontend;

internal sealed class GeneratorLoweringException(string message, LythonSourceSpan span) : Exception("Unsupported generator suspension: " + message)
{
    public LythonSourceSpan Span { get; } = span;
}

internal static class GeneratorSyntaxFacts
{
    public static bool ContainsYield(ExpressionSyntax expression)
        => expression is LambdaExpressionSyntax lambda
            ? lambda.Parameters.Any(parameter => parameter.DefaultValue is not null && ContainsYield(parameter.DefaultValue))
            : expression is YieldExpressionSyntax || ExpressionSyntaxTraversal.EnumerateChildren(expression).Any(ContainsYield);

    public static bool IsGenerator(IReadOnlyList<StatementSyntax> body)
        => body.Any(statement => StatementSyntaxTraversal.EnumerateDirectExpressions(statement).Any(ContainsYield) ||
            statement is not (FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax) &&
            StatementSyntaxTraversal.EnumerateChildBodies(statement).Any(IsGenerator));

    public static IEnumerable<LythonDiagnostic> Validate(ScriptSyntax script)
    {
        var errors = new List<LythonDiagnostic>();
        var postponed = script.Statements.OfType<ImportStatementSyntax>().Any(i =>
            i.ModuleName == "__future__" && i.ImportedMembers?.Any(m => m.Name == "annotations") == true);
        Statements(script.Statements, false);
        return errors;

        void Statements(IReadOnlyList<StatementSyntax> statements, bool function)
        {
            foreach (var statement in statements)
            {
                if (postponed)
                {
                    var annotations = statement switch
                    {
                        FunctionDefinitionStatementSyntax definition => definition.Parameters.Where(p => p.Annotation is not null)
                            .Select(p => p.Annotation!).Concat(definition.ReturnAnnotation is null ? [] : new[] { definition.ReturnAnnotation }),
                        AnnotatedAssignmentStatementSyntax assignment => new[] { assignment.Annotation },
                        _ => Enumerable.Empty<ExpressionSyntax>(),
                    };
                    foreach (var annotation in annotations)
                        if (ContainsYield(annotation)) errors.Add(new LythonDiagnostic("LA1100",
                            "Yield expression cannot be used within a postponed annotation.", LythonDiagnosticSeverity.Error, annotation.Span));
                }
                foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                    Expression(expression, function, statement is AssignmentStatementSyntax or ChainedAssignmentStatementSyntax or
                        AnnotatedAssignmentStatementSyntax or UnpackingAssignmentStatementSyntax or AugmentedAssignmentStatementSyntax or ExpressionStatementSyntax or MemberAssignmentStatementSyntax or SubscriptAssignmentStatementSyntax or SliceAssignmentStatementSyntax);
                foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                    Statements(body, statement switch
                    {
                        FunctionDefinitionStatementSyntax => true,
                        ClassDefinitionStatementSyntax => false,
                        _ => function,
                    });
            }
        }
        void Expression(ExpressionSyntax expression, bool function, bool bare)
        {
            if (expression is YieldExpressionSyntax && (!function || !bare))
                errors.Add(new LythonDiagnostic("LA1100", !function ? "'yield' outside function or inside comprehension." :
                    "Yield expression requires parentheses here.", LythonDiagnosticSeverity.Error, expression.Span));
            if (expression is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is not null) Expression(parameter.DefaultValue, function, false);
                Expression(lambda.Body, true, false);
                return;
            }
            var clauses = expression switch
            {
                ListComprehensionExpressionSyntax item => item.Clauses,
                SetComprehensionExpressionSyntax item => item.Clauses,
                DictComprehensionExpressionSyntax item => item.Clauses,
                GeneratorExpressionSyntax item => item.Clauses,
                _ => null,
            };
            foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
                Expression(child, clauses is null ? function : function && ReferenceEquals(child, clauses[0].Iterable),
                    expression is ParenthesizedExpressionSyntax);
        }
    }
}
