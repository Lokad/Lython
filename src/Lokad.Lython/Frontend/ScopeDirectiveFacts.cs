namespace Lokad.Lython.Frontend;

internal sealed record ScopeDirectiveFacts(
    IReadOnlySet<string> GlobalNames,
    IReadOnlySet<string> NonlocalNames,
    IReadOnlyCollection<string> LocalNames,
    string[] LocalBindingOrder)
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

    // Function declarations already know their initial executable coordinates.
    // A builder extending this map must first take an independent copy.
    internal Dictionary<string, int>? PreparedLocalSlots { get; init; }

    // Scope validation visits child suites and records their directive effects.
    public bool ContainsDirectivesInBody { get; set; }

    // The enclosing scope's binding walk also visits comprehension reads.
    // A proven empty result lets later passes skip the same subtree.
    internal bool HasNoOutwardAssignments { get; set; }

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
    {
        // Module and class suites have dynamic namespaces. Their executable
        // slots follow actual emission, so only membership survives this walk.
        var localNames = new HashSet<string>(StringComparer.Ordinal);
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        var nonlocalNames = new HashSet<string>(StringComparer.Ordinal);
        var containsAnnotations = CollectScopeBindings(body, localNames, globalNames, nonlocalNames,
            scheduleLexicalWork, cancellationToken);
        localNames.ExceptWith(globalNames);
        localNames.ExceptWith(nonlocalNames);
        return new ScopeDirectiveFacts(globalNames, nonlocalNames, localNames, [])
        {
            ContainsVariableAnnotations = containsAnnotations
        };
    }

    private static ScopeDirectiveFacts CollectFunctionBindings(FunctionParameterLayout parameters,
        IReadOnlyList<StatementSyntax> body, Func<StatementSyntax, bool>? scheduleLexicalWork,
        CancellationToken cancellationToken)
    {
        var globalNames = new HashSet<string>(StringComparer.Ordinal);
        var nonlocalNames = new HashSet<string>(StringComparer.Ordinal);

        var bindings = new List<string>(parameters.LayoutParameterNames);
        var containsAnnotations = CollectScopeBindings(body, bindings, globalNames, nonlocalNames, scheduleLexicalWork,
            cancellationToken);
        return CompactLocalBindings(bindings, globalNames, nonlocalNames,
            parameters.LayoutParameterIndex, parameters.LayoutParameterNames.Count, cancellationToken, containsAnnotations);
    }

    public static ScopeDirectiveFacts ForClass(ClassDefinitionStatementSyntax definition) => definition.PreparedScopeFacts.RequireNotNull();

    internal static ScopeDirectiveFacts PrepareClass(ClassDefinitionStatementSyntax definition, Func<StatementSyntax, bool> scheduleLexicalWork,
        CancellationToken cancellationToken)
        => definition.PreparedScopeFacts = ForSuite(definition.Body, scheduleLexicalWork, cancellationToken);

    public static ScopeDirectiveFacts ForFunction(FunctionDefinitionStatementSyntax definition) => definition.PreparedScopeFacts.RequireNotNull();

    internal static ScopeDirectiveFacts PrepareFunction(FunctionDefinitionStatementSyntax definition, Func<StatementSyntax, bool> scheduleLexicalWork,
        CancellationToken cancellationToken)
        => definition.PreparedScopeFacts = CollectFunctionBindings(definition.ParameterLayout,
            definition.Body, scheduleLexicalWork, cancellationToken);

    public static IReadOnlyCollection<string> CollectLambdaLocalNames(LambdaExpressionSyntax lambda)
    {
        if (lambda.PreparedScopeFacts is { } prepared) return prepared.LocalNames;
        var localNames = new HashSet<string>(
            lambda.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        CollectExpressionBindings(lambda.Body, localNames);
        return localNames;
    }

    private static ScopeDirectiveFacts CollectLambdaBindings(FunctionParameterLayout parameters, ExpressionSyntax body,
        CancellationToken cancellationToken)
    {
        var bindings = new List<string>(parameters.LayoutParameterNames);
        CollectExpressionBindings(body, bindings);
        return CompactLocalBindings(bindings, EmptyNames, EmptyNames, null, 0, cancellationToken);
    }

    public static ScopeDirectiveFacts ForLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.PreparedScopeFacts.RequireNotNull();
    }

    internal static ScopeDirectiveFacts PrepareLambda(LambdaExpressionSyntax lambda, FunctionParameterLayout parameters,
        CancellationToken cancellationToken)
        => lambda.PreparedScopeFacts = CollectLambdaBindings(parameters, lambda.Body, cancellationToken);

    private static readonly IReadOnlySet<string> EmptyNames = new HashSet<string>(StringComparer.Ordinal);

    private static ScopeDirectiveFacts CompactLocalBindings(List<string> bindings,
        IReadOnlySet<string> globalNames, IReadOnlySet<string> nonlocalNames,
        IReadOnlyDictionary<string, int>? parameterSlots, int parameterCount, CancellationToken cancellationToken,
        bool containsAnnotations = false)
    {
        var writesParameter = false;
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        var written = 0;
        for (var scan = 0; scan < bindings.Count; scan++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, scan);
            var name = bindings[scan];
            if (globalNames.Contains(name) || nonlocalNames.Contains(name)) continue;
            if (!slots.TryAdd(name, written))
            {
                if (!writesParameter && scan >= parameterCount && parameterSlots?.ContainsKey(name) == true)
                    writesParameter = true;
                continue;
            }
            bindings[written++] = name;
        }
        // Preserve the binder's parameter prefix and the first occurrence of
        // each body name. Scope membership uses the same map's key collection.
        bindings.RemoveRange(written, bindings.Count - written);
        return new ScopeDirectiveFacts(globalNames, nonlocalNames, slots.Keys, bindings.ToArray())
        {
            PreparedLocalSlots = slots,
            WritesParameter = writesParameter,
            ContainsVariableAnnotations = containsAnnotations
        };
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
                            if (matchCase.PreparedBindings is { } bindings)
                                foreach (var entry in bindings) names.Add(entry.Value);
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

    internal static void CollectExpressionBindings(ExpressionSyntax expression, ICollection<string> names)
        => CollectExpressionBindings(expression, names, static (collection, name) => collection.Add(name));

    internal static bool CollectExpressionBindings<TState>(ExpressionSyntax expression, TState state, Action<TState, string> add)
    {
        var found = false;
        if (expression is LambdaExpressionSyntax lambda)
        {
            foreach (var parameter in lambda.Parameters)
                if (parameter.DefaultValue is not null) found |= CollectExpressionBindings(parameter.DefaultValue, state, add);
            return found;
        }

        var comprehension = (expression as ComprehensionExpressionSyntax)?.PreparedScopeFacts;
        if (comprehension is { HasNoOutwardAssignments: true }) return false;
        if (expression is AssignmentExpressionSyntax assignment)
        {
            add(state, assignment.Name);
            found = true;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            found |= CollectExpressionBindings(child, state, add);
        }
        if (!found && comprehension is not null) comprehension.HasNoOutwardAssignments = true;
        return found;
    }
}
