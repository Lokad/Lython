namespace Lokad.Lython.Frontend;

internal static class StaticNameBindingDiagnostics
{
    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeNestedFunctionDefinitions(context.Script.Statements, context);
    }

    private static void AnalyzeNestedFunctionDefinitions(IReadOnlyList<StatementSyntax> statements, StaticAnalysisContext context)
    {
        foreach (var statement in statements)
        {
            if (statement is FunctionDefinitionStatementSyntax functionDefinition)
            {
                AnalyzeFunctionHeaderLambdas(functionDefinition, context);
                AnalyzeFunctionBody(functionDefinition, context);
                continue;
            }

            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
            {
                AnalyzeNestedFunctionDefinitions(body, context);
            }
        }
    }

    private static void AnalyzeFunctionHeaderLambdas(FunctionDefinitionStatementSyntax functionDefinition, StaticAnalysisContext context)
    {
        foreach (var parameter in functionDefinition.Parameters)
        {
            if (parameter.Annotation is not null)
            {
                AnalyzeExpressionForNestedFunctions(parameter.Annotation, context);
            }

            if (parameter.DefaultValue is not null)
            {
                AnalyzeExpressionForNestedFunctions(parameter.DefaultValue, context);
            }
        }

        foreach (var decorator in functionDefinition.Decorators)
        {
            AnalyzeExpressionForNestedFunctions(decorator, context);
        }
    }

    private static void AnalyzeFunctionBody(FunctionDefinitionStatementSyntax functionDefinition, StaticAnalysisContext context)
    {
        var scopeFacts = ScopeDirectiveFactsCollector.ForFunction(functionDefinition);
        var maybeAssigned = new AssignmentFacts(scopeFacts.PreparedLocalSlots.RequireNotNull(),
            scopeFacts.NestedNonlocalNames, functionDefinition.Parameters, context);
        AnalyzeStatements(functionDefinition.Body, context, maybeAssigned);
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        AssignmentFacts maybeAssigned)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, context, maybeAssigned);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        StaticAnalysisContext context,
        AssignmentFacts maybeAssigned)
    {
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
        {
            if (statement is TryStatementSyntax or DeleteStatementSyntax) continue;
            // Alias values and type-parameter bounds/defaults run lazily in
            // their annotation scope, after the declaration has bound its name.
            if (statement is TypeAliasStatementSyntax) continue;
            if (statement is ChainedAssignmentStatementSyntax chain && !ReferenceEquals(expression, chain.Expression)) continue;
            if (statement is UnpackingAssignmentStatementSyntax unpack && !ReferenceEquals(expression, unpack.Expression)) continue;
            if (statement is ForStatementSyntax loop && !ReferenceEquals(expression, loop.Iterable)) continue;
            if (statement is WithStatementSyntax manager && !ReferenceEquals(expression, manager.ContextExpression)) continue;
            if (statement is MatchStatementSyntax matchGuardOwner &&
                IsMatchCaseGuard(matchGuardOwner, expression))
            {
                // Case guards run after their own pattern binds, so they are
                // analyzed per case below with the pattern names in scope.
                continue;
            }

            if (statement is AnnotatedAssignmentStatementSyntax localAnnotation &&
                ReferenceEquals(expression, localAnnotation.Annotation)) continue;
            AnalyzeExpression(expression, context, maybeAssigned);
        }

        switch (statement)
        {
            case TypeAliasStatementSyntax alias:
                maybeAssigned.Add(alias.Name);
                break;

            case ImportStatementSyntax importStatement:
                foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement))
                {
                    maybeAssigned.Add(memberName);
                }
                break;

            case AssignmentStatementSyntax assignment:
                maybeAssigned.Add(assignment.Name);
                break;

            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets)
                {
                    AnalyzeLoopStores(AssignmentTargetFacts.ToLoop(target), context, maybeAssigned);
                }
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                if (annotated.Expression is not null && annotated.Target is NameAssignmentTargetSyntax name)
                {
                    maybeAssigned.Add(name.Name);
                }
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                if (augmented.Target is NameAssignmentTargetSyntax augmentedName)
                {
                    AnalyzeLocalRead(augmentedName.Name, augmentedName.Span, context, maybeAssigned);
                    maybeAssigned.Add(augmentedName.Name);
                }
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets)
                    AnalyzeLoopStores(AssignmentTargetFacts.ToLoop(AssignmentTargetFacts.FromUnpacking(target)), context, maybeAssigned);
                break;

            case WithStatementSyntax withStatement:
                if (withStatement.Target is not null)
                    AnalyzeLoopStores(AssignmentTargetFacts.ToLoop(withStatement.Target), context, maybeAssigned);
                AnalyzeStatements(withStatement.Body, context, maybeAssigned);
                break;

            case IfStatementSyntax ifStatement:
                {
                    // Analyze both alternatives from the original bindings.
                    // The join keeps a name assigned on either path.
                    var saved = maybeAssigned.SavedCount;
                    var branch = maybeAssigned.BeginBranch();
                    AnalyzeStatements(ifStatement.ThenStatements, context, maybeAssigned);
                    maybeAssigned.SaveAndRewind(branch);
                    AnalyzeOptionalBranch(ifStatement.ElseStatements, context, maybeAssigned);
                    maybeAssigned.ApplySaved(saved);
                    break;
                }

            case ForStatementSyntax forStatement:
                {
                    var branch = maybeAssigned.BeginBranch();
                    AnalyzeLoopStores(forStatement.Target, context, maybeAssigned);
                    AnalyzeStatements(forStatement.Body, context, maybeAssigned);
                    maybeAssigned.Merge(branch);
                    AnalyzeOptionalBranch(forStatement.ElseStatements, context, maybeAssigned);
                    break;
                }

            case WhileStatementSyntax whileStatement:
                AnalyzeOptionalBranch(whileStatement.Body, context, maybeAssigned);
                AnalyzeOptionalBranch(whileStatement.ElseStatements, context, maybeAssigned);
                break;

            case MatchStatementSyntax matchStatement:
                {
                    var savedBodies = maybeAssigned.SavedCount;
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        var branch = maybeAssigned.BeginBranch();
                        if (matchCase.PreparedBindings is { } captures)
                            foreach (var entry in captures) maybeAssigned.Add(entry.Value);
                        if (matchCase.Guard is not null)
                        {
                            AnalyzeExpression(matchCase.Guard, context, maybeAssigned);
                            // Pattern and guard only add bindings. Their effects
                            // are visible to later cases, unlike body effects.
                            maybeAssigned.Merge(branch);
                            branch = maybeAssigned.BeginBranch();
                        }

                        AnalyzeStatements(matchCase.Body, context, maybeAssigned);
                        maybeAssigned.SaveAndRewind(branch);
                    }
                    maybeAssigned.ApplySaved(savedBodies);
                    break;
                }

            case DeleteStatementSyntax deleteStatement:
                StatementSyntaxTraversal.VisitDeletionTargets(deleteStatement.Target, (context, maybeAssigned),
                    static (state, target) =>
                    {
                        if (target is not IdentifierExpressionSyntax)
                            AnalyzeExpression(target, state.context, state.maybeAssigned);
                    });
                StatementSyntaxTraversal.VisitDeletionTargets(deleteStatement.Target, maybeAssigned, static (assigned, target) =>
                {
                    if (target is IdentifierExpressionSyntax name) assigned.Remove(name.Name);
                });
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                maybeAssigned.Add(functionDefinition.Name);
                // The surrounding expression walk has already checked this header.
                AnalyzeFunctionBody(functionDefinition, context);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                maybeAssigned.Add(classDefinition.Name);
                // A class suite can initialize the enclosing function's cells.
                // Retain these possible assignments rather than rejecting a later
                // read before the class actually runs.
                foreach (var nonlocalName in ScopeDirectiveFactsCollector.ForClass(classDefinition).NonlocalNames)
                    maybeAssigned.Add(nonlocalName);
                AnalyzeNestedFunctionDefinitions(classDefinition.Body, context);
                break;

            case TryStatementSyntax tryStatement:
                {
                    AnalyzeOptionalBranch(tryStatement.TryBody, context, maybeAssigned);
                    foreach (var exceptClause in tryStatement.ExceptClauses)
                    {
                        var handlerName = exceptClause.ExceptionVariableName;
                        var hideHandler = handlerName is not null && !maybeAssigned.Contains(handlerName);
                        var branch = maybeAssigned.BeginBranch();
                        if (exceptClause.ExceptionTypeExpression is { } header)
                            AnalyzeExpression(header, context, maybeAssigned);
                        if (handlerName is not null) maybeAssigned.Add(handlerName);

                        AnalyzeStatements(exceptClause.Body, context, maybeAssigned);
                        // Only a handler variable that existed before the handler
                        // can remain a possible assignment after it exits.
                        if (hideHandler) maybeAssigned.Remove(handlerName!);
                        maybeAssigned.Merge(branch);
                    }
                    AnalyzeOptionalBranch(tryStatement.ElseBody, context, maybeAssigned);
                    if (tryStatement.FinallyBody is not null)
                    {
                        AnalyzeStatements(tryStatement.FinallyBody, context, maybeAssigned);
                    }
                    break;
                }
        }
    }

    private static void AnalyzeOptionalBranch(IReadOnlyList<StatementSyntax>? statements, StaticAnalysisContext context,
        AssignmentFacts assignments)
    {
        if (statements is null) return;
        var branch = assignments.BeginBranch();
        AnalyzeStatements(statements, context, assignments);
        assignments.Merge(branch);
    }

    private static void AnalyzeExpression(
        ExpressionSyntax expression,
        StaticAnalysisContext context,
        AssignmentFacts maybeAssigned)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                AnalyzeLocalRead(identifier.Name, identifier.Span, context, maybeAssigned);
                return;

            case ListComprehensionExpressionSyntax listComprehension:
                AnalyzeComprehension(expression, listComprehension.Clauses, listComprehension.ItemExpression, null,
                    context, maybeAssigned);
                return;

            case GeneratorExpressionSyntax generator:
                AnalyzeComprehension(expression, generator.Clauses, generator.ItemExpression, null,
                    context, maybeAssigned);
                return;

            case SetComprehensionExpressionSyntax setComprehension:
                AnalyzeComprehension(expression, setComprehension.Clauses, setComprehension.ItemExpression, null,
                    context, maybeAssigned);
                return;

            case DictComprehensionExpressionSyntax dictComprehension:
                AnalyzeComprehension(expression, dictComprehension.Clauses, dictComprehension.KeyExpression,
                    dictComprehension.ValueExpression, context, maybeAssigned);
                return;

            case AssignmentExpressionSyntax assignment:
                AnalyzeExpression(assignment.Expression, context, maybeAssigned);
                maybeAssigned.Add(assignment.Name);
                return;

            case LambdaExpressionSyntax lambda:
                foreach (var parameter in lambda.Parameters)
                    if (parameter.DefaultValue is not null) AnalyzeExpression(parameter.DefaultValue, context, maybeAssigned);
                AnalyzeExpression(lambda.Body, context, CreateLambdaFacts(lambda, context));
                return;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            AnalyzeExpression(child, context, maybeAssigned);
        }
    }

    // Clause targets stay visible within this comprehension. Only binding
    // expressions can assign in the surrounding scope.
    private static void AnalyzeComprehension(ExpressionSyntax expression, IReadOnlyList<ComprehensionClauseSyntax> clauses,
        ExpressionSyntax item, ExpressionSyntax? value, StaticAnalysisContext context,
        AssignmentFacts assignments)
    {
        var branch = assignments.BeginBranch();
        foreach (var clause in clauses)
        {
            AnalyzeExpression(clause.Iterable, context, assignments);
            AnalyzeLoopStores(clause.Target, context, assignments);
            if (clause.Condition is not null)
                AnalyzeExpression(clause.Condition, context, assignments);
        }
        AnalyzeExpression(item, context, assignments);
        if (value is not null) AnalyzeExpression(value, context, assignments);
        assignments.Rewind(branch);
        ScopeDirectiveFactsCollector.CollectExpressionBindings(expression, assignments, static (facts, name) => facts.Add(name));
    }

    private static void AnalyzeLocalRead(
        string name,
        LythonSourceSpan span,
        StaticAnalysisContext context,
        AssignmentFacts maybeAssigned)
    {
        if (maybeAssigned.IsUnassignedLocal(name))
        {
            StaticDiagnosticSink.AddError(
                context,
                "LA3146",
                $"Local variable '{name}' is read before it is assigned.",
                span);
        }
    }

    private static void AnalyzeExpressionForNestedFunctions(ExpressionSyntax expression, StaticAnalysisContext context)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            AnalyzeExpression(lambda.Body, context, CreateLambdaFacts(lambda, context));
        }
    }

    private static AssignmentFacts CreateLambdaFacts(LambdaExpressionSyntax lambda, StaticAnalysisContext context)
    {
        if (lambda.PreparedScopeFacts?.PreparedLocalSlots is { } slots)
            return new AssignmentFacts(slots, null, lambda.Parameters, context);

        // A postponed annotation can still be checked even if lexical
        // preparation skipped its expressions.
        var localSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in ScopeDirectiveFactsCollector.CollectLambdaLocalNames(lambda))
            localSlots.Add(name, localSlots.Count);
        return new AssignmentFacts(localSlots, null, lambda.Parameters, context);
    }

    private static bool IsMatchCaseGuard(MatchStatementSyntax matchStatement, ExpressionSyntax expression)
    {
        foreach (var matchCase in matchStatement.Cases)
        {
            if (matchCase.Guard is not null && ReferenceEquals(matchCase.Guard, expression))
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeLoopStores(LoopTargetSyntax target, StaticAnalysisContext context,
        AssignmentFacts assigned)
    {
        if (target is LoopTupleTargetSyntax tuple)
        {
            foreach (var child in tuple.Items) AnalyzeLoopStores(child, context, assigned);
            return;
        }
        foreach (var read in AssignmentTargetFacts.Reads(target)) AnalyzeExpression(read, context, assigned);
        AddLoopTarget(target, assigned);
    }

    private static void AddLoopTarget(LoopTargetSyntax target, AssignmentFacts maybeAssigned)
    {
        switch (target)
        {
            case LoopStoreTargetSyntax store:
                foreach (var binding in AssignmentTargetFacts.Names(store.Target)) maybeAssigned.Add(binding);
                break;
            case LoopNameTargetSyntax nameTarget:
                maybeAssigned.Add(nameTarget.Name);
                break;
            case LoopStarredTargetSyntax starredTarget:
                maybeAssigned.Add(starredTarget.Name);
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items) AddLoopTarget(item, maybeAssigned);
                break;
        }
    }

    // Assignment changes use the lexical scope's existing slot order.
    // Slots outside this function's bindings cannot contribute a diagnostic.
    private sealed class AssignmentFacts
    {
        private readonly Dictionary<string, int> _slots;
        private readonly HashSet<string>? _nestedNonlocals;
        private readonly StaticAnalysisContext _context;
        private readonly bool[] _assigned;
        private readonly record struct Change(int Slot, bool Added);
        private List<Change>? _changes;
        private List<int>? _saved;
        private int _branchDepth;

        internal AssignmentFacts(Dictionary<string, int> slots, HashSet<string>? nestedNonlocals,
            IReadOnlyList<FunctionParameterSyntax> parameters, StaticAnalysisContext context)
        {
            _slots = slots;
            _nestedNonlocals = nestedNonlocals;
            _context = context;
            _assigned = slots.Count == 0 ? [] : new bool[slots.Count];
            foreach (var parameter in parameters) Add(parameter.Name);
        }

        internal bool Contains(string name) => _slots.TryGetValue(name, out var slot) && _assigned[slot];

        internal bool IsUnassignedLocal(string name) => _slots.TryGetValue(name, out var slot) &&
            !_assigned[slot] && _nestedNonlocals?.Contains(name) != true;

        internal int SavedCount => _saved?.Count ?? 0;

        internal void Add(string name)
        {
            if (_slots.TryGetValue(name, out var slot)) SetAssigned(slot, true);
        }

        internal void Remove(string name)
        {
            if (_slots.TryGetValue(name, out var slot)) SetAssigned(slot, false);
        }

        private void SetAssigned(int slot, bool assigned)
        {
            if (_assigned[slot] == assigned) return;
            _assigned[slot] = assigned;
            if (_branchDepth != 0) (_changes ??= []).Add(new(slot, assigned));
        }

        internal int BeginBranch()
        {
            _branchDepth++;
            return _changes?.Count ?? 0;
        }

        internal void Rewind(int start)
        {
            _branchDepth--;
            if (_changes is not { } changes || changes.Count == start) return;
            for (var i = changes.Count - 1; i >= start; i--)
            {
                if ((i & 255) == 0) _context.CheckExecution();
                var change = changes[i];
                _assigned[change.Slot] = !change.Added;
            }
            changes.RemoveRange(start, changes.Count - start);
        }

        internal void SaveAndRewind(int start)
        {
            if (_changes is { } changes)
            {
                for (var i = start; i < changes.Count; i++)
                {
                    if ((i & 255) == 0) _context.CheckExecution();
                    var change = changes[i];
                    if (change.Added && _assigned[change.Slot]) (_saved ??= []).Add(change.Slot);
                }
            }
            Rewind(start);
        }

        internal void ApplySaved(int start)
        {
            if (_saved is not { } saved || saved.Count == start) return;
            for (var i = start; i < saved.Count; i++)
            {
                if ((i & 255) == 0) _context.CheckExecution();
                SetAssigned(saved[i], true);
            }
            saved.RemoveRange(start, saved.Count - start);
        }

        internal void Merge(int start)
        {
            var saved = SavedCount;
            SaveAndRewind(start);
            ApplySaved(saved);
        }
    }
}
