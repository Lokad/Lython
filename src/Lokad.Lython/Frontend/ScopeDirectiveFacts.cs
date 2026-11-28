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
        var (globalNames, nonlocalNames, containsAnnotations) = CollectScopeBindings(body, localNames,
            static (names, name) => names.Add(name), scheduleLexicalWork, cancellationToken);
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
        var bindings = new LocalBindings(parameters, trackParameterWrites: true);
        var (globalNames, nonlocalNames, containsAnnotations) = CollectScopeBindings(body, bindings,
            static (slots, name) => slots.Add(name), scheduleLexicalWork, cancellationToken);
        return bindings.Complete(globalNames, nonlocalNames, cancellationToken, containsAnnotations);
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
        var bindings = new LocalBindings(parameters, trackParameterWrites: false);
        CollectExpressionBindings(body, bindings, static (slots, name) => slots.Add(name));
        return bindings.Complete(EmptyNames, EmptyNames, cancellationToken, containsAnnotations: false);
    }

    public static ScopeDirectiveFacts ForLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.PreparedScopeFacts.RequireNotNull();
    }

    internal static ScopeDirectiveFacts PrepareLambda(LambdaExpressionSyntax lambda, FunctionParameterLayout parameters,
        CancellationToken cancellationToken)
        => lambda.PreparedScopeFacts = CollectLambdaBindings(parameters, lambda.Body, cancellationToken);

    private static readonly IReadOnlySet<string> EmptyNames = new HashSet<string>(StringComparer.Ordinal);

    private sealed class LocalBindings(FunctionParameterLayout parameters, bool trackParameterWrites)
    {
        private Dictionary<string, int>? _slots;
        private bool _writesParameter;

        internal void Add(string name)
        {
            if (_slots is null)
            {
                // The parameter prefix already has its final slot numbers.
                if (parameters.LayoutParameterIndex.ContainsKey(name))
                {
                    _writesParameter = trackParameterWrites;
                    return;
                }
                _slots = new(parameters.LayoutParameterIndex, StringComparer.Ordinal);
            }
            if (!_slots.TryAdd(name, _slots.Count) && trackParameterWrites && !_writesParameter)
                _writesParameter = parameters.LayoutParameterIndex.ContainsKey(name);
        }

        internal ScopeDirectiveFacts Complete(IReadOnlySet<string> globalNames, IReadOnlySet<string> nonlocalNames,
            CancellationToken cancellationToken, bool containsAnnotations)
        {
            PythonTokenization.CheckCancellation(cancellationToken, 0);
            var slots = _slots ?? parameters.LayoutParameterIndex;
            var names = parameters.LayoutParameterNames;
            if (_slots is not null)
            {
                names = new string[slots.Count];
                var checkedSlots = 0;
                foreach (var (name, slot) in slots)
                {
                    PythonTokenization.CheckCancellation(cancellationToken, checkedSlots++);
                    names[slot] = name;
                }
            }

            if (globalNames.Count != 0 || nonlocalNames.Count != 0)
            {
                // Later directives can exclude a name already seen in the body.
                // In valid scopes these directives never exclude a parameter.
                var retained = 0;
                for (var slot = 0; slot < names.Length; slot++)
                {
                    PythonTokenization.CheckCancellation(cancellationToken, slot);
                    var name = names[slot];
                    if (globalNames.Contains(name) || nonlocalNames.Contains(name))
                    {
                        if (_slots is null)
                        {
                            slots = _slots = new(slots, StringComparer.Ordinal);
                            names = (string[])names.Clone();
                        }
                        slots.Remove(name);
                        continue;
                    }
                    if (retained != slot)
                    {
                        names[retained] = name;
                        slots[name] = retained;
                    }
                    retained++;
                }
                if (retained != names.Length) Array.Resize(ref names, retained);
            }

            return new ScopeDirectiveFacts(globalNames, nonlocalNames, slots.Keys, names)
            {
                PreparedLocalSlots = slots,
                WritesParameter = _writesParameter,
                ContainsVariableAnnotations = containsAnnotations
            };
        }
    }

    private static (IReadOnlySet<string> Globals, IReadOnlySet<string> Nonlocals, bool ContainsAnnotations)
        CollectScopeBindings<T>(IReadOnlyList<StatementSyntax> statements, T names, Action<T, string> add,
        Func<StatementSyntax, bool>? scheduleLexicalWork,
        CancellationToken cancellationToken)
    {
        HashSet<string>? globalNames = null, nonlocalNames = null;
        var containsAnnotations = false;
        CollectSuite(statements);
        return (globalNames ?? EmptyNames, nonlocalNames ?? EmptyNames, containsAnnotations);

        void CollectSuite(IReadOnlyList<StatementSyntax> suite)
        {
            foreach (var statement in suite)
            {
                PythonTokenization.CheckCancellation(cancellationToken, statement.Span.Start);
                var collectExpressionBindings = scheduleLexicalWork?.Invoke(statement) ?? true;
                switch (statement)
                {
                    case ScopeDirectiveStatementSyntax directive:
                        var directives = directive.Kind == ScopeDirectiveKind.Global
                            ? globalNames ??= new(StringComparer.Ordinal)
                            : nonlocalNames ??= new(StringComparer.Ordinal);
                        directives.UnionWith(directive.Names);
                        break;
                    case ImportStatementSyntax importStatement:
                        foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                        {
                            add(names, memberName);
                        }
                        break;

                    case TypeAliasStatementSyntax alias:
                        add(names, alias.Name);
                        break;
                    case AssignmentStatementSyntax assignment:
                        add(names, assignment.Name);
                        break;

                    case ChainedAssignmentStatementSyntax chained:
                        foreach (var target in chained.Targets) CollectAssignmentTargetBindings(target);
                        break;

                    case AnnotatedAssignmentStatementSyntax annotated:
                        containsAnnotations = true;
                        if (annotated.Target is NameAssignmentTargetSyntax name)
                        {
                            add(names, name.Name);
                        }

                        break;

                    case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget }:
                        add(names, nameTarget.Name);
                        break;

                    case UnpackingAssignmentStatementSyntax unpacking:
                        foreach (var target in unpacking.Targets) CollectAssignmentTargetBindings(target);
                        break;

                    case ForStatementSyntax forStatement:
                        CollectLoopTargetBindings(forStatement.Target, names, add);
                        break;

                    case WithStatementSyntax withStatement:
                        CollectDirectBindings(withStatement, collectExpressionBindings);
                        foreach (var boundName in withStatement.BoundNames) add(names, boundName);
                        CollectSuite(withStatement.Body);
                        continue;

                    case TryStatementSyntax tryStatement:
                        CollectSuite(tryStatement.TryBody);
                        foreach (var exceptClause in tryStatement.ExceptClauses)
                        {
                            if (collectExpressionBindings && exceptClause.ExceptionTypeExpression is not null)
                                CollectExpressionBindings(exceptClause.ExceptionTypeExpression, names, add);
                            if (exceptClause.ExceptionVariableName is not null) add(names, exceptClause.ExceptionVariableName);
                            CollectSuite(exceptClause.Body);
                        }
                        if (tryStatement.ElseBody is not null)
                            CollectSuite(tryStatement.ElseBody);
                        if (tryStatement.FinallyBody is not null)
                            CollectSuite(tryStatement.FinallyBody);
                        continue;

                    case MatchStatementSyntax matchStatement:
                        CollectExpressionBindings(matchStatement.Subject, names, add);
                        foreach (var matchCase in matchStatement.Cases)
                        {
                            if (matchCase.PreparedBindings is { } bindings)
                                foreach (var entry in bindings) add(names, entry.Value);
                            if (matchCase.Guard is not null) CollectExpressionBindings(matchCase.Guard, names, add);
                            CollectSuite(matchCase.Body);
                        }
                        continue;

                    case DeleteStatementSyntax deletion:
                        StatementSyntaxTraversal.VisitDeletionTargets(deletion.Target, (names, add), static (bindings, target) =>
                        {
                            if (target is IdentifierExpressionSyntax name) bindings.add(bindings.names, name.Name);
                        });
                        break;

                    case FunctionDefinitionStatementSyntax functionDefinition:
                        add(names, functionDefinition.Name);
                        CollectDirectBindings(functionDefinition, collectExpressionBindings);
                        continue;

                    case ClassDefinitionStatementSyntax classDefinition:
                        add(names, classDefinition.Name);
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
                CollectExpressionBindings(expression, names, add);
        }

        void CollectAssignmentTargetBindings(AssignmentTargetSyntax target)
        {
            switch (target)
            {
                case NameAssignmentTargetSyntax name:
                    add(names, name.Name);
                    break;
                case UnpackingAssignmentTargetGroupSyntax unpacking:
                    foreach (var nestedTarget in unpacking.Targets) CollectAssignmentTargetBindings(nestedTarget);
                    break;
            }
        }
    }

    private static void CollectLoopTargetBindings<T>(LoopTargetSyntax target, T names, Action<T, string> add)
    {
        switch (target)
        {
            case LoopStoreTargetSyntax store:
                foreach (var binding in AssignmentTargetFacts.Names(store.Target)) add(names, binding);
                break;
            case LoopNameTargetSyntax name:
                add(names, name.Name);
                break;
            case LoopStarredTargetSyntax starred:
                add(names, starred.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectLoopTargetBindings(item, names, add);
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
