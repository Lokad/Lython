namespace Lokad.Lython.Frontend;

internal static class StaticScopeDirectiveDiagnostics
{
    private sealed class EnclosingFunctionBindings
    {
        private readonly List<ScopeDirectiveFacts> _scopes = [];

        public int Depth => _scopes.Count(facts => !ReferenceEquals(facts, ClassCellSyntaxFacts.Scope));

        public bool Contains(string name)
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                if (_scopes[i].IsGlobal(name)) return false;
                if (_scopes[i].LocalNames.Contains(name)) return true;
            }
            return false;
        }

        public void Enter(ScopeDirectiveFacts facts) => _scopes.Add(facts);

        public void Leave(ScopeDirectiveFacts facts) => _scopes.RemoveAt(_scopes.Count - 1);
    }

    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeStatements(context.Script.Statements, context, new EnclosingFunctionBindings(), inClassBody: false);
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        EnclosingFunctionBindings enclosingFunctions,
        bool inClassBody)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ScopeDirectiveStatementSyntax directive:
                    if (directive.Kind == ScopeDirectiveKind.Nonlocal && enclosingFunctions.Depth == 0 &&
                        !directive.Names.All(enclosingFunctions.Contains))
                    {
                        context.AddError("LA3201", "`nonlocal` requires an enclosing function scope.", directive.Span);
                    }

                    break;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    AnalyzeFunction(functionDefinition, context, enclosingFunctions);
                    break;

                case ClassDefinitionStatementSyntax classDefinition:
                    var classFacts = ScopeDirectiveFactsCollector.ForClass(classDefinition);
                    AnalyzeDirectiveBindings(classFacts, classDefinition.Span, context, enclosingFunctions);
                    AnalyzeUseBeforeDirective(classDefinition.Body,
                        new HashSet<string>(classFacts.GlobalNames.Concat(classFacts.NonlocalNames), StringComparer.Ordinal),
                        new HashSet<string>(StringComparer.Ordinal), context);
                    enclosingFunctions.Enter(ClassCellSyntaxFacts.Scope);
                    try
                    {
                        AnalyzeStatements(classDefinition.Body, context, enclosingFunctions, inClassBody: true);
                    }
                    finally
                    {
                        enclosingFunctions.Leave(ClassCellSyntaxFacts.Scope);
                    }
                    break;

                case IfStatementSyntax ifStatement:
                    AnalyzeStatements(ifStatement.ThenStatements, context, enclosingFunctions, inClassBody);
                    if (ifStatement.ElseStatements is not null) AnalyzeStatements(ifStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case ForStatementSyntax forStatement:
                    AnalyzeStatements(forStatement.Body, context, enclosingFunctions, inClassBody);
                    if (forStatement.ElseStatements is not null) AnalyzeStatements(forStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case WhileStatementSyntax whileStatement:
                    AnalyzeStatements(whileStatement.Body, context, enclosingFunctions, inClassBody);
                    if (whileStatement.ElseStatements is not null) AnalyzeStatements(whileStatement.ElseStatements, context, enclosingFunctions, inClassBody);
                    break;

                case WithStatementSyntax withStatement:
                    AnalyzeStatements(withStatement.Body, context, enclosingFunctions, inClassBody);
                    break;

                case TryStatementSyntax tryStatement:
                    AnalyzeStatements(tryStatement.TryBody, context, enclosingFunctions, inClassBody);
                    foreach (var exceptClause in tryStatement.ExceptClauses) AnalyzeStatements(exceptClause.Body, context, enclosingFunctions, inClassBody);
                    if (tryStatement.ElseBody is not null) AnalyzeStatements(tryStatement.ElseBody, context, enclosingFunctions, inClassBody);
                    if (tryStatement.FinallyBody is not null) AnalyzeStatements(tryStatement.FinallyBody, context, enclosingFunctions, inClassBody);
                    break;

                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        AnalyzeStatements(matchCase.Body, context, enclosingFunctions, inClassBody);
                    }
                    break;
            }
        }
    }

    private static void AnalyzeFunction(
        FunctionDefinitionStatementSyntax functionDefinition,
        StaticAnalysisContext context,
        EnclosingFunctionBindings enclosingFunctions)
    {
        var facts = ScopeDirectiveFactsCollector.ForFunction(functionDefinition);
        AnalyzeDirectiveBindings(facts, functionDefinition.Span, context, enclosingFunctions);

        var parameterNames = new HashSet<string>(
            functionDefinition.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        foreach (var name in facts.GlobalNames.Concat(facts.NonlocalNames))
        {
            if (parameterNames.Contains(name))
            {
                context.AddError("LA3204", $"Parameter '{name}' cannot also be declared global or nonlocal.", functionDefinition.Span);
            }
        }

        AnalyzeUseBeforeDirective(functionDefinition, facts, context);

        enclosingFunctions.Enter(facts);
        try
        {
            AnalyzeStatements(functionDefinition.Body, context, enclosingFunctions, inClassBody: false);
        }
        finally
        {
            enclosingFunctions.Leave(facts);
        }
    }

    private static void AnalyzeDirectiveBindings(
        ScopeDirectiveFacts facts,
        LythonSourceSpan span,
        StaticAnalysisContext context,
        EnclosingFunctionBindings enclosingFunctions)
    {
        foreach (var name in facts.GlobalNames.Intersect(facts.NonlocalNames, StringComparer.Ordinal))
        {
            context.AddError("LA3203", $"Name '{name}' cannot be declared both global and nonlocal.", span);
        }

        foreach (var name in facts.NonlocalNames)
        {
            if (!enclosingFunctions.Contains(name))
            {
                context.AddError("LA3205", $"No enclosing function binding exists for nonlocal name '{name}'.", span);
            }
        }

    }

    private static void AnalyzeUseBeforeDirective(
        FunctionDefinitionStatementSyntax functionDefinition,
        ScopeDirectiveFacts facts,
        StaticAnalysisContext context)
    {
        var directiveNames = new HashSet<string>(facts.GlobalNames.Concat(facts.NonlocalNames), StringComparer.Ordinal);
        if (directiveNames.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(
            functionDefinition.Parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);
        AnalyzeUseBeforeDirective(functionDefinition.Body, directiveNames, seen, context);
    }

    private static void AnalyzeUseBeforeDirective(
        IReadOnlyList<StatementSyntax> statements,
        HashSet<string> directiveNames,
        HashSet<string> seen,
        StaticAnalysisContext context)
    {
        foreach (var statement in statements)
        {
            if (statement is ScopeDirectiveStatementSyntax directive)
            {
                foreach (var name in directive.Names)
                {
                    if (directiveNames.Contains(name) && seen.Contains(name))
                    {
                        context.AddError("LA3206", $"Name '{name}' is used or assigned before its scope directive.", directive.Span);
                    }
                }
                continue;
            }

            CollectSeenNames(statement, seen, directive =>
            {
                foreach (var name in directive.Names)
                    if (directiveNames.Contains(name) && seen.Contains(name))
                        context.AddError("LA3206", $"Name '{name}' is used or assigned before its scope directive.", directive.Span);
            }, annotated =>
            {
                if (annotated.Target is NameAssignmentTargetSyntax name && directiveNames.Contains(name.Name))
                    context.AddError("LA3207", $"Declared global or nonlocal name '{name.Name}' cannot be annotated.", annotated.Span);
            });
        }
    }

    private static void CollectSeenNames(StatementSyntax statement, HashSet<string> names,
        Action<ScopeDirectiveStatementSyntax>? validateDirective = null,
        Action<AnnotatedAssignmentStatementSyntax>? validateAnnotation = null)
    {
        switch (statement)
        {
            case ScopeDirectiveStatementSyntax directive:
                validateDirective?.Invoke(directive);
                break;
            case ImportStatementSyntax importStatement:
                names.Add(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var memberName in ImportSyntaxFacts.EnumerateBindingNames(importStatement)) names.Add(memberName);
                }
                break;
            case TypeAliasStatementSyntax alias:
                names.Add(alias.Name);
                break;
            case AssignmentStatementSyntax assignment:
                names.Add(assignment.Name);
                CollectSeenNames(assignment.Expression, names);
                break;
            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets) CollectSeenNames(target, names);
                CollectSeenNames(chained.Expression, names);
                break;
            case AnnotatedAssignmentStatementSyntax annotated:
                validateAnnotation?.Invoke(annotated);
                CollectSeenNames(annotated.Target, names);
                CollectSeenNames(annotated.Annotation, names);
                if (annotated.Expression is not null) CollectSeenNames(annotated.Expression, names);
                break;
            case AugmentedAssignmentStatementSyntax { Target: NameAssignmentTargetSyntax nameTarget } augmented:
                names.Add(nameTarget.Name);
                CollectSeenNames(augmented.Expression, names);
                break;
            case AugmentedAssignmentStatementSyntax augmented:
                CollectSeenNames(augmented.Target, names);
                CollectSeenNames(augmented.Expression, names);
                break;
            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets) CollectSeenNames(target, names);
                CollectSeenNames(unpacking.Expression, names);
                break;
            case SubscriptAssignmentStatementSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                CollectSeenNames(subscript.Expression, names);
                break;
            case SliceAssignmentStatementSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                CollectSeenNames(slice.Expression, names);
                break;
            case MemberAssignmentStatementSyntax member:
                CollectSeenNames(member.Target, names);
                CollectSeenNames(member.Expression, names);
                break;
            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(functionDefinition))
                    CollectSeenNames(expression, names);
                names.Add(functionDefinition.Name);
                break;
            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(classDefinition))
                    CollectSeenNames(expression, names);
                names.Add(classDefinition.Name);
                break;
            case ForStatementSyntax forStatement:
                CollectSeenNames(forStatement.Target, names);
                CollectSeenNames(forStatement.Iterable, names);
                AnalyzeNestedSeen(forStatement.Body, names, validateDirective, validateAnnotation);
                if (forStatement.ElseStatements is not null) AnalyzeNestedSeen(forStatement.ElseStatements, names, validateDirective, validateAnnotation);
                break;
            case WithStatementSyntax withStatement:
                names.UnionWith(withStatement.BoundNames);
                if (withStatement.Target is not null)
                    foreach (var read in AssignmentTargetFacts.Reads(withStatement.Target)) CollectSeenNames(read, names);
                CollectSeenNames(withStatement.ContextExpression, names);
                AnalyzeNestedSeen(withStatement.Body, names, validateDirective, validateAnnotation);
                break;
            case IfStatementSyntax ifStatement:
                CollectSeenNames(ifStatement.Condition, names);
                AnalyzeNestedSeen(ifStatement.ThenStatements, names, validateDirective, validateAnnotation);
                if (ifStatement.ElseStatements is not null) AnalyzeNestedSeen(ifStatement.ElseStatements, names, validateDirective, validateAnnotation);
                break;
            case WhileStatementSyntax whileStatement:
                CollectSeenNames(whileStatement.Condition, names);
                AnalyzeNestedSeen(whileStatement.Body, names, validateDirective, validateAnnotation);
                if (whileStatement.ElseStatements is not null) AnalyzeNestedSeen(whileStatement.ElseStatements, names, validateDirective, validateAnnotation);
                break;
            case ExpressionStatementSyntax expressionStatement:
                CollectSeenNames(expressionStatement.Expression, names);
                break;
            case MatchStatementSyntax matchStatement:
                CollectSeenNames(matchStatement.Subject, names);
                foreach (var matchCase in matchStatement.Cases)
                {
                    CollectPatternNames(matchCase.Pattern, names);
                    if (matchCase.Guard is not null) CollectSeenNames(matchCase.Guard, names);
                    AnalyzeNestedSeen(matchCase.Body, names, validateDirective, validateAnnotation);
                }
                break;
            case AssertStatementSyntax assertStatement:
                CollectSeenNames(assertStatement.Condition, names);
                if (assertStatement.Message is not null) CollectSeenNames(assertStatement.Message, names);
                break;
            case DeleteStatementSyntax deleteStatement:
                CollectSeenNames(deleteStatement.Target, names);
                break;
            case ReturnStatementSyntax { Expression: { } expression }:
                CollectSeenNames(expression, names);
                break;
            case RaiseStatementSyntax raiseStatement:
                if (raiseStatement.Expression is not null) CollectSeenNames(raiseStatement.Expression, names);
                if (raiseStatement.CauseExpression is not null) CollectSeenNames(raiseStatement.CauseExpression, names);
                break;
            case TryStatementSyntax tryStatement:
                AnalyzeNestedSeen(tryStatement.TryBody, names, validateDirective, validateAnnotation);
                foreach (var exceptClause in tryStatement.ExceptClauses)
                {
                    if (exceptClause.ExceptionTypeExpression is { } header) CollectSeenNames(header, names);
                    if (exceptClause.ExceptionVariableName is not null) names.Add(exceptClause.ExceptionVariableName);
                    AnalyzeNestedSeen(exceptClause.Body, names, validateDirective, validateAnnotation);
                }
                if (tryStatement.ElseBody is not null) AnalyzeNestedSeen(tryStatement.ElseBody, names, validateDirective, validateAnnotation);
                if (tryStatement.FinallyBody is not null) AnalyzeNestedSeen(tryStatement.FinallyBody, names, validateDirective, validateAnnotation);
                break;
        }
    }

    private static void AnalyzeNestedSeen(IReadOnlyList<StatementSyntax> statements, HashSet<string> names,
        Action<ScopeDirectiveStatementSyntax>? validateDirective,
        Action<AnnotatedAssignmentStatementSyntax>? validateAnnotation)
    {
        foreach (var statement in statements)
            CollectSeenNames(statement, names, validateDirective, validateAnnotation);
    }

    private static void CollectSeenNames(AssignmentTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                names.Add(name.Name);
                break;
            case UnpackingAssignmentTargetGroupSyntax group:
                foreach (var nested in group.Targets) CollectSeenNames(nested, names);
                break;
            case SubscriptAssignmentTargetSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                break;
            case SliceAssignmentTargetSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                break;
            case MemberAssignmentTargetSyntax member:
                CollectSeenNames(member.Target, names);
                break;
        }
    }

    private static void CollectSeenNames(UnpackingTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case UnpackingNameTargetSyntax name:
                names.Add(name.Name);
                break;

            case UnpackingSubscriptTargetSyntax subscript:
                CollectSeenNames(subscript.Target, names);
                CollectSeenNames(subscript.Index, names);
                break;

            case UnpackingSliceTargetSyntax slice:
                CollectSeenNames(slice.Target, names);
                if (slice.Start is not null) CollectSeenNames(slice.Start, names);
                if (slice.End is not null) CollectSeenNames(slice.End, names);
                if (slice.Step is not null) CollectSeenNames(slice.Step, names);
                break;

            case UnpackingMemberTargetSyntax member:
                CollectSeenNames(member.Target, names);
                break;

            case UnpackingNestedTargetSyntax nested:
                foreach (var nestedItem in nested.Items) CollectSeenNames(nestedItem, names);
                break;
        }
    }

    private static void CollectSeenNames(LoopTargetSyntax target, HashSet<string> names)
    {
        switch (target)
        {
            case LoopStoreTargetSyntax store:
                foreach (var read in AssignmentTargetFacts.Reads(store.Target)) CollectSeenNames(read, names);
                foreach (var name in AssignmentTargetFacts.Names(store.Target)) names.Add(name);
                break;
            case LoopNameTargetSyntax name:
                names.Add(name.Name);
                break;
            case LoopStarredTargetSyntax starred:
                names.Add(starred.Name);
                break;
            case LoopTupleTargetSyntax tuple:
                foreach (var item in tuple.Items) CollectSeenNames(item, names);
                break;
        }
    }

    private static void CollectSeenNames(ExpressionSyntax expression, HashSet<string> names)
    {
        if (expression is LambdaExpressionSyntax)
        {
            return;
        }

        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                names.Add(identifier.Name);
                break;
            case AssignmentExpressionSyntax assignment:
                names.Add(assignment.Name);
                break;
            case ListComprehensionExpressionSyntax listComprehension:
                CollectComprehensionTargetNames(listComprehension.Clauses, names);
                break;
            case GeneratorExpressionSyntax generator:
                CollectComprehensionTargetNames(generator.Clauses, names);
                break;
            case SetComprehensionExpressionSyntax setComprehension:
                CollectComprehensionTargetNames(setComprehension.Clauses, names);
                break;
            case DictComprehensionExpressionSyntax dictComprehension:
                CollectComprehensionTargetNames(dictComprehension.Clauses, names);
                break;
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            CollectSeenNames(child, names);
        }
    }

    private static void CollectComprehensionTargetNames(IReadOnlyList<ComprehensionClauseSyntax> clauses, HashSet<string> names)
    {
        foreach (var clause in clauses)
        {
            CollectSeenNames(clause.Target, names);
        }
    }

    private static void CollectPatternNames(PatternSyntax pattern, HashSet<string> names)
    {
        switch (pattern)
        {
            case MatchCapturePatternSyntax capture:
                names.Add(capture.Name);
                break;
            case MatchSequencePatternSyntax sequence:
                foreach (var item in sequence.Items) CollectPatternNames(item, names);
                break;
            case MatchMappingPatternSyntax mapping:
                foreach (var item in mapping.Items) CollectPatternNames(item.Pattern, names);
                if (mapping.RestName is not null) names.Add(mapping.RestName);
                break;
            case MatchClassPatternSyntax classPattern:
                foreach (var item in classPattern.PositionalPatterns) CollectPatternNames(item, names);
                foreach (var item in classPattern.KeywordPatterns) CollectPatternNames(item.Pattern, names);
                break;
            case MatchStarPatternSyntax { Name: { } name }:
                names.Add(name);
                break;
            case MatchAsPatternSyntax asPattern:
                CollectPatternNames(asPattern.Pattern, names);
                names.Add(asPattern.Name);
                break;
            case MatchOrPatternSyntax orPattern:
                foreach (var item in orPattern.Patterns) CollectPatternNames(item, names);
                break;
        }
    }
}
