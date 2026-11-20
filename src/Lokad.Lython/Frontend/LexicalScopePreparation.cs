using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// Prepare lexical facts once, including Python's direct super().member argument
// preloads. These depend on the complete lexical tree, not runtime name values.
internal static class LexicalScopePreparation
{
    private sealed record Scope(Scope? Parent, ScopeDirectiveFacts Facts,
        bool Function = false, bool Class = false, bool Positional = false, bool Annotation = false,
        string? DeclarationName = null, bool UseLocalsQualifier = false, bool Inlined = false)
    {
        public bool IsModule => !Function && !Class && !Annotation && (Parent?.IsModule ?? true);

        public string? QualifyDeclaration(string name, Runtime.MemoryGovernor.TemporaryMemoryReservation? workspace, LythonSourceSpan span,
            CancellationToken cancellationToken, bool genericParameters = false)
        {
            Scope? scope = this;
            while (scope is { DeclarationName: null }) scope = scope.Parent;
            if (scope is null && !genericParameters) return null;

            const string genericPrefix = "<generic parameters of ";
            var enclosing = scope?.DeclarationName ?? string.Empty;
            var separator = scope is null ? string.Empty : scope.UseLocalsQualifier ? ".<locals>." : ".";
            var length = checked(enclosing.Length + separator.Length + name.Length + (genericParameters ? genericPrefix.Length + 1 : 0));
            workspace?.Grow(48L + 2L * length, span);
            PythonTokenization.CheckCancellation(cancellationToken, span.Start);
            return genericParameters
                ? string.Concat((ReadOnlySpan<string>)[enclosing, separator, genericPrefix, name, ">"])
                : string.Concat(enclosing, separator, name);
        }

        private bool ShadowsClass => Facts.IsGlobal("__class__") || Facts.LocalNames.Contains("__class__");

        public bool CanPreload => Function && Positional && !ShadowsClass &&
            HasEnclosingClassBinding() && SuperIsImplicitGlobal();

        public bool HasEnclosingClassBinding()
        {
            for (var scope = Parent; scope is not null; scope = scope.Parent)
            {
                if (scope.Class) return true;
                if (scope.Facts.IsGlobal("__class__")) return false;
                if (scope.Facts.LocalNames.Contains("__class__")) return true;
            }
            return false;
        }

        public void RequireClassCell(bool implicitUse = false)
        {
            // An inlined comprehension can shadow the name without adding a
            // frame. A direct super read in a class suite has no receiver.
            if (implicitUse && !Function && !Annotation) return;
            if (Facts.RequiresClassCell || ShadowsClass || !HasEnclosingClassBinding()) return;

            // Capture into each intervening callable. A nested class suite can
            // read an enclosing cell, while its methods request its own cell.
            for (var scope = this; scope.Parent is { } enclosing; scope = enclosing)
            {
                if (scope.Class || scope.Function && !scope.Inlined)
                    scope.Facts.RequiresClassCell = true;
                if (enclosing.Class)
                {
                    enclosing.Facts.CreatesClassCell = true;
                    return;
                }
                if (enclosing.Facts.LocalNames.Contains("__class__")) return;
            }
        }

        private bool SuperIsImplicitGlobal()
        {
            for (var scope = this; scope is not null; scope = scope.Parent)
            {
                if (scope.Class) continue;
                if (scope.Facts.IsGlobal("super") || scope.Facts.IsNonlocal("super") ||
                    scope.Facts.LocalNames.Contains("super")) return false;
            }
            return true;
        }

        public ScopeDirectiveFacts BindGeneratorAssignments(ExpressionSyntax expression, ScopeDirectiveFacts facts)
        {
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            ScopeDirectiveFactsCollector.CollectExpressionBindings(expression, assigned);
            if (assigned.Count == 0) return facts;
            var globals = new HashSet<string>(StringComparer.Ordinal);
            var nonlocals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in assigned)
            {
                var nonlocal = false;
                for (var scope = this; scope is not null; scope = scope.Parent)
                {
                    if (scope.Class) continue;
                    if (scope.Facts.IsGlobal(name)) break;
                    if (scope.Facts.IsNonlocal(name) || scope.Function && scope.Facts.LocalNames.Contains(name))
                    {
                        nonlocal = true;
                        break;
                    }
                }
                (nonlocal ? nonlocals : globals).Add(name);
            }
            return facts with { GlobalNames = globals, NonlocalNames = nonlocals };
        }
    }

    public static void Prepare(ScriptSyntax script, string source, Runtime.MemoryGovernor? governor = null,
        CancellationToken cancellationToken = default)
    {
        var postponed = script.Statements.OfType<ImportStatementSyntax>().Any(import =>
            import.ModuleName == "__future__" && import.ImportedMembers?.Any(member => member.Name == "annotations") == true);
        // A module owns bindings, not an enclosing class-cell capture.
        var moduleFacts = ScopeDirectiveFactsCollector.ForSuite(script.Statements);
        var moduleUsesSuper = moduleFacts.LocalNames.Contains("super") || moduleFacts.IsGlobal("super");
        List<CallExpressionSyntax>? preloads = null;
        List<ExpressionSyntax>? callables = null;
        var requiresAnnotationChecks = false;
        var importsRegex = false;
        // Module bindings do not supply lexical class cells to child functions.
        var root = new Scope(null, ScopeDirectiveFacts.Empty);
        using var qualifiedNameWorkspace = governor?.ReserveTemporary(0, null);
        var pending = new Stack<(object Syntax, Scope Scope)>();
        PushStatements(script.Statements, root);
        while (pending.Count != 0)
        {
            var (syntax, scope) = pending.Pop();
            if (syntax is StatementSyntax statement)
            {
                governor?.ThrowIfAdmissionFailed();
                PythonTokenization.CheckCancellation(cancellationToken, statement.Span.Start);
                if (statement is AnnotatedAssignmentStatementSyntax annotated)
                {
                    requiresAnnotationChecks |= annotated.Expression is not null;
                    if (postponed) PostponedAnnotationText.Attach(annotated.Annotation, source, governor, cancellationToken);
                }
                importsRegex |= statement is ImportStatementSyntax import &&
                    (import.ModuleName == "re" || import.ModuleName.StartsWith("re.", StringComparison.Ordinal));
                // Even a later declaration in a sibling function affects the
                // module symbol table. Decide candidate tags after this walk.
                if (statement is ScopeDirectiveStatementSyntax { Kind: ScopeDirectiveKind.Global } directive &&
                    directive.Names.Contains("super", StringComparer.Ordinal)) moduleUsesSuper = true;
                // Lazy type expressions are deliberately outside the ordinary
                // statement-expression walk, but their callable code is eager.
                if (statement is TypeAliasStatementSyntax alias)
                {
                    var qualified = scope.Facts.IsGlobal(alias.Name) ? null
                        : scope.QualifyDeclaration(alias.DeclaredName, qualifiedNameWorkspace, alias.Span, cancellationToken);
                    var aliasScope = new Scope(TypeParameterScope(scope, alias.TypeParameters),
                        ScopeDirectiveFacts.Empty, Annotation: true);
                    pending.Push((alias.Value, aliasScope with
                    {
                        DeclarationName = qualified ?? alias.DeclaredName,
                        UseLocalsQualifier = true
                    }));
                    PushTypeParameters(alias.TypeParameters, alias.DeclaredName, alias.Span, scope, aliasScope);
                    continue;
                }
                PushStatementReads(statement, scope);
                if (statement is FunctionDefinitionStatementSyntax function)
                {
                    requiresAnnotationChecks |= function.ReturnAnnotation is not null ||
                        function.Parameters.Any(static parameter => parameter.Annotation is not null && parameter.DefaultValue is not null);
                    if (postponed)
                    {
                        foreach (var parameter in function.Parameters)
                            if (parameter.Annotation is { } annotation)
                                PostponedAnnotationText.Attach(annotation, source, governor, cancellationToken);
                        if (function.ReturnAnnotation is { } returns)
                            PostponedAnnotationText.Attach(returns, source, governor, cancellationToken);
                    }
                    var enclosing = TypeParameterScope(scope, function.TypeParameters);
                    if (!ReferenceEquals(enclosing, scope))
                        function.PreparedTypeParameterNames = enclosing.Facts.LocalNames;
                    PushTypeParameters(function.TypeParameters, function.DeclaredName, function.Span, scope, enclosing);
                    var facts = ScopeDirectiveFactsCollector.PrepareFunction(function);
                    var parameters = function.PrepareParameterLayout(cancellationToken);
                    var qualified = scope.Facts.IsGlobal(function.Name) ? null
                        : scope.QualifyDeclaration(function.DeclaredName, qualifiedNameWorkspace, function.Span, cancellationToken);
                    function.PreparedQualifiedName = PrepareQualifiedName(qualified, function.Span);
                    var functionScope = new Scope(enclosing, facts,
                        Function: true, Positional: parameters.PositionalCount != 0,
                        DeclarationName: qualified ?? function.DeclaredName, UseLocalsQualifier: true);
                    if (facts.IsNonlocal("__class__")) functionScope.RequireClassCell();
                    PushStatements(function.Body, functionScope);
                }
                else if (statement is ClassDefinitionStatementSyntax definition)
                {
                    var enclosing = TypeParameterScope(scope, definition.TypeParameters);
                    PushTypeParameters(definition.TypeParameters, definition.DeclaredName, definition.Span, scope, enclosing);
                    var qualified = scope.Facts.IsGlobal(definition.Name) ? null
                        : scope.QualifyDeclaration(definition.DeclaredName, qualifiedNameWorkspace, definition.Span, cancellationToken);
                    var facts = ScopeDirectiveFactsCollector.PrepareClass(definition);
                    var classScope = new Scope(enclosing, facts, Class: true,
                        DeclarationName: qualified ?? definition.DeclaredName);
                    if (facts.IsNonlocal("__class__")) classScope.RequireClassCell();
                    PushStatements(definition.Body, classScope);
                }
                else foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement)) PushStatements(body, scope);
                continue;
            }
            var node = (ExpressionSyntax)syntax;
            if (node is IdentifierExpressionSyntax identifier)
            {
                if (identifier.Name == "__class__") scope.RequireClassCell();
                else if (identifier.Name == "super")
                {
                    if (scope.IsModule) moduleUsesSuper = true;
                    else scope.RequireClassCell(implicitUse: true);
                }
            }
            if (node is MemberExpressionSyntax member && member.MemberName != "__class__" &&
                !moduleUsesSuper && scope.CanPreload &&
                Unwrap(member.Target) is CallExpressionSyntax { Arguments.Count: 0 } call &&
                Unwrap(call.Target) is IdentifierExpressionSyntax { Name: "super" })
                (preloads ??= []).Add(call);
            if (node is LambdaExpressionSyntax lambda)
            {
                (callables ??= []).Add(lambda);
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is { } value) pending.Push((value, scope));
                var facts = ScopeDirectiveFactsCollector.PrepareLambda(lambda);
                var parameters = lambda.PrepareParameterLayout(cancellationToken);
                var qualified = scope.QualifyDeclaration("<lambda>", qualifiedNameWorkspace, lambda.Span, cancellationToken);
                lambda.PreparedQualifiedName = PrepareQualifiedName(qualified, lambda.Span);
                var lambdaScope = new Scope(scope, facts, Function: true,
                    Positional: parameters.PositionalCount != 0,
                    DeclarationName: qualified ?? "<lambda>", UseLocalsQualifier: true);
                pending.Push((lambda.Body, lambdaScope));
            }
            else if (ComprehensionSyntaxFacts.TryGetParts(node, out var items, out var clauses, out var generator))
            {
                pending.Push((clauses[0].Iterable, scope));
                var facts = ComprehensionSyntaxFacts.Bindings(clauses.Select(clause => clause.Target), generator);
                if (!generator && scope.IsModule && facts.LocalNames.Contains("super")) moduleUsesSuper = true;
                if (generator)
                {
                    (callables ??= []).Add(node);
                    facts = scope.BindGeneratorAssignments(node, facts);
                }
                var generatorName = generator
                    ? scope.QualifyDeclaration("<genexpr>", qualifiedNameWorkspace, node.Span, cancellationToken) ?? "<genexpr>" : null;
                var bodyScope = new Scope(scope, facts, Function: generator || scope.Function,
                    Positional: generator || scope.Positional, DeclarationName: generatorName, Inlined: !generator);
                node.PreparedComprehensionScopeFacts = facts;
                foreach (var item in items) pending.Push((item, bodyScope));
                for (var i = 0; i < clauses.Count; i++)
                {
                    var clause = clauses[i];
                    if (i != 0) pending.Push((clause.Iterable, bodyScope));
                    if (clause.Condition is { } condition) pending.Push((condition, bodyScope));
                    foreach (var read in AssignmentTargetFacts.Reads(clause.Target)) pending.Push((read, bodyScope));
                }
            }
            else foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(node)) pending.Push((child, scope));
        }

        if (!moduleUsesSuper && preloads is not null)
            foreach (var call in preloads) call.PreloadImplicitSuperArguments = true;

        script.PreparedScopeFacts = moduleFacts;
        script.PreparedCallables = callables ?? [];
        script.RequiresAnnotationChecks = requiresAnnotationChecks;
        script.ImportsRegex = importsRegex;

        void PushStatements(IReadOnlyList<StatementSyntax> statements, Scope scope)
        {
            foreach (var statement in statements) pending.Push((statement, scope));
        }

        void PushStatementReads(StatementSyntax statement, Scope scope)
        {
            if (statement is DeleteStatementSyntax deleted)
            {
                // A bare deleted name is a binding, not a lexical read.
                PushDeletedTargetReads(deleted.Target, scope);
                return;
            }
            if (postponed && statement is FunctionDefinitionStatementSyntax function)
            {
                foreach (var decorator in function.Decorators) pending.Push((decorator, scope));
                foreach (var parameter in function.Parameters)
                    if (parameter.DefaultValue is { } value) pending.Push((value, scope));
                return;
            }
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                if (!(postponed && statement is AnnotatedAssignmentStatementSyntax annotation &&
                    ReferenceEquals(expression, annotation.Annotation))) pending.Push((expression, scope));
            if (statement is MatchStatementSyntax match)
                foreach (var item in match.Cases) PushPatternReads(item.Pattern, scope);
        }

        void PushDeletedTargetReads(ExpressionSyntax target, Scope scope)
        {
            target = Unwrap(target);
            if (target is IdentifierExpressionSyntax) return;
            if (target is TupleLiteralExpressionSyntax tuple)
            {
                foreach (var item in tuple.Items) PushDeletedTargetReads(item.Expression, scope);
            }
            else if (target is ListLiteralExpressionSyntax list)
            {
                foreach (var item in list.Items) PushDeletedTargetReads(item.Expression, scope);
            }
            else foreach (var read in ExpressionSyntaxTraversal.EnumerateChildren(target)) pending.Push((read, scope));
        }

        void PushPatternReads(PatternSyntax pattern, Scope scope)
        {
            PythonTokenization.CheckCancellation(cancellationToken, pattern.Span.Start);
            switch (pattern)
            {
                case MatchValuePatternSyntax value:
                    pending.Push((value.Expression, scope));
                    break;
                case MatchMappingPatternSyntax mapping:
                    foreach (var item in mapping.Items)
                    {
                        pending.Push((item.Key, scope));
                        PushPatternReads(item.Pattern, scope);
                    }
                    break;
                case MatchClassPatternSyntax type:
                    pending.Push((type.ClassExpression, scope));
                    foreach (var item in type.PositionalPatterns) PushPatternReads(item, scope);
                    foreach (var item in type.KeywordPatterns) PushPatternReads(item.Pattern, scope);
                    break;
                case MatchSequencePatternSyntax sequence:
                    foreach (var item in sequence.Items) PushPatternReads(item, scope);
                    break;
                case MatchOrPatternSyntax alternatives:
                    foreach (var item in alternatives.Patterns) PushPatternReads(item, scope);
                    break;
                case MatchAsPatternSyntax alias:
                    PushPatternReads(alias.Pattern, scope);
                    break;
            }
        }

        PyString? PrepareQualifiedName(string? qualified, LythonSourceSpan span)
        {
            if (qualified is null) return null;
            // UTF-8 uses at most three bytes per UTF-16 code unit. The
            // frontend workspace retains the intermediate until this walk ends.
            var bytes = PyString.EstimateApproximateBytes(0) + 3L * qualified.Length;
            governor?.Reserve(bytes, span);
            try
            {
                PythonTokenization.CheckCancellation(cancellationToken, span.Start);
                var name = PyString.FromString(qualified);
                governor?.Commit(bytes);
                return name;
            }
            catch
            {
                governor?.ReleaseReserved(bytes);
                throw;
            }
        }

        void PushTypeParameters(IReadOnlyList<TypeParameterSyntax>? parameters, string declarationName, LythonSourceSpan span,
            Scope declarationScope, Scope parameterScope)
        {
            if (parameters is null) return;
            Scope? expressionScope = null;
            foreach (var parameter in parameters)
            {
                if (parameter.Bound is null && parameter.Default is null) continue;
                expressionScope ??= parameterScope with
                {
                    DeclarationName = declarationScope.QualifyDeclaration(declarationName, qualifiedNameWorkspace, span, cancellationToken,
                        genericParameters: true)
                };
                if (parameter.Bound is { } bound) pending.Push((bound, expressionScope));
                if (parameter.Default is { } value) pending.Push((value, expressionScope));
            }
        }
    }

    private static Scope TypeParameterScope(Scope parent, IReadOnlyList<TypeParameterSyntax>? parameters)
    {
        if (parameters is not { Count: > 0 }) return parent;
        var names = parameters.Select(p => p.Name).ToArray();
        return new Scope(parent, new ScopeDirectiveFacts(ScopeDirectiveFacts.Empty.GlobalNames,
            ScopeDirectiveFacts.Empty.NonlocalNames, new HashSet<string>(names, StringComparer.Ordinal), names), Annotation: true);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses) expression = parentheses.Inner;
        return expression;
    }

}
