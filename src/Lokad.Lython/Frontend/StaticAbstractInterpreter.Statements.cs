namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private static void AnalyzeStatement(
        StatementSyntax statement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax assignment:
            {
                AnalyzeExpression(assignment.Expression, diagnostics, bindings);
                return;
            }

            case ChainedAssignmentStatementSyntax chained:
            {
                AnalyzeExpression(chained.Expression, diagnostics, bindings);
                foreach (var target in chained.Targets)
                {
                    AnalyzeAssignmentTarget(target, diagnostics, bindings);
                }

                return;
            }

            case AnnotatedAssignmentStatementSyntax annotated:
            {
                AnalyzeExpressionIfPresent(annotated.Expression, diagnostics, bindings);
                AnalyzeAnnotatedTarget(annotated.Target, diagnostics, bindings);
                return;
            }

            case SubscriptAssignmentStatementSyntax subscript:
            {
                AnalyzeExpression(subscript.Target, diagnostics, bindings);
                AnalyzeExpression(subscript.Index, diagnostics, bindings);
                AnalyzeExpression(subscript.Expression, diagnostics, bindings);
                return;
            }

            case SliceAssignmentStatementSyntax slice:
            {
                AnalyzeExpression(slice.Target, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                AnalyzeExpression(slice.Expression, diagnostics, bindings);
                StaticStructuralDiagnostics.AnalyzeSliceAssignment(slice, diagnostics, bindings);
                return;
            }

            case MemberAssignmentStatementSyntax member:
            {
                AnalyzeExpression(member.Target, diagnostics, bindings);
                AnalyzeExpression(member.Expression, diagnostics, bindings);
                return;
            }

            case AugmentedAssignmentStatementSyntax augmented:
            {
                AnalyzeAssignmentTarget(augmented.Target, diagnostics, bindings);
                AnalyzeExpression(augmented.Expression, diagnostics, bindings);
                return;
            }

            case UnpackingAssignmentStatementSyntax unpacking:
            {
                AnalyzeExpression(unpacking.Expression, diagnostics, bindings);
                StaticDestructuringDiagnostics.AnalyzeUnpackingTargets(unpacking.Targets, unpacking.Expression, unpacking.Span, diagnostics, bindings);
                return;
            }

            case WithStatementSyntax withStatement:
            {
                AnalyzeExpression(withStatement.ContextExpression, diagnostics, bindings);
                // The target can shadow a name used by the context expression.
                // Read its incoming fact before binding any of the targets.
                var contextValue = withStatement.VariableName is null ? default
                    : StaticAbstractValueResolver.ResolveOrUnknown(withStatement.ContextExpression, bindings);
                foreach (var name in withStatement.BoundNames)
                    bindings.Remove(name);
                if (withStatement.VariableName is { } variable && contextValue.Kind is
                    AbstractValueKind.TextFileHandle or AbstractValueKind.BinaryFileHandle or AbstractValueKind.StringIO or AbstractValueKind.BytesIO)
                {
                    bindings.Set(variable, contextValue);
                }

                AnalyzeStatements(withStatement.Body, diagnostics, bindings);
                foreach (var name in withStatement.BoundNames)
                    bindings.Remove(name);
                return;
            }

            case IfStatementSyntax ifStatement:
            {
                AnalyzeIf(ifStatement, diagnostics, bindings);
                return;
            }

            case ForStatementSyntax forStatement:
            {
                AnalyzeExpression(forStatement.Iterable, diagnostics, bindings);
                StaticIterationDiagnostics.AnalyzeLoopTarget(forStatement.Target, forStatement.Iterable, forStatement.Span, diagnostics, bindings);
                var forBodyBindings = bindings.Clone();
                StaticBindingEngine.BindLoopTargetFromIterable(forStatement.Target, forStatement.Iterable, forBodyBindings);
                AnalyzeStatements(forStatement.Body, diagnostics, forBodyBindings);
                var forElseBindings = forStatement.ElseStatements is null ? null : bindings.Clone();
                bindings.JoinBranch(forBodyBindings);
                if (forElseBindings is not null)
                {
                    AnalyzeStatements(forStatement.ElseStatements!, diagnostics, forElseBindings);
                    bindings.JoinBranch(forElseBindings);
                }
                return;
            }

            case WhileStatementSyntax whileStatement:
            {
                AnalyzeExpression(whileStatement.Condition, diagnostics, bindings);
                var whileBodyBindings = bindings.Clone();
                if (!TryResolveConditionTruth(whileStatement.Condition, bindings, out var whileTruth) || whileTruth)
                {
                    StaticConditionRefinements.Apply(whileStatement.Condition, assumedTruth: true, whileBodyBindings);
                    AnalyzeStatements(whileStatement.Body, diagnostics, whileBodyBindings);
                }

                var whileElseBindings = whileStatement.ElseStatements is null ? null : bindings.Clone();
                bindings.JoinBranch(whileBodyBindings);
                if (whileElseBindings is not null)
                {
                    StaticConditionRefinements.Apply(whileStatement.Condition, assumedTruth: false, whileElseBindings);
                    AnalyzeStatements(whileStatement.ElseStatements!, diagnostics, whileElseBindings);
                    bindings.JoinBranch(whileElseBindings);
                }
                return;
            }

            case MatchStatementSyntax matchStatement:
            {
                AnalyzeExpression(matchStatement.Subject, diagnostics, bindings);
                AbstractState? mergedMatchBindings = null;
                foreach (var matchCase in matchStatement.Cases)
                {
                    var caseBindings = bindings.Clone();
                    if (matchCase.Guard is not null)
                    {
                        AnalyzeExpression(matchCase.Guard, diagnostics, caseBindings);
                        bindings.JoinBranch(caseBindings);
                        StaticConditionRefinements.Apply(matchCase.Guard, assumedTruth: true, caseBindings);
                    }

                    AnalyzeStatements(matchCase.Body, diagnostics, caseBindings);
                    if (mergedMatchBindings is null) mergedMatchBindings = caseBindings;
                    else mergedMatchBindings.JoinBranch(caseBindings);
                }

                if (mergedMatchBindings is not null)
                    bindings.JoinBranch(mergedMatchBindings);

                return;
            }

            case TryStatementSyntax tryStatement:
            {
                AnalyzeTry(tryStatement, diagnostics, bindings);
                return;
            }

            case FunctionDefinitionStatementSyntax functionDefinition:
            {
                var mayMutateCollections = functionDefinition.Decorators.Count != 0 ||
                    AnnotationMayInvokeGuest(functionDefinition.ReturnAnnotation);
                foreach (var decorator in functionDefinition.Decorators)
                {
                    AnalyzeExpression(decorator, diagnostics, bindings);
                }

                foreach (var parameter in functionDefinition.Parameters)
                {
                    AnalyzeExpressionIfPresent(parameter.DefaultValue, diagnostics, bindings);
                    mayMutateCollections |= AnnotationMayInvokeGuest(parameter.Annotation);
                }
                // Applying decorators and evaluating eager annotations can
                // mutate aliases without an explicit call in the outer scope.
                if (mayMutateCollections) bindings.InvalidateCollectionFacts();

                if (ScopeDirectiveFactsCollector.ForFunction(functionDefinition).ContainsDirectivesInBody)
                {
                    InvalidateDirectiveBindings(functionDefinition.Body, bindings);
                    bindings.InvalidateMutableSequenceFacts();
                    return;
                }

                var functionBindings = bindings.CloneForDeferredBody();
                if (functionDefinition.TypeParameters is { } typeParameters)
                    foreach (var typeParameter in typeParameters)
                        functionBindings.Remove(typeParameter.Name);
                foreach (var parameter in functionDefinition.Parameters)
                {
                    functionBindings.Remove(parameter.Name);
                }

                AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings);
                return;
            }

            case ClassDefinitionStatementSyntax classDefinition:
            {
                foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(classDefinition))
                    AnalyzeExpression(expression, diagnostics, bindings);
                var classFacts = ScopeDirectiveFactsCollector.ForClass(classDefinition);
                if (classFacts.GlobalNames.Count != 0 || classFacts.NonlocalNames.Count != 0)
                {
                    foreach (var name in classFacts.GlobalNames.Concat(classFacts.NonlocalNames))
                        bindings.Remove(name);
                    bindings.InvalidateMutableSequenceFacts();
                    return;
                }

                // Methods can later rebind outer names through global/nonlocal.
                // The class-local clone must not hide those effects from its parent.
                if (classFacts.ContainsDirectivesInBody)
                    InvalidateDirectiveBindings(classDefinition.Body, bindings);
                AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone());
                return;
            }

            case ExpressionStatementSyntax expressionStatement:
            {
                AnalyzeExpression(expressionStatement.Expression, diagnostics, bindings);
                break;
            }

            case AssertStatementSyntax assertStatement:
            {
                AnalyzeExpression(assertStatement.Condition, diagnostics, bindings);
                AnalyzeExpressionIfPresent(assertStatement.Message, diagnostics, bindings);
                StaticConditionRefinements.Apply(assertStatement.Condition, assumedTruth: true, bindings);
                break;
            }

            case DeleteStatementSyntax deleteStatement:
            {
                AnalyzeExpression(deleteStatement.Target, diagnostics, bindings);
                break;
            }

            case ReturnStatementSyntax { Expression: not null } returnStatement:
            {
                AnalyzeExpression(returnStatement.Expression, diagnostics, bindings);
                break;
            }

            case RaiseStatementSyntax raiseStatement:
            {
                if (raiseStatement.Expression is not null)
                    AnalyzeExpression(raiseStatement.Expression, diagnostics, bindings);
                if (raiseStatement.CauseExpression is not null)
                    AnalyzeExpression(raiseStatement.CauseExpression, diagnostics, bindings);
                break;
            }
        }
    }

    private static bool AnnotationMayInvokeGuest(ExpressionSyntax? annotation)
        => annotation is { PostponedAnnotation: null } and not (IdentifierExpressionSyntax or StringLiteralExpressionSyntax);

    private static void AnalyzeAnnotatedTarget(
        AssignmentTargetSyntax target,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        // Names bind through StaticBindingEngine; every other target reads like
        // the matching single-target assignment.
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeExpression(subscript.Target, diagnostics, bindings);
                AnalyzeExpression(subscript.Index, diagnostics, bindings);
                break;
            case SliceAssignmentTargetSyntax slice:
                AnalyzeExpression(slice.Target, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                break;
            case MemberAssignmentTargetSyntax member:
                AnalyzeExpression(member.Target, diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzeIf(
        IfStatementSyntax ifStatement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        AnalyzeExpression(ifStatement.Condition, diagnostics, bindings);
        if (TryResolveConditionTruth(ifStatement.Condition, bindings, out var conditionTruth))
        {
            var body = conditionTruth ? ifStatement.ThenStatements : ifStatement.ElseStatements;
            if (body is not null)
            {
                StaticConditionRefinements.Apply(ifStatement.Condition, conditionTruth, bindings);
                AnalyzeStatements(body, diagnostics, bindings);
            }

            return;
        }

        var thenExits = StatementsAlwaysExit(ifStatement.ThenStatements);
        var elseExits = ifStatement.ElseStatements is not null && StatementsAlwaysExit(ifStatement.ElseStatements);
        // Keep the incoming maps on the continuing path when one branch ends.
        // Both branches still run in source order for diagnostic purposes.
        var keepThen = !thenExits && elseExits;
        var fork = bindings.Clone();
        var thenBindings = keepThen ? bindings : fork;
        var elseBindings = keepThen ? fork : bindings;
        bindings.InvalidateCachedFacts();
        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, thenBindings);
        AnalyzeStatements(ifStatement.ThenStatements, diagnostics, thenBindings);
        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
        if (ifStatement.ElseStatements is not null)
            AnalyzeStatements(ifStatement.ElseStatements, diagnostics, elseBindings);
        if (thenExits != elseExits)
        {
            bindings.InvalidateCachedFacts();
            return;
        }

        thenBindings.JoinBranch(elseBindings);
        bindings.TakeBranchResult(thenBindings);
    }

    private static void AnalyzeTry(
        TryStatementSyntax tryStatement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var handlers = tryStatement.ExceptClauses;
        AbstractState? exceptBindings = null;
        if (handlers.Count == 0)
        {
            bindings.InvalidateCachedFacts();
            AnalyzeStatements(tryStatement.TryBody, diagnostics, bindings);
            AnalyzeRemainingSuites(bindings);
            bindings.InvalidateCachedFacts();
            return;
        }

        // A runtime contract failure in the protected body can be the exception
        // the script intends to catch. Keep successful-path facts, and continue
        // to report diagnostics for handlers and the successful else suite.
        var tryBindings = bindings.Clone();
        AnalyzeStatements(tryStatement.TryBody, [], tryBindings);

        for (var i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];
            var clauseBindings = i == handlers.Count - 1 ? bindings : bindings.Clone();
            clauseBindings.InvalidateCachedFacts();
            // The protected body can partially mutate retained containers
            // before a computed header or handler suite observes them.
            clauseBindings.InvalidateCollectionFacts();
            if (handler.ExceptionTypeExpression is { } header)
                AnalyzeExpression(header, [], clauseBindings);
            AnalyzeStatements(handler.Body, diagnostics, clauseBindings);

            if (exceptBindings is null) exceptBindings = clauseBindings;
            else exceptBindings.JoinBranch(clauseBindings);
        }

        AnalyzeRemainingSuites(tryBindings);
        bindings.TakeBranchResult(tryBindings);

        void AnalyzeRemainingSuites(AbstractState continuation)
        {
            if (tryStatement.ElseBody is not null)
                AnalyzeStatements(tryStatement.ElseBody, diagnostics, continuation);
            if (exceptBindings is not null)
                continuation.JoinBranch(exceptBindings);
            if (tryStatement.FinallyBody is not null)
                AnalyzeStatements(tryStatement.FinallyBody, diagnostics, continuation);
        }
    }

    private static void InvalidateDirectiveBindings(
        IReadOnlyList<StatementSyntax> statements,
        AbstractState bindings)
    {
        foreach (var statement in statements)
        {
            if (statement is ScopeDirectiveStatementSyntax directive)
                foreach (var name in directive.Names)
                    bindings.Remove(name);
            foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                InvalidateDirectiveBindings(body, bindings);
        }
    }
}
