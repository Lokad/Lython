namespace Lokad.Lython.Frontend;

// Python's context-sensitive syntax rules apply even in dead branches and
// uncalled bodies. Keep them independent of abstract-value/runtime analysis.
internal static class SyntaxValidityDiagnostics
{
    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script)
    {
        var diagnostics = new List<LythonDiagnostic>();
        var statements = new Stack<(StatementSyntax Statement, int LoopDepth)>();
        foreach (var statement in script.Statements) statements.Push((statement, 0));
        while (statements.TryPop(out var work))
        {
            var (statement, loopDepth) = work;
            if (statement is BreakStatementSyntax or ContinueStatementSyntax && loopDepth == 0)
                Error("Loop control requires an enclosing loop in the same scope.", statement.Span);
            if (statement is FunctionDefinitionStatementSyntax function)
                Parameters(function.Parameters, function.Span);
            if (statement is MatchStatementSyntax match)
                foreach (var matchCase in match.Cases) PatternNames(matchCase.Pattern);
            if (statement is ClassDefinitionStatementSyntax type)
                Unique(type.KeywordArguments.Select(a => a.Name), type.Span, "Repeated class keyword argument.");
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement)) Expressions(expression);
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                var depth = statement switch
                {
                    FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax => 0,
                    ForStatementSyntax loop when ReferenceEquals(body, loop.Body) => loopDepth + 1,
                    WhileStatementSyntax loop when ReferenceEquals(body, loop.Body) => loopDepth + 1,
                    _ => loopDepth
                };
                foreach (var child in body) statements.Push((child, depth));
            }
        }
        return diagnostics;

        void Error(string message, LythonSourceSpan span)
            => diagnostics.Add(new LythonDiagnostic("LA1100", message, LythonDiagnosticSeverity.Error, span));

        void Unique(IEnumerable<string> names, LythonSourceSpan span, string message)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names) if (!seen.Add(name)) Error(message, span);
        }

        void Parameters(IReadOnlyList<FunctionParameterSyntax> parameters, LythonSourceSpan span)
            => Unique(parameters.Select(p => p.Name), span, "Duplicate function parameter.");

        void Expressions(ExpressionSyntax root)
        {
            var pending = new Stack<(ExpressionSyntax Expression, HashSet<string> Targets, bool Iterable)>();
            pending.Push((root, new HashSet<string>(StringComparer.Ordinal), false));
            while (pending.TryPop(out var item))
            {
                var (expression, targets, iterable) = item;
                if (expression is AssignmentExpressionSyntax assignment)
                {
                    if (iterable) Error("Assignment expressions are forbidden in comprehension iterables.", assignment.Span);
                    else if (targets.Contains(assignment.Name)) Error("Assignment expression rebinds a comprehension iteration target.", assignment.Span);
                }
                if (expression is CallExpressionSyntax call)
                    Unique(call.Arguments.Where(a => a.Kind == CallArgumentKind.Keyword).Select(a => a.KeywordName), call.Span, "Repeated keyword argument.");
                if (expression is LambdaExpressionSyntax lambda)
                {
                    Parameters(lambda.Parameters, lambda.Span);
                    foreach (var parameter in lambda.Parameters)
                        if (parameter.DefaultValue is not null) pending.Push((parameter.DefaultValue, targets, iterable));
                    pending.Push((lambda.Body, new HashSet<string>(StringComparer.Ordinal), iterable));
                    continue;
                }
                var clauses = expression switch
                {
                    ListComprehensionExpressionSyntax c => c.Clauses,
                    SetComprehensionExpressionSyntax c => c.Clauses,
                    DictComprehensionExpressionSyntax c => c.Clauses,
                    GeneratorExpressionSyntax c => c.Clauses,
                    _ => null
                };
                if (clauses is not null)
                {
                    var nestedTargets = new HashSet<string>(targets, StringComparer.Ordinal);
                    foreach (var clause in clauses) AddTargetNames(clause.Target, nestedTargets);
                    foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
                        pending.Push((child, nestedTargets, iterable || clauses.Any(c => ReferenceEquals(c.Iterable, child))));
                }
                else
                    foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression)) pending.Push((child, targets, iterable));
            }
        }

        HashSet<string> PatternNames(PatternSyntax pattern)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            void Add(string? name)
            {
                if (name is not null && !names.Add(name)) Error("Pattern captures a name more than once.", pattern.Span);
            }
            void Merge(PatternSyntax child)
            {
                foreach (var name in PatternNames(child)) Add(name);
            }
            switch (pattern)
            {
                case MatchCapturePatternSyntax capture: Add(capture.Name); break;
                case MatchStarPatternSyntax star: Add(star.Name); break;
                case MatchAsPatternSyntax alias: Merge(alias.Pattern); Add(alias.Name); break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var child in sequence.Items) Merge(child);
                    break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var child in mapping.Items) Merge(child.Pattern);
                    Add(mapping.RestName);
                    break;
                case MatchClassPatternSyntax type:
                    Unique(type.KeywordPatterns.Select(p => p.Name), type.Span, "Repeated class pattern keyword.");
                    foreach (var child in type.PositionalPatterns) Merge(child);
                    foreach (var child in type.KeywordPatterns) Merge(child.Pattern);
                    break;
                case MatchOrPatternSyntax alternatives:
                    HashSet<string>? first = null;
                    foreach (var child in alternatives.Patterns)
                    {
                        var captures = PatternNames(child);
                        if (first is null) first = captures;
                        else if (!first.SetEquals(captures)) Error("OR-pattern alternatives must capture the same names.", child.Span);
                    }
                    if (first is not null) names.UnionWith(first);
                    break;
            }
            return names;
        }
    }

    private static void AddTargetNames(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name: names.Add(name.Name); break;
            case LoopStarredTargetSyntax star: names.Add(star.Name); break;
            case LoopTupleTargetSyntax tuple:
                foreach (var child in tuple.Items) AddTargetNames(child, names);
                break;
        }
    }
}
