using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed class LoweredScript
{
    private LoweredScript(
        ScriptSyntax syntax,
        IReadOnlyList<LoweredStatement> statements)
    {
        Syntax = syntax;
        Statements = statements;
    }

    public ScriptSyntax Syntax { get; }

    public IReadOnlyList<LoweredStatement> Statements { get; }

    public static LoweredScript Lower(ScriptSyntax syntax)
    {
        var statements = LowerStatements(syntax.Statements);
        return new LoweredScript(syntax, statements);
    }

    internal static LoweredExpression LowerStandaloneExpression(ExpressionSyntax expression)
    {
        return LowerExpression(expression);
    }

    private static IReadOnlyList<LoweredStatement> LowerStatements(IReadOnlyList<StatementSyntax> statements, bool classScope = false)
    {
        var lowered = new List<LoweredStatement>(statements.Count);
        foreach (var statement in statements)
        {
            var item = LowerStatement(statement, classScope);
            if (classScope && item is LoweredFunctionDefinitionStatement function)
            {
                var code = ExecutableScript.CompileClassFunction(function);
                item = function with { PreparedCode = code, Body = [] };
            }
            lowered.Add(item);
        }

        return lowered;
    }

    internal static IReadOnlyList<LoweredTypeParameter>? LowerTypeParameters(IReadOnlyList<TypeParameterSyntax>? parameters)
        => parameters?.Select(parameter => new LoweredTypeParameter(parameter, parameter.Bound is null ? null : LowerExpression(parameter.Bound),
            parameter.Default is null ? null : LowerExpression(parameter.Default))).ToArray();

    private static LoweredFunctionDefinitionStatement LowerFunction(FunctionDefinitionStatementSyntax definition)
    {
        var decorators = definition.Decorators.Select(LowerExpression).ToArray();
        var defaults = LowerDefaults(definition.Parameters, definition.ParameterLayout);
        return new(definition, decorators, defaults, LowerFunctionAnnotations(definition),
            LowerStatements(definition.Body), ScopeDirectiveFactsCollector.ForFunction(definition));
    }

    // The shared layout owns names, kinds and binding order. Lower only the
    // expressions that execute in the enclosing scope, in layout order.
    internal static LoweredExpression[] LowerDefaults(IReadOnlyList<FunctionParameterSyntax> parameters,
        FunctionParameterLayout layout)
    {
        if (layout.Defaults.Length == 0) return [];
        var expressions = new LoweredExpression[layout.Defaults.Length];
        for (var i = 0; i < expressions.Length; i++)
            expressions[i] = LowerExpression(parameters[layout.Defaults[i].ParameterIndex].DefaultValue.RequireNotNull());
        return expressions;
    }

    private static LoweredFunctionAnnotation[] LowerFunctionAnnotations(FunctionDefinitionStatementSyntax definition)
    {
        var count = definition.ReturnAnnotation is null ? 0 : 1;
        var positionalCount = 0;
        var positionalOnlyCount = 0;
        foreach (var parameter in definition.Parameters)
        {
            if (parameter.Annotation is null) continue;
            count++;
            if (parameter.Kind == FunctionParameterKind.Positional) positionalCount++;
            else if (parameter.Kind == FunctionParameterKind.PositionalOnly) positionalOnlyCount++;
        }
        if (count == 0) return [];

        // Exchange the two positional groups for Python's annotation order.
        // The remaining parameter kinds already appear in evaluation order.
        var annotations = new LoweredFunctionAnnotation[count];
        var nextPositional = 0;
        var nextPositionalOnly = positionalCount;
        var nextOther = positionalCount + positionalOnlyCount;
        foreach (var parameter in definition.Parameters)
        {
            if (parameter.Annotation is not { } expression) continue;
            var index = parameter.Kind switch
            {
                FunctionParameterKind.Positional => nextPositional++,
                FunctionParameterKind.PositionalOnly => nextPositionalOnly++,
                _ => nextOther++,
            };
            annotations[index] = new(PyString.FromString(parameter.Name), LowerExpression(expression));
        }
        if (definition.ReturnAnnotation is { } result) annotations[^1] = new(PyString.FromString("return"), LowerExpression(result));
        return annotations;
    }

    private static LoweredStatement LowerStatement(StatementSyntax statement, bool classScope)
    {
        return statement switch
        {
            TypeAliasStatementSyntax alias => new LoweredTypeAliasStatement(alias, LowerExpression(alias.Value)),
            ImportStatementSyntax importStatement => new LoweredImportStatement(importStatement),
            ScopeDirectiveStatementSyntax scopeDirective => new LoweredScopeDirectiveStatement(scopeDirective),
            FunctionDefinitionStatementSyntax functionDefinition => LowerFunction(functionDefinition),
            ClassDefinitionStatementSyntax classDefinition => new LoweredClassDefinitionStatement(
                classDefinition,
                classDefinition.Decorators.Select(LowerExpression).ToArray(),
                classDefinition.HeaderArguments.Select(argument =>
                    new LoweredCallArgument(argument.Form, LowerExpression(argument.Expression))).ToArray(),
                LowerStatements(classDefinition.Body, classScope: true)),
            AssignmentStatementSyntax assignment
                => new LoweredNameAssignmentStatement(
                    assignment,
                    LowerExpression(assignment.Expression)),
            ChainedAssignmentStatementSyntax chainedAssignment
                => new LoweredChainedAssignmentStatement(
                    chainedAssignment,
                    LowerExpression(chainedAssignment.Expression)),
            AnnotatedAssignmentStatementSyntax annotatedAssignment
                => new LoweredAnnotatedAssignmentStatement(
                    annotatedAssignment,
                    LowerExpression(annotatedAssignment.Annotation),
                    annotatedAssignment.Expression is null ? null : LowerExpression(annotatedAssignment.Expression)),
            AugmentedAssignmentStatementSyntax augmentedAssignment
                => new LoweredAugmentedAssignmentStatement(
                    augmentedAssignment,
                    LoweredStoreTarget.FromSyntax(augmentedAssignment.Target),
                    LowerExpression(augmentedAssignment.Expression)),
            UnpackingAssignmentStatementSyntax unpackingAssignment
                => new LoweredUnpackingAssignmentStatement(
                    unpackingAssignment,
                    LowerExpression(unpackingAssignment.Expression)),
            SubscriptAssignmentStatementSyntax subscriptAssignment
                => new LoweredSubscriptAssignmentStatement(
                    subscriptAssignment,
                    LowerExpression(subscriptAssignment.Target),
                    LowerExpression(subscriptAssignment.Index),
                    LowerExpression(subscriptAssignment.Expression)),
            SliceAssignmentStatementSyntax sliceAssignment
                => new LoweredSliceAssignmentStatement(
                    sliceAssignment,
                    LowerExpression(sliceAssignment.Target),
                    sliceAssignment.Start is null ? null : LowerExpression(sliceAssignment.Start),
                    sliceAssignment.End is null ? null : LowerExpression(sliceAssignment.End),
                    sliceAssignment.Step is null ? null : LowerExpression(sliceAssignment.Step),
                    LowerExpression(sliceAssignment.Expression)),
            MemberAssignmentStatementSyntax memberAssignment
                => new LoweredMemberAssignmentStatement(
                    memberAssignment,
                    LowerExpression(memberAssignment.Target),
                    LowerExpression(memberAssignment.Expression)),
            ExpressionStatementSyntax expressionStatement => new LoweredExpressionStatement(expressionStatement, LowerExpression(expressionStatement.Expression)),
            IfStatementSyntax ifStatement => new LoweredIfStatement(
                ifStatement,
                LowerExpression(ifStatement.Condition),
                LowerStatements(ifStatement.ThenStatements, classScope),
                ifStatement.ElseStatements is null ? null : LowerStatements(ifStatement.ElseStatements, classScope)),
            ForStatementSyntax forStatement => new LoweredForStatement(
                forStatement,
                LowerExpression(forStatement.Iterable),
                forStatement.ElseStatements is null ? null : LowerStatements(forStatement.ElseStatements, classScope),
                LowerStatements(forStatement.Body, classScope)),
            WhileStatementSyntax whileStatement => new LoweredWhileStatement(
                whileStatement,
                LowerExpression(whileStatement.Condition),
                whileStatement.ElseStatements is null ? null : LowerStatements(whileStatement.ElseStatements, classScope),
                LowerStatements(whileStatement.Body, classScope)),
            MatchStatementSyntax matchStatement => new LoweredMatchStatement(
                matchStatement,
                LowerExpression(matchStatement.Subject),
                matchStatement.Cases.Select(matchCase => new LoweredMatchCase(
                    matchCase,
                    LoweredPatternPreparation.Prepare(matchCase.Pattern),
                    LowerStatements(matchCase.Body, classScope),
                    matchCase.Guard is null ? null : LowerExpression(matchCase.Guard))).ToArray()),
            WithStatementSyntax withStatement => new LoweredWithStatement(
                withStatement,
                LowerExpression(withStatement.ContextExpression),
                LowerStatements(withStatement.Body, classScope)),
            TryStatementSyntax tryStatement => new LoweredTryStatement(
                tryStatement,
                LowerStatements(tryStatement.TryBody, classScope),
                tryStatement.ExceptClauses.Select(clause => new LoweredExceptClause(clause, LowerStatements(clause.Body, classScope),
                    clause.ExceptionTypeExpression is null ? null : LowerExpression(clause.ExceptionTypeExpression))).ToArray(),
                tryStatement.ElseBody is null ? null : LowerStatements(tryStatement.ElseBody, classScope),
                tryStatement.FinallyBody is null ? null : LowerStatements(tryStatement.FinallyBody, classScope)),
            PassStatementSyntax passStatement => new LoweredPassStatement(passStatement),
            BreakStatementSyntax breakStatement => new LoweredBreakStatement(breakStatement),
            ContinueStatementSyntax continueStatement => new LoweredContinueStatement(continueStatement),
            AssertStatementSyntax assertStatement => new LoweredAssertStatement(
                assertStatement,
                LowerExpression(assertStatement.Condition),
                assertStatement.Message is null ? null : LowerExpression(assertStatement.Message)),
            DeleteStatementSyntax deleteStatement => new LoweredDeleteStatement(
                deleteStatement,
                LowerExpression(deleteStatement.Target)),
            ReturnStatementSyntax returnStatement => new LoweredReturnStatement(
                returnStatement,
                returnStatement.Expression is null ? null : LowerExpression(returnStatement.Expression)),
            RaiseStatementSyntax raiseStatement => new LoweredRaiseStatement(
                raiseStatement,
                raiseStatement.Expression is null ? null : LowerExpression(raiseStatement.Expression),
                raiseStatement.CauseExpression is null ? null : LowerExpression(raiseStatement.CauseExpression)),
            _ => new LoweredOtherStatement(statement)
        };
    }

    internal static LoweredExpression LowerExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            UnpackedTypeExpressionSyntax unpacked => new LoweredUnpackedTypeExpression(unpacked, LowerExpression(unpacked.Value)),
            YieldExpressionSyntax yielded => new LoweredYieldExpression(yielded, yielded.Value is null ? null : LowerExpression(yielded.Value)),
            IdentifierExpressionSyntax identifier => new LoweredIdentifierExpression(identifier),
            StringLiteralExpressionSyntax text => new LoweredStringLiteralExpression(text),
            BytesLiteralExpressionSyntax bytes => new LoweredBytesLiteralExpression(bytes),
            IntegerLiteralExpressionSyntax integer => new LoweredIntegerLiteralExpression(integer),
            ImaginaryLiteralExpressionSyntax imaginary => new LoweredImaginaryLiteralExpression(imaginary),
            FloatLiteralExpressionSyntax floating => new LoweredFloatLiteralExpression(floating),
            BooleanLiteralExpressionSyntax boolean => new LoweredBooleanLiteralExpression(boolean),
            NoneLiteralExpressionSyntax none => new LoweredNoneLiteralExpression(none),
            EllipsisLiteralExpressionSyntax ellipsis => new LoweredEllipsisLiteralExpression(ellipsis),
            FormattedStringExpressionSyntax formatted => new LoweredFormattedStringExpression(
                formatted,
                formatted.Parts.Select(LowerFormattedStringPart).ToArray()),
            ParenthesizedExpressionSyntax parenthesized => new LoweredParenthesizedExpression(
                parenthesized,
                LowerExpression(parenthesized.Inner)),
            ListLiteralExpressionSyntax list => new LoweredListLiteralExpression(
                list,
                list.Items.Select(LowerCollectionDisplayItem).ToArray()),
            ListComprehensionExpressionSyntax comprehension => new LoweredListComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.ItemExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            GeneratorExpressionSyntax generator => generator.PreparedCallable ?? new LoweredGeneratorExpression(
                generator,
                LowerExpression(generator.ItemExpression),
                generator.Clauses.Select(LowerComprehensionClause).ToArray()),
            TupleLiteralExpressionSyntax tuple => new LoweredTupleLiteralExpression(
                tuple,
                tuple.Items.Select(LowerCollectionDisplayItem).ToArray()),
            SetLiteralExpressionSyntax set => new LoweredSetLiteralExpression(
                set,
                set.Items.Select(LowerCollectionDisplayItem).ToArray()),
            SetComprehensionExpressionSyntax comprehension => new LoweredSetComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.ItemExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            DictLiteralExpressionSyntax dict => new LoweredDictLiteralExpression(
                dict,
                dict.Items.Select(item => item switch
                {
                    DictionaryKeyValueItemSyntax pair => (LoweredDictionaryDisplayItem)new LoweredDictionaryKeyValueItem(
                        pair,
                        LowerExpression(pair.Key),
                        LowerExpression(pair.Value)),
                    DictionaryUnpackingItemSyntax unpacking => new LoweredDictionaryUnpackingItem(
                        unpacking,
                        LowerExpression(unpacking.Mapping)),
                    _ => throw new InvalidOperationException($"Unknown dictionary display item: {item.GetType().Name}")
                }).ToArray()),
            DictComprehensionExpressionSyntax comprehension => new LoweredDictComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.KeyExpression),
                LowerExpression(comprehension.ValueExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            MemberExpressionSyntax member => new LoweredMemberExpression(
                member,
                LowerExpression(member.Target)),
            CallExpressionSyntax call => new LoweredCallExpression(
                call,
                LowerExpression(call.Target),
                call.Arguments.Select(argument => new LoweredCallArgument(
                    argument.Form,
                    LowerExpression(argument.Expression))).ToArray()),
            SubscriptExpressionSyntax subscript => new LoweredSubscriptExpression(
                subscript,
                LowerExpression(subscript.Target),
                LowerExpression(subscript.Index)),
            SliceExpressionSyntax slice => new LoweredSliceExpression(
                slice,
                LowerExpression(slice.Target),
                slice.Start is null ? null : LowerExpression(slice.Start),
                slice.End is null ? null : LowerExpression(slice.End),
                slice.Step is null ? null : LowerExpression(slice.Step)),
            SliceValueExpressionSyntax slice => new LoweredSliceValueExpression(
                slice,
                slice.Start is null ? null : LowerExpression(slice.Start),
                slice.End is null ? null : LowerExpression(slice.End),
                slice.Step is null ? null : LowerExpression(slice.Step)),
            BinaryExpressionSyntax binary => new LoweredBinaryExpression(
                binary,
                LowerExpression(binary.Left),
                LowerExpression(binary.Right)),
            ChainedComparisonExpressionSyntax chained => new LoweredChainedComparisonExpression(
                chained,
                chained.Operands.Select(LowerExpression).ToArray()),
            UnaryExpressionSyntax unary => new LoweredUnaryExpression(
                unary,
                LowerExpression(unary.Operand)),
            ConditionalExpressionSyntax conditional => new LoweredConditionalExpression(
                conditional,
                LowerExpression(conditional.Consequent),
                LowerExpression(conditional.Condition),
                LowerExpression(conditional.Alternative)),
            AssignmentExpressionSyntax assignment => new LoweredAssignmentExpression(
                assignment,
                LowerExpression(assignment.Expression)),
            LambdaExpressionSyntax lambda => lambda.PreparedCallable ?? new LoweredLambdaExpression(
                lambda,
                LowerExpression(lambda.Body)),
            _ => throw new InvalidOperationException($"Unknown expression during lowering: {expression.GetType().Name}")
        };
    }

    private static LoweredCollectionDisplayItem LowerCollectionDisplayItem(CollectionDisplayItemSyntax item)
        => new(item, LowerExpression(item.Expression));

    private static LoweredFormattedStringPart LowerFormattedStringPart(FormattedStringPartSyntax part)
    {
        return part switch
        {
            FormattedStringTextPartSyntax text => new LoweredFormattedStringTextPart(text.Text),
            FormattedStringExpressionPartSyntax expression => new LoweredFormattedStringExpressionPart(
                LowerExpression(expression.Expression),
                expression.Conversion,
                expression.FormatSpecifier,
                expression.FormatSpecifierParts?.Select(LowerFormattedStringPart).ToArray()),
            _ => throw new InvalidOperationException($"Unknown formatted string part: {part.GetType().Name}")
        };
    }

    private static LoweredComprehensionClause LowerComprehensionClause(ComprehensionClauseSyntax clause)
    {
        return new LoweredComprehensionClause(
            clause.Target,
            LowerExpression(clause.Iterable),
            clause.Condition is null ? null : LowerExpression(clause.Condition),
            clause.Span);
    }
}
