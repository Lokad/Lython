namespace Lokad.Lython.Frontend;

internal sealed record ScopeDirectiveFacts(
    IReadOnlySet<string> GlobalNames,
    IReadOnlySet<string> NonlocalNames,
    IReadOnlySet<string> LocalNames,
    IReadOnlyList<string> LocalBindingOrder)
{
    // A free __class__ cell is required only by lexical reads in this function
    // or an enclosed scope. A neighboring method does not supply that demand.
    public bool RequiresClassCell { get; set; }

    // A class's own implicit cell is distinct from an enclosing cell captured
    // by its suite. Prepare this lexical requirement before execution too.
    public bool CreatesClassCell { get; set; }

    internal static ScopeDirectiveFacts ClassCellScope { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(["__class__"], StringComparer.Ordinal), ["__class__"]);

    // Lexical setup includes dead branches and excludes nested definition bodies.
    public bool ContainsVariableAnnotations { get; init; }

    // Filled by scope validation, which already resolves every nonlocal target.
    // Such cells can change through callbacks; local flow cannot prove them unbound.
    public HashSet<string>? NestedNonlocalNames { get; set; }

    internal bool WritesParameter { get; init; }

    // Scope validation visits child suites and records their directive effects.
    public bool ContainsDirectivesInBody { get; set; }

    public static ScopeDirectiveFacts Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal), []);

    public bool IsGlobal(string name) => GlobalNames.Contains(name);

    public bool IsNonlocal(string name) => NonlocalNames.Contains(name);
}

internal static class ScopeDirectiveFactsCollector
{
    public static ScopeDirectiveFacts ForModule(ScriptSyntax script)
        => script.PreparedScopeFacts ??= ForSuite(script.Statements);

    internal static ScopeDirectiveFacts ForSuite(IReadOnlyList<StatementSyntax> body,
        Func<StatementSyntax, bool>? scheduleLexicalWork = null, CancellationToken cancellationToken = default)
        => CollectFunctionBindings([], body, scheduleLexicalWork, cancellationToken);

    private static ScopeDirectiveFacts CollectFunctionBindings(IEnumerable<string> parameterNames,
        IReadOnlyList<StatementSyntax> body, Func<StatementSyntax, bool>? scheduleLexicalWork,
        CancellationToken cancellationToken, IReadOnlyDictionary<string, int>? parameterSlots = null)
    {
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        var nonlocalNames = new HashSet<string>(StringComparer.Ordinal);

        var bindings = new List<string>();
        foreach (var name in parameterNames)
        {
            bindings.Add(name);
        }

        var parameterCount = bindings.Count;
        var containsAnnotations = CollectScopeBindings(body, bindings, globalNames, nonlocalNames, scheduleLexicalWork,
            cancellationToken);
        var localNames = CompactLocalBindings(bindings, globalNames, nonlocalNames,
            parameterSlots, parameterCount, out var writesParameter);

        return new ScopeDirectiveFacts(globalNames, nonlocalNames, localNames, bindings.ToArray())
        {
            ContainsVariableAnnotations = containsAnnotations,
            WritesParameter = writesParameter
        };
    }

    public static ScopeDirectiveFacts ForClass(ClassDefinitionStatementSyntax definition) => definition.PreparedScopeFacts.RequireNotNull();

    internal static ScopeDirectiveFacts PrepareClass(ClassDefinitionStatementSyntax definition, Func<StatementSyntax, bool> scheduleLexicalWork,
        CancellationToken cancellationToken)
        => definition.PreparedScopeFacts = ForSuite(definition.Body, scheduleLexicalWork, cancellationToken);

    public static ScopeDirectiveFacts ForFunction(FunctionDefinitionStatementSyntax definition) => definition.PreparedScopeFacts.RequireNotNull();

    internal static ScopeDirectiveFacts PrepareFunction(FunctionDefinitionStatementSyntax definition, Func<StatementSyntax, bool> scheduleLexicalWork,
        CancellationToken cancellationToken)
        => definition.PreparedScopeFacts = CollectFunctionBindings(definition.Parameters.Select(p => p.Name),
            definition.Body, scheduleLexicalWork, cancellationToken, definition.ParameterLayout.LayoutParameterIndex);

    public static IReadOnlySet<string> CollectLambdaLocalNames(LambdaExpressionSyntax lambda)
    {
        if (lambda.PreparedScopeFacts is { } prepared) return prepared.LocalNames;
        var localNames = new HashSet<string>(
            lambda.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        CollectExpressionBindings(lambda.Body, localNames);
        return localNames;
    }

    private static ScopeDirectiveFacts CollectLambdaBindings(IEnumerable<string> parameterNames, ExpressionSyntax body)
    {
        var bindings = parameterNames.ToList();
        CollectExpressionBindings(body, bindings);
        var locals = CompactLocalBindings(bindings, EmptyNames, EmptyNames, null, 0, out _);
        return new ScopeDirectiveFacts(EmptyNames, EmptyNames, locals, bindings.ToArray());
    }

    public static ScopeDirectiveFacts ForLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.PreparedScopeFacts.RequireNotNull();
    }

    internal static ScopeDirectiveFacts PrepareLambda(LambdaExpressionSyntax lambda)
        => lambda.PreparedScopeFacts = CollectLambdaBindings(lambda.Parameters.Select(parameter => parameter.Name), lambda.Body);

    private static readonly IReadOnlySet<string> EmptyNames = new HashSet<string>(StringComparer.Ordinal);

    private static HashSet<string> CompactLocalBindings(List<string> bindings,
        IReadOnlySet<string> globalNames, IReadOnlySet<string> nonlocalNames,
        IReadOnlyDictionary<string, int>? parameterSlots, int parameterCount, out bool writesParameter)
    {
        writesParameter = false;
        var locals = new HashSet<string>(StringComparer.Ordinal);
        var written = 0;
        for (var scan = 0; scan < bindings.Count; scan++)
        {
            var name = bindings[scan];
            if (globalNames.Contains(name) || nonlocalNames.Contains(name)) continue;
            if (!locals.Add(name))
            {
                if (!writesParameter && scan >= parameterCount && parameterSlots?.ContainsKey(name) == true)
                    writesParameter = true;
                continue;
            }
            bindings[written++] = name;
        }
        // Compaction preserves the first source occurrence. The one set is
        // also the membership data used by the prepared scope.
        bindings.RemoveRange(written, bindings.Count - written);
        return locals;
    }

    private static bool CollectScopeBindings(IReadOnlyList<StatementSyntax> statements, ICollection<string> names,
        HashSet<string> globalNames, HashSet<string> nonlocalNames, Func<StatementSyntax, bool>? scheduleLexicalWork,
        CancellationToken cancellationToken)
    {
        var containsAnnotations = false;
        CollectSuite(statements);
        return containsAnnotations;

        void CollectSuite(IReadOnlyList<StatementSyntax> suite)
        {
            foreach (var statement in suite)
            {
                PythonTokenization.CheckCancellation(cancellationToken, statement.Span.Start);
                var collectExpressionBindings = scheduleLexicalWork?.Invoke(statement) ?? true;
                switch (statement)
                {
                    case ScopeDirectiveStatementSyntax directive:
                        (directive.Kind == ScopeDirectiveKind.Global ? globalNames : nonlocalNames).UnionWith(directive.Names);
                        break;
                    case ImportStatementSyntax importStatement:
                        foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                        {
                            names.Add(memberName);
                        }
                        break;

                    case TypeAliasStatementSyntax alias:
                        names.Add(alias.Name);
                        break;
                    case AssignmentStatementSyntax assignment:
                        names.Add(assignment.Name);
                        break;

                    case ChainedAssignmentStatementSyntax chained:
                        foreach (var target in chained.Targets) CollectAssignmentTargetBindings(target, names);
                        break;

                    case AnnotatedAssignmentStatementSyntax annotated:
                        containsAnnotations = true;
                        if (annotated.Target is NameAssignmentTargetSyntax name)
                        {
                            names.Add(name.Name);
                        }

                        break;

                    case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                        names.Add(nameTarget.Name);
                        break;

                    case UnpackingAssignmentStatementSyntax unpacking:
                        foreach (var target in unpacking.Targets) CollectUnpackingTargetNames(target, names);
                        break;

                    case ForStatementSyntax forStatement:
                        CollectLoopTargetBindings(forStatement.Target, names);
                        break;

                    case WithStatementSyntax withStatement:
                        CollectDirectBindings(withStatement, collectExpressionBindings);
                        foreach (var boundName in withStatement.BoundNames) names.Add(boundName);
                        CollectSuite(withStatement.Body);
                        continue;

                    case TryStatementSyntax tryStatement:
                        CollectSuite(tryStatement.TryBody);
                        foreach (var exceptClause in tryStatement.ExceptClauses)
                        {
                            if (collectExpressionBindings && exceptClause.ExceptionTypeExpression is not null)
                                CollectExpressionBindings(exceptClause.ExceptionTypeExpression, names);
                            if (exceptClause.ExceptionVariableName is not null) names.Add(exceptClause.ExceptionVariableName);
                            CollectSuite(exceptClause.Body);
                        }
                        if (tryStatement.ElseBody is not null)
                            CollectSuite(tryStatement.ElseBody);
                        if (tryStatement.FinallyBody is not null)
                            CollectSuite(tryStatement.FinallyBody);
                        continue;

                    case MatchStatementSyntax matchStatement:
                        CollectExpressionBindings(matchStatement.Subject, names);
                        foreach (var matchCase in matchStatement.Cases)
                        {
                            CollectPatternBindings(matchCase.Pattern, names);
                            if (matchCase.Guard is not null) CollectExpressionBindings(matchCase.Guard, names);
                            CollectSuite(matchCase.Body);
                        }
                        continue;

                    case DeleteStatementSyntax deletion:
                        StatementSyntaxTraversal.VisitDeletionTargets(deletion.Target, names, static (bindings, target) =>
                        {
                            if (target is IdentifierExpressionSyntax name) bindings.Add(name.Name);
                        });
                        break;

                    case FunctionDefinitionStatementSyntax functionDefinition:
                        names.Add(functionDefinition.Name);
                        CollectDirectBindings(functionDefinition, collectExpressionBindings);
                        continue;

                    case ClassDefinitionStatementSyntax classDefinition:
                        names.Add(classDefinition.Name);
                        CollectDirectBindings(classDefinition, collectExpressionBindings);
                        continue;
                }

                CollectDirectBindings(statement, collectExpressionBindings);

                foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                {
                    // Keep collecting bindings after finding an annotation.
                    CollectSuite(body);
                }
            }
        }

        void CollectDirectBindings(StatementSyntax statement, bool collectExpressionBindings)
        {
            if (!collectExpressionBindings) return;
            foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
                CollectExpressionBindings(expression, names);
        }

        static void CollectAssignmentTargetBindings(AssignmentTargetSyntax target, ICollection<string> names)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax name:
                    names.Add(name.Name);
                    break;
                case UnpackingAssignmentTargetGroupSyntax unpacking:
                    foreach (var nestedTarget in unpacking.Targets) CollectUnpackingTargetNames(nestedTarget, names);
                    break;
            }
        }

        static void CollectUnpackingTargetNames(UnpackingTargetSyntax target, ICollection<string> names)
        {
            if (target is UnpackingNameTargetSyntax name)
            {
                names.Add(name.Name);
                return;
            }

            if (target is UnpackingNestedTargetSyntax nested)
            {
                foreach (var nestedItem in nested.Items)
                {
                    CollectUnpackingTargetNames(nestedItem, names);
                }
            }
        }
    }

    private static void CollectLoopTargetBindings(LoopTargetSyntax target, ICollection<string> names)
    {
        switch (target)
        {
            case LoopStoreTargetSyntax store:
                foreach (var binding in AssignmentTargetFacts.Names(store.Target)) names.Add(binding);
                break;
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopStarredTargetSyntax starred:
                names.Add(starred.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectLoopTargetBindings(item, names);
                break;
        }
    }

    internal static void CollectPatternBindings(PatternSyntax pattern, ICollection<string> names)
    {
        switch (pattern)
        {
            case MatchCapturePatternSyntax capture:
                names.Add(capture.Name);
                break;
            case MatchSequencePatternSyntax sequence:
                foreach (var item in sequence.Items) CollectPatternBindings(item, names);
                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items) CollectPatternBindings(item.Pattern, names);
                if (mapping.RestName is not null) names.Add(mapping.RestName);
                break;
            case MatchClassPatternSyntax classPattern:
                foreach (var item in classPattern.PositionalPatterns) CollectPatternBindings(item, names);
                foreach (var item in classPattern.KeywordPatterns) CollectPatternBindings(item.Pattern, names);
                break;
            case MatchStarPatternSyntax star when star.Name is not null:
                names.Add(star.Name);
                break;
            case MatchAsPatternSyntax asPattern:
                CollectPatternBindings(asPattern.Pattern, names);
                names.Add(asPattern.Name);
                break;
            case MatchOrPatternSyntax orPattern:
                foreach (var item in orPattern.Patterns) CollectPatternBindings(item, names);
                break;
        }
    }

    internal static void CollectExpressionBindings(ExpressionSyntax expression, ICollection<string> names)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            foreach (var parameter in lambda.Parameters)
                if (parameter.DefaultValue is not null) CollectExpressionBindings(parameter.DefaultValue, names);
            return;
        }

        if (expression is AssignmentExpressionSyntax assignment)
        {
            names.Add(assignment.Name);
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            CollectExpressionBindings(child, names);
        }
    }
}
