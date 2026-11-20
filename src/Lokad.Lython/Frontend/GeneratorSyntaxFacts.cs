namespace Lokad.Lython.Frontend;

internal static class GeneratorSyntaxFacts
{
    public static bool ContainsYield(ExpressionSyntax expression)
        => expression is LambdaExpressionSyntax lambda
            ? lambda.Parameters.Any(parameter => parameter.DefaultValue is not null && ContainsYield(parameter.DefaultValue))
            : expression is YieldExpressionSyntax || ExpressionSyntaxTraversal.EnumerateChildren(expression).Any(ContainsYield);

    public static IEnumerable<LythonDiagnostic> Prepare(ScriptSyntax script, CancellationToken cancellationToken)
    {
        var errors = new List<LythonDiagnostic>();
        var postponed = script.Statements.OfType<ImportStatementSyntax>().Any(i =>
            i.ModuleName == "__future__" && i.ImportedMembers?.Any(m => m.Name == "annotations") == true);
        Statements(script.Statements, null);
        return errors;

        void Statements(IReadOnlyList<StatementSyntax> statements, FunctionDefinitionStatementSyntax? function)
        {
            foreach (var statement in statements)
            {
                PythonTokenization.CheckCancellation(cancellationToken, statement.Span.Start);
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
                        FunctionDefinitionStatementSyntax definition => definition,
                        ClassDefinitionStatementSyntax => null,
                        _ => function,
                    });
            }
        }
        void Expression(ExpressionSyntax expression, object? callable, bool bare)
        {
            PythonTokenization.CheckCancellation(cancellationToken, expression.Span.Start);
            if (expression is YieldExpressionSyntax)
            {
                if (callable is null || !bare)
                    errors.Add(new LythonDiagnostic("LA1100", callable is null ? "'yield' outside function or inside comprehension." :
                        "Yield expression requires parentheses here.", LythonDiagnosticSeverity.Error, expression.Span));
                else if (callable is FunctionDefinitionStatementSyntax function)
                    function.IsGenerator = true;
            }
            if (expression is LambdaExpressionSyntax lambda)
            {
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is not null) Expression(parameter.DefaultValue, callable, false);
                Expression(lambda.Body, lambda, false);
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
                Expression(child, clauses is null || ReferenceEquals(child, clauses[0].Iterable) ? callable : null,
                    expression is ParenthesizedExpressionSyntax or FormattedStringExpressionSyntax);
        }
    }
}
