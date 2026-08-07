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
            switch (statement)
            {
                case FunctionDefinitionStatementSyntax functionDefinition:
                    AnalyzeFunctionDefinition(functionDefinition, context);
                    break;
                case ClassDefinitionStatementSyntax classDefinition:
                    AnalyzeNestedFunctionDefinitions(classDefinition.Body, context);
                    break;
                case IfStatementSyntax ifStatement:
                    AnalyzeNestedFunctionDefinitions(ifStatement.ThenStatements, context);
                    if (ifStatement.ElseStatements is not null) AnalyzeNestedFunctionDefinitions(ifStatement.ElseStatements, context);
                    break;
                case ForStatementSyntax forStatement:
                    AnalyzeNestedFunctionDefinitions(forStatement.Body, context);
                    if (forStatement.ElseStatements is not null) AnalyzeNestedFunctionDefinitions(forStatement.ElseStatements, context);
                    break;
                case WhileStatementSyntax whileStatement:
                    AnalyzeNestedFunctionDefinitions(whileStatement.Body, context);
                    if (whileStatement.ElseStatements is not null) AnalyzeNestedFunctionDefinitions(whileStatement.ElseStatements, context);
                    break;
                case WithStatementSyntax withStatement:
                    AnalyzeNestedFunctionDefinitions(withStatement.Body, context);
                    break;
                case TryStatementSyntax tryStatement:
                    AnalyzeNestedFunctionDefinitions(tryStatement.TryBody, context);
                    if (tryStatement.ExceptBody is not null) AnalyzeNestedFunctionDefinitions(tryStatement.ExceptBody, context);
                    if (tryStatement.ElseBody is not null) AnalyzeNestedFunctionDefinitions(tryStatement.ElseBody, context);
                    if (tryStatement.FinallyBody is not null) AnalyzeNestedFunctionDefinitions(tryStatement.FinallyBody, context);
                    break;
                case MatchStatementSyntax matchStatement:
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        AnalyzeNestedFunctionDefinitions(matchCase.Body, context);
                    }
                    break;
            }
        }
    }

    private static void AnalyzeFunctionDefinition(FunctionDefinitionStatementSyntax functionDefinition, StaticAnalysisContext context)
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

        var localNames = new HashSet<string>(StringComparer.Ordinal);
        CollectLocalAssignments(functionDefinition.Body, localNames);
        var scopeFacts = ScopeDirectiveFactsCollector.ForFunction(functionDefinition);
        localNames.ExceptWith(scopeFacts.GlobalNames);
        localNames.ExceptWith(scopeFacts.NonlocalNames);

        var maybeAssigned = new HashSet<string>(
            functionDefinition.Parameters
                .Select(static parameter => parameter.Name)
                .Where(name => !scopeFacts.GlobalNames.Contains(name) && !scopeFacts.NonlocalNames.Contains(name)),
            StringComparer.Ordinal);
        AnalyzeStatements(functionDefinition.Body, context, localNames, maybeAssigned);
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                maybeAssigned.Add(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var memberName in EnumerateImportedBindingNames(importStatement))
                    {
                        maybeAssigned.Add(memberName);
                    }
                }
                break;

            case AssignmentStatementSyntax assignment:
                AnalyzeExpression(assignment.Expression, context, localNames, maybeAssigned);
                maybeAssigned.Add(assignment.Name);
                break;

            case ChainedAssignmentStatementSyntax chained:
                AnalyzeExpression(chained.Expression, context, localNames, maybeAssigned);
                foreach (var target in chained.Targets)
                {
                    AddAssignmentTarget(target, maybeAssigned);
                }
                break;

            case AnnotatedAssignmentStatementSyntax annotated:
                AnalyzeExpression(annotated.Annotation, context, localNames, maybeAssigned);
                if (annotated.Expression is not null)
                {
                    AnalyzeExpression(annotated.Expression, context, localNames, maybeAssigned);
                    maybeAssigned.Add(annotated.Name);
                }
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                AnalyzeExpression(subscript.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(subscript.Index, context, localNames, maybeAssigned);
                AnalyzeExpression(subscript.Expression, context, localNames, maybeAssigned);
                break;

            case SliceAssignmentStatementSyntax slice:
                AnalyzeExpression(slice.Target, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.Start, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.End, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.Step, context, localNames, maybeAssigned);
                AnalyzeExpression(slice.Expression, context, localNames, maybeAssigned);
                break;

            case MemberAssignmentStatementSyntax member:
                AnalyzeExpression(member.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(member.Expression, context, localNames, maybeAssigned);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                AnalyzeAugmentedAssignmentTarget(augmented.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(augmented.Expression, context, localNames, maybeAssigned);
                if (augmented.Target is NameAssignmentTargetSyntax augmentedName)
                {
                    maybeAssigned.Add(augmentedName.Name);
                }
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                AnalyzeExpression(unpacking.Expression, context, localNames, maybeAssigned);
                foreach (var target in unpacking.Targets)
                {
                    maybeAssigned.Add(target.Name);
                }
                break;

            case ExpressionStatementSyntax expressionStatement:
                AnalyzeExpression(expressionStatement.Expression, context, localNames, maybeAssigned);
                break;

            case WithStatementSyntax withStatement:
                AnalyzeExpression(withStatement.ContextExpression, context, localNames, maybeAssigned);
                if (withStatement.VariableName is not null)
                {
                    maybeAssigned.Add(withStatement.VariableName);
                }
                AnalyzeStatements(withStatement.Body, context, localNames, maybeAssigned);
                break;

            case IfStatementSyntax ifStatement:
                {
                    AnalyzeExpression(ifStatement.Condition, context, localNames, maybeAssigned);
                    var thenAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(ifStatement.ThenStatements, context, localNames, thenAssigned);
                    var elseAssigned = Clone(maybeAssigned);
                    if (ifStatement.ElseStatements is not null)
                    {
                        AnalyzeStatements(ifStatement.ElseStatements, context, localNames, elseAssigned);
                    }
                    maybeAssigned.UnionWith(thenAssigned);
                    maybeAssigned.UnionWith(elseAssigned);
                    break;
                }

            case ForStatementSyntax forStatement:
                {
                    AnalyzeExpression(forStatement.Iterable, context, localNames, maybeAssigned);
                    var bodyAssigned = Clone(maybeAssigned);
                    AddLoopTarget(forStatement.Target, bodyAssigned);
                    AnalyzeStatements(forStatement.Body, context, localNames, bodyAssigned);
                    maybeAssigned.UnionWith(bodyAssigned);
                    if (forStatement.ElseStatements is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(forStatement.ElseStatements, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    break;
                }

            case WhileStatementSyntax whileStatement:
                {
                    AnalyzeExpression(whileStatement.Condition, context, localNames, maybeAssigned);
                    var bodyAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(whileStatement.Body, context, localNames, bodyAssigned);
                    maybeAssigned.UnionWith(bodyAssigned);
                    if (whileStatement.ElseStatements is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(whileStatement.ElseStatements, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    break;
                }

            case MatchStatementSyntax matchStatement:
                {
                    AnalyzeExpression(matchStatement.Subject, context, localNames, maybeAssigned);
                    var unionAssigned = Clone(maybeAssigned);
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        if (matchCase.Guard is not null)
                        {
                            AnalyzeExpression(matchCase.Guard, context, localNames, maybeAssigned);
                        }
                        var caseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(matchCase.Body, context, localNames, caseAssigned);
                        unionAssigned.UnionWith(caseAssigned);
                    }
                    maybeAssigned.UnionWith(unionAssigned);
                    break;
                }

            case AssertStatementSyntax assertStatement:
                AnalyzeExpression(assertStatement.Condition, context, localNames, maybeAssigned);
                if (assertStatement.Message is not null) AnalyzeExpression(assertStatement.Message, context, localNames, maybeAssigned);
                break;

            case DeleteStatementSyntax deleteStatement:
                AnalyzeExpression(deleteStatement.Target, context, localNames, maybeAssigned);
                if (deleteStatement.Target is IdentifierExpressionSyntax identifier)
                {
                    maybeAssigned.Remove(identifier.Name);
                }
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators)
                {
                    AnalyzeExpression(decorator, context, localNames, maybeAssigned);
                }
                foreach (var parameter in functionDefinition.Parameters)
                {
                    if (parameter.Annotation is not null) AnalyzeExpression(parameter.Annotation, context, localNames, maybeAssigned);
                    if (parameter.DefaultValue is not null) AnalyzeExpression(parameter.DefaultValue, context, localNames, maybeAssigned);
                }
                if (functionDefinition.ReturnAnnotation is not null)
                {
                    AnalyzeExpression(functionDefinition.ReturnAnnotation, context, localNames, maybeAssigned);
                }
                maybeAssigned.Add(functionDefinition.Name);
                AnalyzeFunctionDefinition(functionDefinition, context);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators)
                {
                    AnalyzeExpression(decorator, context, localNames, maybeAssigned);
                }
                foreach (var @base in classDefinition.Bases)
                {
                    AnalyzeExpression(@base, context, localNames, maybeAssigned);
                }
                foreach (var keywordArgument in classDefinition.KeywordArguments)
                {
                    AnalyzeExpression(keywordArgument.Value, context, localNames, maybeAssigned);
                }
                maybeAssigned.Add(classDefinition.Name);
                AnalyzeNestedFunctionDefinitions(classDefinition.Body, context);
                break;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null:
                AnalyzeExpression(returnStatement.Expression, context, localNames, maybeAssigned);
                break;

            case RaiseStatementSyntax raiseStatement:
                AnalyzeExpression(raiseStatement.Expression, context, localNames, maybeAssigned);
                break;

            case TryStatementSyntax tryStatement:
                {
                    var tryAssigned = Clone(maybeAssigned);
                    AnalyzeStatements(tryStatement.TryBody, context, localNames, tryAssigned);
                    maybeAssigned.UnionWith(tryAssigned);
                    if (tryStatement.ExceptBody is not null)
                    {
                        var exceptAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(tryStatement.ExceptBody, context, localNames, exceptAssigned);
                        maybeAssigned.UnionWith(exceptAssigned);
                    }
                    if (tryStatement.ElseBody is not null)
                    {
                        var elseAssigned = Clone(maybeAssigned);
                        AnalyzeStatements(tryStatement.ElseBody, context, localNames, elseAssigned);
                        maybeAssigned.UnionWith(elseAssigned);
                    }
                    if (tryStatement.FinallyBody is not null)
                    {
                        AnalyzeStatements(tryStatement.FinallyBody, context, localNames, maybeAssigned);
                    }
                    break;
                }
        }
    }

    private static void AnalyzeExpressionIfPresent(
        ExpressionSyntax? expression,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        if (expression is not null)
        {
            AnalyzeExpression(expression, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeExpression(
        ExpressionSyntax expression,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                AnalyzeLocalRead(identifier.Name, identifier.Span, context, localNames, maybeAssigned);
                break;

            case FormattedStringExpressionSyntax formatted:
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    AnalyzeExpression(nestedExpression, context, localNames, maybeAssigned);
                }
                break;

            case ListLiteralExpressionSyntax list:
                AnalyzeExpressions(list.Items, context, localNames, maybeAssigned);
                break;

            case ListComprehensionExpressionSyntax listComprehension:
                AnalyzeExpression(listComprehension.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(listComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case GeneratorExpressionSyntax generator:
                AnalyzeExpression(generator.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(generator.Clauses, context, localNames, maybeAssigned);
                break;

            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    AnalyzeExpression(item.Key, context, localNames, maybeAssigned);
                    if (!item.IsUnpacking)
                    {
                        AnalyzeExpression(item.Value, context, localNames, maybeAssigned);
                    }
                }
                break;

            case SetLiteralExpressionSyntax set:
                AnalyzeExpressions(set.Items, context, localNames, maybeAssigned);
                break;

            case SetComprehensionExpressionSyntax setComprehension:
                AnalyzeExpression(setComprehension.ItemExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(setComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case DictComprehensionExpressionSyntax dictComprehension:
                AnalyzeExpression(dictComprehension.KeyExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeExpression(dictComprehension.ValueExpression, context, localNames, Clone(maybeAssigned));
                AnalyzeComprehensionClauses(dictComprehension.Clauses, context, localNames, maybeAssigned);
                break;

            case TupleLiteralExpressionSyntax tuple:
                AnalyzeExpressions(tuple.Items, context, localNames, maybeAssigned);
                break;

            case ParenthesizedExpressionSyntax parenthesized:
                AnalyzeExpression(parenthesized.Inner, context, localNames, maybeAssigned);
                break;

            case MemberExpressionSyntax member:
                AnalyzeExpression(member.Target, context, localNames, maybeAssigned);
                break;

            case CallExpressionSyntax call:
                AnalyzeExpression(call.Target, context, localNames, maybeAssigned);
                foreach (var argument in call.Arguments)
                {
                    AnalyzeExpression(argument.Expression, context, localNames, maybeAssigned);
                }
                break;

            case SubscriptExpressionSyntax subscript:
                AnalyzeExpression(subscript.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(subscript.Index, context, localNames, maybeAssigned);
                break;

            case SliceExpressionSyntax slice:
                AnalyzeExpression(slice.Target, context, localNames, maybeAssigned);
                if (slice.Start is not null) AnalyzeExpression(slice.Start, context, localNames, maybeAssigned);
                if (slice.End is not null) AnalyzeExpression(slice.End, context, localNames, maybeAssigned);
                if (slice.Step is not null) AnalyzeExpression(slice.Step, context, localNames, maybeAssigned);
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeExpression(binary.Left, context, localNames, maybeAssigned);
                AnalyzeExpression(binary.Right, context, localNames, maybeAssigned);
                break;

            case ChainedComparisonExpressionSyntax chained:
                AnalyzeExpressions(chained.Operands, context, localNames, maybeAssigned);
                break;

            case UnaryExpressionSyntax unary:
                AnalyzeExpression(unary.Operand, context, localNames, maybeAssigned);
                break;

            case ConditionalExpressionSyntax conditional:
                AnalyzeExpression(conditional.Condition, context, localNames, maybeAssigned);
                AnalyzeExpression(conditional.Consequent, context, localNames, maybeAssigned);
                AnalyzeExpression(conditional.Alternative, context, localNames, maybeAssigned);
                break;

            case AssignmentExpressionSyntax assignment:
                AnalyzeExpression(assignment.Expression, context, localNames, maybeAssigned);
                maybeAssigned.Add(assignment.Name);
                break;

            case LambdaExpressionSyntax lambda:
                {
                    var lambdaLocalNames = new HashSet<string>(StringComparer.Ordinal);
                    CollectLocalAssignments(lambda.Body, lambdaLocalNames);
                    var lambdaAssigned = new HashSet<string>(
                        lambda.Parameters.Select(static parameter => parameter.Name),
                        StringComparer.Ordinal);
                    AnalyzeExpression(lambda.Body, context, lambdaLocalNames, lambdaAssigned);
                    break;
                }
        }
    }

    private static void AnalyzeExpressions(
        IReadOnlyList<ExpressionSyntax> expressions,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        foreach (var expression in expressions)
        {
            AnalyzeExpression(expression, context, localNames, maybeAssigned);
        }
    }

    private static void AnalyzeComprehensionClauses(
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        var comprehensionAssigned = Clone(maybeAssigned);
        foreach (var clause in clauses)
        {
            AnalyzeExpression(clause.Iterable, context, localNames, comprehensionAssigned);
            AddLoopTarget(clause.Target, comprehensionAssigned);
            if (clause.Condition is not null)
            {
                AnalyzeExpression(clause.Condition, context, localNames, comprehensionAssigned);
            }
        }
    }

    private static void AnalyzeLocalRead(
        string name,
        LythonSourceSpan span,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        if (localNames.Contains(name) && !maybeAssigned.Contains(name))
        {
            StaticDiagnosticSink.AddError(
                context,
                "LA3146",
                $"Local variable '{name}' is read before it is assigned.",
                span,
                new StaticDiagnosticProof("binding", name, "function-local name is read before any assignment path"));
        }
    }

    private static void AnalyzeAugmentedAssignmentTarget(
        AssignmentTargetSyntax target,
        StaticAnalysisContext context,
        HashSet<string> localNames,
        HashSet<string> maybeAssigned)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax nameTarget:
                AnalyzeLocalRead(nameTarget.Name, target.Span, context, localNames, maybeAssigned);
                break;

            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeExpression(subscript.Target, context, localNames, maybeAssigned);
                AnalyzeExpression(subscript.Index, context, localNames, maybeAssigned);
                break;

            case SliceAssignmentTargetSyntax slice:
                AnalyzeExpression(slice.Target, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.Start, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.End, context, localNames, maybeAssigned);
                AnalyzeExpressionIfPresent(slice.Step, context, localNames, maybeAssigned);
                break;

            case MemberAssignmentTargetSyntax member:
                AnalyzeExpression(member.Target, context, localNames, maybeAssigned);
                break;
        }
    }

    private static void CollectLocalAssignments(IReadOnlyList<StatementSyntax> statements, HashSet<string> localNames)
    {
        foreach (var statement in statements)
        {
            CollectLocalAssignments(statement, localNames);
        }
    }

    private static void CollectLocalAssignments(StatementSyntax statement, HashSet<string> localNames)
    {
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                localNames.Add(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var memberName in EnumerateImportedBindingNames(importStatement))
                    {
                        localNames.Add(memberName);
                    }
                }
                break;
            case AssignmentStatementSyntax assignment:
                localNames.Add(assignment.Name);
                CollectLocalAssignments(assignment.Expression, localNames);
                break;
            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets) CollectAssignmentTargetNames(target, localNames);
                CollectLocalAssignments(chained.Expression, localNames);
                break;
            case AnnotatedAssignmentStatementSyntax annotated:
                localNames.Add(annotated.Name);
                if (annotated.Expression is not null) CollectLocalAssignments(annotated.Expression, localNames);
                break;
            case AugmentedAssignmentStatementSyntax augmented:
                if (augmented.Target is NameAssignmentTargetSyntax augmentedName)
                {
                    localNames.Add(augmentedName.Name);
                }
                CollectLocalAssignments(augmented.Target, localNames);
                CollectLocalAssignments(augmented.Expression, localNames);
                break;
            case UnpackingAssignmentStatementSyntax unpacking:
                foreach (var target in unpacking.Targets) localNames.Add(target.Name);
                CollectLocalAssignments(unpacking.Expression, localNames);
                break;
            case WithStatementSyntax withStatement:
                if (withStatement.VariableName is not null) localNames.Add(withStatement.VariableName);
                CollectLocalAssignments(withStatement.ContextExpression, localNames);
                CollectLocalAssignments(withStatement.Body, localNames);
                break;
            case ForStatementSyntax forStatement:
                CollectLoopTargetNames(forStatement.Target, localNames);
                CollectLocalAssignments(forStatement.Iterable, localNames);
                CollectLocalAssignments(forStatement.Body, localNames);
                if (forStatement.ElseStatements is not null) CollectLocalAssignments(forStatement.ElseStatements, localNames);
                break;
            case WhileStatementSyntax whileStatement:
                CollectLocalAssignments(whileStatement.Condition, localNames);
                CollectLocalAssignments(whileStatement.Body, localNames);
                if (whileStatement.ElseStatements is not null) CollectLocalAssignments(whileStatement.ElseStatements, localNames);
                break;
            case IfStatementSyntax ifStatement:
                CollectLocalAssignments(ifStatement.Condition, localNames);
                CollectLocalAssignments(ifStatement.ThenStatements, localNames);
                if (ifStatement.ElseStatements is not null) CollectLocalAssignments(ifStatement.ElseStatements, localNames);
                break;
            case FunctionDefinitionStatementSyntax functionDefinition:
                localNames.Add(functionDefinition.Name);
                break;
            case ClassDefinitionStatementSyntax classDefinition:
                localNames.Add(classDefinition.Name);
                break;
            case ExpressionStatementSyntax expressionStatement:
                CollectLocalAssignments(expressionStatement.Expression, localNames);
                break;
            case SubscriptAssignmentStatementSyntax subscript:
                CollectLocalAssignments(subscript.Target, localNames);
                CollectLocalAssignments(subscript.Index, localNames);
                CollectLocalAssignments(subscript.Expression, localNames);
                break;
            case SliceAssignmentStatementSyntax slice:
                CollectLocalAssignments(slice.Target, localNames);
                if (slice.Start is not null) CollectLocalAssignments(slice.Start, localNames);
                if (slice.End is not null) CollectLocalAssignments(slice.End, localNames);
                if (slice.Step is not null) CollectLocalAssignments(slice.Step, localNames);
                CollectLocalAssignments(slice.Expression, localNames);
                break;
            case MemberAssignmentStatementSyntax member:
                CollectLocalAssignments(member.Target, localNames);
                CollectLocalAssignments(member.Expression, localNames);
                break;
            case MatchStatementSyntax matchStatement:
                CollectLocalAssignments(matchStatement.Subject, localNames);
                foreach (var matchCase in matchStatement.Cases)
                {
                    if (matchCase.Guard is not null) CollectLocalAssignments(matchCase.Guard, localNames);
                    CollectLocalAssignments(matchCase.Body, localNames);
                }
                break;
            case AssertStatementSyntax assertStatement:
                CollectLocalAssignments(assertStatement.Condition, localNames);
                if (assertStatement.Message is not null) CollectLocalAssignments(assertStatement.Message, localNames);
                break;
            case DeleteStatementSyntax deleteStatement:
                CollectLocalAssignments(deleteStatement.Target, localNames);
                break;
            case ReturnStatementSyntax { Expression: { } expression }:
                CollectLocalAssignments(expression, localNames);
                break;
            case RaiseStatementSyntax raiseStatement:
                CollectLocalAssignments(raiseStatement.Expression, localNames);
                break;
            case TryStatementSyntax tryStatement:
                CollectLocalAssignments(tryStatement.TryBody, localNames);
                if (tryStatement.ExceptBody is not null) CollectLocalAssignments(tryStatement.ExceptBody, localNames);
                if (tryStatement.ElseBody is not null) CollectLocalAssignments(tryStatement.ElseBody, localNames);
                if (tryStatement.FinallyBody is not null) CollectLocalAssignments(tryStatement.FinallyBody, localNames);
                break;
        }
    }

    private static IEnumerable<string> EnumerateImportedBindingNames(ImportStatementSyntax importStatement)
    {
        if (importStatement.ImportedMembers is null)
        {
            yield break;
        }

        if (importStatement.ImportedMembers.Count == 1 && importStatement.ImportedMembers[0].Name == "*")
        {
            foreach (var memberName in StaticContracts.GetModuleExportedMemberNames(importStatement.ModuleName))
            {
                yield return memberName;
            }

            yield break;
        }

        foreach (var member in importStatement.ImportedMembers)
        {
            yield return member.BindingName;
        }
    }

    private static void CollectLocalAssignments(AssignmentTargetSyntax target, HashSet<string> localNames)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                CollectLocalAssignments(subscript.Target, localNames);
                CollectLocalAssignments(subscript.Index, localNames);
                break;

            case SliceAssignmentTargetSyntax slice:
                CollectLocalAssignments(slice.Target, localNames);
                if (slice.Start is not null) CollectLocalAssignments(slice.Start, localNames);
                if (slice.End is not null) CollectLocalAssignments(slice.End, localNames);
                if (slice.Step is not null) CollectLocalAssignments(slice.Step, localNames);
                break;

            case MemberAssignmentTargetSyntax member:
                CollectLocalAssignments(member.Target, localNames);
                break;
        }
    }

    private static void CollectLocalAssignments(ExpressionSyntax expression, HashSet<string> localNames)
    {
        switch (expression)
        {
            case AssignmentExpressionSyntax assignment:
                localNames.Add(assignment.Name);
                CollectLocalAssignments(assignment.Expression, localNames);
                break;
            case FormattedStringExpressionSyntax formatted:
                foreach (var nestedExpression in FormattedStringSyntaxTraversal.EnumerateExpressions(formatted.Parts))
                {
                    CollectLocalAssignments(nestedExpression, localNames);
                }
                break;
            case ListLiteralExpressionSyntax list:
                foreach (var item in list.Items) CollectLocalAssignments(item, localNames);
                break;
            case ListComprehensionExpressionSyntax listComprehension:
                CollectLocalAssignments(listComprehension.ItemExpression, localNames);
                foreach (var clause in listComprehension.Clauses)
                {
                    CollectLoopTargetNames(clause.Target, localNames);
                    CollectLocalAssignments(clause.Iterable, localNames);
                    if (clause.Condition is not null) CollectLocalAssignments(clause.Condition, localNames);
                }
                break;
            case GeneratorExpressionSyntax generator:
                CollectLocalAssignments(generator.ItemExpression, localNames);
                foreach (var clause in generator.Clauses)
                {
                    CollectLoopTargetNames(clause.Target, localNames);
                    CollectLocalAssignments(clause.Iterable, localNames);
                    if (clause.Condition is not null) CollectLocalAssignments(clause.Condition, localNames);
                }
                break;
            case DictLiteralExpressionSyntax dict:
                foreach (var item in dict.Items)
                {
                    CollectLocalAssignments(item.Key, localNames);
                    if (!item.IsUnpacking)
                    {
                        CollectLocalAssignments(item.Value, localNames);
                    }
                }
                break;
            case SetLiteralExpressionSyntax set:
                foreach (var item in set.Items) CollectLocalAssignments(item, localNames);
                break;
            case SetComprehensionExpressionSyntax setComprehension:
                CollectLocalAssignments(setComprehension.ItemExpression, localNames);
                foreach (var clause in setComprehension.Clauses)
                {
                    CollectLoopTargetNames(clause.Target, localNames);
                    CollectLocalAssignments(clause.Iterable, localNames);
                    if (clause.Condition is not null) CollectLocalAssignments(clause.Condition, localNames);
                }
                break;
            case DictComprehensionExpressionSyntax dictComprehension:
                CollectLocalAssignments(dictComprehension.KeyExpression, localNames);
                CollectLocalAssignments(dictComprehension.ValueExpression, localNames);
                foreach (var clause in dictComprehension.Clauses)
                {
                    CollectLoopTargetNames(clause.Target, localNames);
                    CollectLocalAssignments(clause.Iterable, localNames);
                    if (clause.Condition is not null) CollectLocalAssignments(clause.Condition, localNames);
                }
                break;
            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items) CollectLocalAssignments(item, localNames);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                CollectLocalAssignments(parenthesized.Inner, localNames);
                break;
            case MemberExpressionSyntax member:
                CollectLocalAssignments(member.Target, localNames);
                break;
            case CallExpressionSyntax call:
                CollectLocalAssignments(call.Target, localNames);
                foreach (var argument in call.Arguments) CollectLocalAssignments(argument.Expression, localNames);
                break;
            case SubscriptExpressionSyntax subscript:
                CollectLocalAssignments(subscript.Target, localNames);
                CollectLocalAssignments(subscript.Index, localNames);
                break;
            case SliceExpressionSyntax slice:
                CollectLocalAssignments(slice.Target, localNames);
                if (slice.Start is not null) CollectLocalAssignments(slice.Start, localNames);
                if (slice.End is not null) CollectLocalAssignments(slice.End, localNames);
                if (slice.Step is not null) CollectLocalAssignments(slice.Step, localNames);
                break;
            case BinaryExpressionSyntax binary:
                CollectLocalAssignments(binary.Left, localNames);
                CollectLocalAssignments(binary.Right, localNames);
                break;
            case ChainedComparisonExpressionSyntax chained:
                foreach (var operand in chained.Operands) CollectLocalAssignments(operand, localNames);
                break;
            case UnaryExpressionSyntax unary:
                CollectLocalAssignments(unary.Operand, localNames);
                break;
            case ConditionalExpressionSyntax conditional:
                CollectLocalAssignments(conditional.Condition, localNames);
                CollectLocalAssignments(conditional.Consequent, localNames);
                CollectLocalAssignments(conditional.Alternative, localNames);
                break;
        }
    }

    private static void AnalyzeExpressionForNestedFunctions(ExpressionSyntax expression, StaticAnalysisContext context)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            var localNames = new HashSet<string>(StringComparer.Ordinal);
            CollectLocalAssignments(lambda.Body, localNames);
            var maybeAssigned = new HashSet<string>(
                lambda.Parameters.Select(static parameter => parameter.Name),
                StringComparer.Ordinal);
            AnalyzeExpression(lambda.Body, context, localNames, maybeAssigned);
        }
    }

    private static void AddAssignmentTarget(AssignmentTargetSyntax target, HashSet<string> maybeAssigned)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax nameTarget:
                maybeAssigned.Add(nameTarget.Name);
                break;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                foreach (var nested in unpacking.Targets)
                {
                    maybeAssigned.Add(nested.Name);
                }
                break;
        }
    }

    private static void CollectAssignmentTargetNames(AssignmentTargetSyntax target, HashSet<string> localNames)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax nameTarget:
                localNames.Add(nameTarget.Name);
                break;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                foreach (var nested in unpacking.Targets)
                {
                    localNames.Add(nested.Name);
                }
                break;
        }
    }

    private static void AddLoopTarget(LoopTargetSyntax target, HashSet<string> maybeAssigned)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                maybeAssigned.Add(nameTarget.Name);
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items) AddLoopTarget(item, maybeAssigned);
                break;
        }
    }

    private static void CollectLoopTargetNames(LoopTargetSyntax target, HashSet<string> localNames)
    {
        switch (target)
        {
            case LoopNameTargetSyntax nameTarget:
                localNames.Add(nameTarget.Name);
                break;
            case LoopTupleTargetSyntax tupleTarget:
                foreach (var item in tupleTarget.Items) CollectLoopTargetNames(item, localNames);
                break;
        }
    }

    private static HashSet<string> Clone(HashSet<string> names)
        => new(names, StringComparer.Ordinal);
}
