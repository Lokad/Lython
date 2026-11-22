namespace Lokad.Lython.Frontend;

internal readonly record struct StatementFacts(List<CallExpressionSyntax>? PotentialMutations, bool NeedsLexicalExpressions);

// Python's context-sensitive syntax rules apply even in dead branches and
// uncalled bodies. Keep them independent of abstract-value/runtime analysis.
internal static class SyntaxValidityDiagnostics
{
    private struct SuiteVisit(IReadOnlyList<StatementSyntax> statements, int loopDepth, int parent,
        bool lexicalScope, bool annotatedReturn)
    {
        public readonly IReadOnlyList<StatementSyntax> Statements = statements;
        public readonly int LoopDepth = loopDepth;
        public readonly int Parent = parent;
        public readonly bool LexicalScope = lexicalScope;
        public readonly bool AnnotatedReturn = annotatedReturn;
        public int NextStatement = statements.Count - 1;
        public int LastAnnotationCheck = -1;
    }

    private readonly record struct ExpressionVisit(ExpressionSyntax Expression, HashSet<string>? Targets,
        bool Iterable, bool InLambda = false, bool RecordCall = false);

    private static int ReadFuturePrefix(IReadOnlyList<StatementSyntax> statements, CancellationToken cancellationToken,
        out bool postponed)
    {
        var prefixEnd = 0;
        postponed = false;
        for (var i = 0; i < statements.Count; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            var statement = statements[i];
            if (i == 0 && statement is ExpressionStatementSyntax leading)
            {
                var expression = leading.Expression;
                while (expression is ParenthesizedExpressionSyntax grouped) expression = grouped.Inner;
                if (expression is StringLiteralExpressionSyntax)
                {
                    prefixEnd = 1;
                    continue;
                }
            }
            if (statement is ImportStatementSyntax { ModuleName: "__future__", ImportedMembers: { } members })
            {
                if (i == prefixEnd) prefixEnd++;
                for (var j = 0; !postponed && j < members.Count; j++)
                {
                    PythonTokenization.CheckCancellation(cancellationToken, j);
                    postponed = members[j].Name == "annotations";
                }
            }
            // A misplaced import can still enable annotation diagnostics.
            else if (postponed) break;
        }
        return prefixEnd;
    }

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script, out bool hasScopeDirectives, out bool postponedAnnotations,
        out Dictionary<StatementSyntax, StatementFacts>? statementFacts,
        out Dictionary<IReadOnlyList<StatementSyntax>, int>? annotationChecks, out bool importsRegex,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<LythonDiagnostic>();
        var hasYield = false;
        var hasModernTypes = false;
        var futurePrefixEnd = ReadFuturePrefix(script.Statements, cancellationToken, out postponedAnnotations);
        List<LythonDiagnostic>? futureDiagnostics = null;
        Stack<ExpressionSyntax>? annotationExpressions = null;
        hasScopeDirectives = false;
        var suites = new List<SuiteVisit> { new(script.Statements, 0, -1, lexicalScope: true, annotatedReturn: false) };
        var expressions = new Stack<ExpressionVisit>();
        statementFacts = null;
        annotationChecks = null;
        importsRegex = false;
        HashSet<string>? seenNames = null;
        while (suites.Count != 0)
        {
            var current = suites.Count - 1;
            var work = suites[current];
            if (work.NextStatement < 0)
            {
                suites.RemoveAt(current);
                if (importsRegex || work.LastAnnotationCheck < 0) continue;
                if (work.LexicalScope)
                {
                    var checks = annotationChecks ??= new(ReferenceEqualityComparer.Instance);
                    checks[work.Statements] = Math.Max(checks.GetValueOrDefault(work.Statements, -1), work.LastAnnotationCheck);
                }
                if (work.Parent >= 0)
                {
                    // Any child check makes its containing statement relevant
                    // in the parent's independent annotation flow.
                    var parent = suites[work.Parent];
                    parent.LastAnnotationCheck = Math.Max(parent.LastAnnotationCheck, parent.NextStatement + 1);
                    suites[work.Parent] = parent;
                }
                continue;
            }

            var statementIndex = work.NextStatement--;
            var statement = work.Statements[statementIndex];
            PythonTokenization.CheckCancellation(cancellationToken, statement.Span.Start);
            hasScopeDirectives |= statement is ScopeDirectiveStatementSyntax;
            if (statement is BreakStatementSyntax or ContinueStatementSyntax && work.LoopDepth == 0)
                Error("Loop control requires an enclosing loop in the same scope.", statement.Span);
            var checksAnnotation = statement is AnnotatedAssignmentStatementSyntax { Expression: not null }
                || work.AnnotatedReturn && statement is ReturnStatementSyntax;
            if (statement is FunctionDefinitionStatementSyntax function)
            {
                Parameters(function.Parameters, function.Span);
                hasModernTypes |= function.TypeParameters is not null;
                checksAnnotation = function.Parameters.Any(static parameter =>
                    parameter.Annotation is not null && parameter.DefaultValue is not null);
            }
            // Pattern reads are separate from direct expressions.
            var needsLexicalExpressions = statement is MatchStatementSyntax;
            if (statement is MatchStatementSyntax match)
            {
                foreach (var matchCase in match.Cases) PatternNames(matchCase.Pattern);
            }
            if (statement is ClassDefinitionStatementSyntax type)
            {
                Unique(type.HeaderArguments.Where(a => a.Kind == CallArgumentKind.Keyword).Select(a => a.KeywordName),
                    type.Span, "Repeated class keyword argument.");
                hasModernTypes |= type.TypeParameters is not null;
            }
            hasModernTypes |= statement is TypeAliasStatementSyntax;
            if (statement is ImportStatementSyntax import)
            {
                if (import is { ModuleName: "__future__", ImportedMembers: not null } &&
                    (work.Parent >= 0 || statementIndex >= futurePrefixEnd))
                    FutureError("Future imports must occur at the beginning of the module.", statement.Span);
                importsRegex |= import.ModuleName == "re" || import.ModuleName.StartsWith("re.", StringComparison.Ordinal);
            }
            if (postponedAnnotations)
            {
                if (statement is AnnotatedAssignmentStatementSyntax assignment) ValidateAnnotation(assignment.Annotation);
                else if (statement is FunctionDefinitionStatementSyntax definition)
                {
                    foreach (var parameter in definition.Parameters)
                        if (parameter.Annotation is { } annotation) ValidateAnnotation(annotation);
                    if (definition.ReturnAnnotation is { } returns) ValidateAnnotation(returns);
                }
            }
            if (checksAnnotation) work.LastAnnotationCheck = Math.Max(work.LastAnnotationCheck, statementIndex);
            suites[current] = work;
            List<CallExpressionSyntax>? calls = null;
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                Expressions(expression, ref calls, ref needsLexicalExpressions);
            if (calls is not null || needsLexicalExpressions)
                (statementFacts ??= new(ReferenceEqualityComparer.Instance))[statement] = new(calls, needsLexicalExpressions);
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                var depth = statement switch
                {
                    FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax => 0,
                    ForStatementSyntax loop when ReferenceEquals(body, loop.Body) => work.LoopDepth + 1,
                    WhileStatementSyntax loop when ReferenceEquals(body, loop.Body) => work.LoopDepth + 1,
                    _ => work.LoopDepth
                };
                var annotatedReturn = statement switch
                {
                    FunctionDefinitionStatementSyntax child => child.ReturnAnnotation is not null,
                    ClassDefinitionStatementSyntax => false,
                    _ => work.AnnotatedReturn
                };
                // Push child suites in source order and read from the end,
                // preserving the validator's existing diagnostic order.
                suites.Add(new SuiteVisit(body, depth, current,
                    statement is FunctionDefinitionStatementSyntax or ClassDefinitionStatementSyntax, annotatedReturn));
            }
        }
        // These validators can report only on the syntax discovered above/below.
        // Keep their independent scope rules and diagnostic ordering when needed,
        // without repeating complete walks over ordinary scripts.
        if (hasYield) diagnostics.AddRange(GeneratorSyntaxFacts.Prepare(script, cancellationToken));
        if (hasModernTypes) diagnostics.AddRange(ModernTypeSyntaxFacts.Validate(script));
        // Preserve future diagnostics after the independent syntax families.
        if (futureDiagnostics is not null) diagnostics.AddRange(futureDiagnostics);
        return diagnostics;

        void Error(string message, LythonSourceSpan span)
            => diagnostics.Add(new LythonDiagnostic("LA1100", message, LythonDiagnosticSeverity.Error, span));

        void FutureError(string message, LythonSourceSpan span)
            => (futureDiagnostics ??= []).Add(new("LA1100", message, LythonDiagnosticSeverity.Error, span));

        void ValidateAnnotation(ExpressionSyntax annotation)
        {
            var pending = annotationExpressions ??= new();
            pending.Push(annotation);
            for (var visited = 0; pending.TryPop(out var expression); visited++)
            {
                PythonTokenization.CheckCancellation(cancellationToken, visited);
                if (expression is AssignmentExpressionSyntax)
                    FutureError("Assignment expressions cannot be used within postponed annotations.", expression.Span);
                // A lambda's body is its own scope. Its defaults belong here.
                if (expression is LambdaExpressionSyntax lambda)
                {
                    foreach (var parameter in lambda.Parameters)
                        if (parameter.DefaultValue is { } value) pending.Push(value);
                }
                else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression)) pending.Push(child);
            }
        }

        void Unique(IEnumerable<string> names, LythonSourceSpan span, string message)
        {
            using var entries = names.GetEnumerator();
            if (!entries.MoveNext()) return;
            var first = entries.Current;
            if (!entries.MoveNext()) return;
            var seen = seenNames ??= new HashSet<string>(StringComparer.Ordinal);
            seen.Add(first);
            do
            {
                if (!seen.Add(entries.Current)) Error(message, span);
            } while (entries.MoveNext());
            seen.Clear();
        }

        void Parameters(IReadOnlyList<FunctionParameterSyntax> parameters, LythonSourceSpan span)
            => Unique(parameters.Select(p => p.Name), span, "Duplicate function parameter.");

        void Expressions(ExpressionSyntax root, ref List<CallExpressionSyntax>? calls, ref bool needsLexicalExpressions)
        {
            var first = calls?.Count ?? 0;
            expressions.Push(new(root, null, false));
            while (expressions.TryPop(out var item))
            {
                var (expression, targets, iterable, inLambda, recordCall) = item;
                if (recordCall)
                {
                    (calls ??= []).Add((CallExpressionSyntax)expression);
                    continue;
                }
                PythonTokenization.CheckCancellation(cancellationToken, expression.Span.Start);
                needsLexicalExpressions |= expression is IdentifierExpressionSyntax { Name: "super" or "__class__" };
                hasYield |= expression is YieldExpressionSyntax;
                if (expression is AssignmentExpressionSyntax assignment)
                {
                    needsLexicalExpressions = true;
                    if (iterable) Error("Assignment expressions are forbidden in comprehension iterables.", assignment.Span);
                    else if (targets?.Contains(assignment.Name) == true) Error("Assignment expression rebinds a comprehension iteration target.", assignment.Span);
                }
                if (expression is CallExpressionSyntax call)
                {
                    Unique(call.Arguments.Where(a => a.Kind == CallArgumentKind.Keyword).Select(a => a.KeywordName), call.Span, "Repeated keyword argument.");
                    // The mutation pass stops at lambdas. Record the call after
                    // descendants so reversing this segment restores its original
                    // preorder without changing validation's diagnostic order.
                    if (!inLambda && call.Target is MemberExpressionSyntax { Target: IdentifierExpressionSyntax })
                        expressions.Push(item with { RecordCall = true });
                }
                if (expression is LambdaExpressionSyntax lambda)
                {
                    needsLexicalExpressions = true;
                    Parameters(lambda.Parameters, lambda.Span);
                    foreach (var parameter in lambda.Parameters)
                        if (parameter.DefaultValue is not null) expressions.Push(new(parameter.DefaultValue, targets, iterable, InLambda: true));
                    expressions.Push(new(lambda.Body, null, iterable, InLambda: true));
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
                    needsLexicalExpressions = true;
                    var nestedTargets = targets is null ? new HashSet<string>(StringComparer.Ordinal)
                        : new HashSet<string>(targets, StringComparer.Ordinal);
                    foreach (var clause in clauses) AddTargetNames(clause.Target, nestedTargets);
                    foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
                        expressions.Push(new(child, nestedTargets,
                            iterable || clauses.Any(c => ReferenceEquals(c.Iterable, child)), inLambda));
                }
                else
                    foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
                        expressions.Push(new(child, targets, iterable, inLambda));
            }
            if (calls is not null) calls.Reverse(first, calls.Count - first);
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
                case MatchCapturePatternSyntax capture: Add(capture.OriginalBindingName ?? capture.Name); break;
                case MatchStarPatternSyntax star: Add(star.OriginalBindingName ?? star.Name); break;
                case MatchAsPatternSyntax alias: Merge(alias.Pattern); Add(alias.OriginalBindingName ?? alias.Name); break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var child in sequence.Items) Merge(child);
                    break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var child in mapping.Items) Merge(child.Pattern);
                    Add(mapping.OriginalBindingName ?? mapping.RestName);
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
            case LoopStoreTargetSyntax store:
                foreach (var binding in AssignmentTargetFacts.Names(store.Target)) names.Add(binding);
                break;
            case LoopNameTargetSyntax name: names.Add(name.Name); break;
            case LoopStarredTargetSyntax star: names.Add(star.Name); break;
            case LoopTupleTargetSyntax tuple:
                foreach (var child in tuple.Items) AddTargetNames(child, names);
                break;
        }
    }
}
