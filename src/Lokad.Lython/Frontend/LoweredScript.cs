namespace Lokad.Lython.Frontend;

internal sealed class LoweredScript
{
    private LoweredScript(
        ScriptSyntax syntax,
        IReadOnlyList<LoweredStatement> statements,
        IReadOnlyList<LoweredImportStatement> topLevelImports,
        IReadOnlyList<LoweredFunctionDefinitionStatement> topLevelFunctions)
    {
        Syntax = syntax;
        Statements = statements;
        TopLevelImports = topLevelImports;
        TopLevelFunctions = topLevelFunctions;
    }

    public ScriptSyntax Syntax { get; }

    public IReadOnlyList<LoweredStatement> Statements { get; }

    public IReadOnlyList<LoweredImportStatement> TopLevelImports { get; }

    public IReadOnlyList<LoweredFunctionDefinitionStatement> TopLevelFunctions { get; }

    public static LoweredScript Lower(ScriptSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);

        var statements = LowerStatements(syntax.Statements);
        var imports = statements.OfType<LoweredImportStatement>().ToArray();
        var functions = statements.OfType<LoweredFunctionDefinitionStatement>().ToArray();

        return new LoweredScript(
            syntax,
            statements,
            imports,
            functions);
    }

    internal static LoweredExpression LowerStandaloneExpression(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return LowerExpression(expression);
    }

    private static IReadOnlyList<LoweredStatement> LowerStatements(IReadOnlyList<StatementSyntax> statements)
    {
        var lowered = new List<LoweredStatement>(statements.Count);
        foreach (var statement in statements)
        {
            lowered.Add(LowerStatement(statement));
        }

        return lowered;
    }

    private static LoweredStatement LowerStatement(StatementSyntax statement)
    {
        return statement switch
        {
            ImportStatementSyntax importStatement => new LoweredImportStatement(importStatement),
            FunctionDefinitionStatementSyntax functionDefinition => new LoweredFunctionDefinitionStatement(
                functionDefinition,
                functionDefinition.Decorators.Select(LowerExpression).ToArray(),
                functionDefinition.Parameters.Select(parameter => new LoweredFunctionParameter(
                    parameter.Name,
                    parameter.Kind,
                    parameter.Annotation is null ? null : LowerExpression(parameter.Annotation),
                    parameter.DefaultValue is null ? null : LowerExpression(parameter.DefaultValue))).ToArray(),
                functionDefinition.ReturnAnnotation is null ? null : LowerExpression(functionDefinition.ReturnAnnotation),
                LowerStatements(functionDefinition.Body)),
            ClassDefinitionStatementSyntax classDefinition => new LoweredClassDefinitionStatement(
                classDefinition,
                classDefinition.Decorators.Select(LowerExpression).ToArray(),
                classDefinition.Bases.Select(LowerExpression).ToArray(),
                classDefinition.KeywordArguments.Select(argument => new LoweredCallArgument(argument.Name, LowerExpression(argument.Value))).ToArray(),
                LowerStatements(classDefinition.Body)),
            AssignmentStatementSyntax assignment
                => new LoweredAssignmentStatement(
                    assignment,
                    LowerExpression(assignment.Expression)),
            ChainedAssignmentStatementSyntax chainedAssignment
                => new LoweredAssignmentStatement(
                    chainedAssignment,
                    LowerExpression(chainedAssignment.Expression)),
            AnnotatedAssignmentStatementSyntax annotatedAssignment
                => new LoweredAssignmentStatement(
                    annotatedAssignment,
                    annotatedAssignment.Expression is null ? null : LowerExpression(annotatedAssignment.Expression),
                    LowerExpression(annotatedAssignment.Annotation)),
            AugmentedAssignmentStatementSyntax augmentedAssignment
                => new LoweredAssignmentStatement(
                    augmentedAssignment,
                    LowerExpression(augmentedAssignment.Expression),
                    Target: augmentedAssignment.Target switch
                    {
                        SubscriptAssignmentTargetSyntax subscript => LowerExpression(subscript.Target),
                        SliceAssignmentTargetSyntax slice => LowerExpression(slice.Target),
                        MemberAssignmentTargetSyntax member => LowerExpression(member.Target),
                        _ => null
                    },
                    Index: augmentedAssignment.Target is SubscriptAssignmentTargetSyntax subscriptTarget
                        ? LowerExpression(subscriptTarget.Index)
                        : null,
                    Start: augmentedAssignment.Target is SliceAssignmentTargetSyntax sliceTarget && sliceTarget.Start is not null
                        ? LowerExpression(sliceTarget.Start)
                        : null,
                    End: augmentedAssignment.Target is SliceAssignmentTargetSyntax sliceTargetForEnd && sliceTargetForEnd.End is not null
                        ? LowerExpression(sliceTargetForEnd.End)
                        : null,
                    Step: augmentedAssignment.Target is SliceAssignmentTargetSyntax sliceTargetForStep && sliceTargetForStep.Step is not null
                        ? LowerExpression(sliceTargetForStep.Step)
                        : null,
                    MemberName: augmentedAssignment.Target is MemberAssignmentTargetSyntax memberTarget
                        ? memberTarget.MemberName
                        : null),
            UnpackingAssignmentStatementSyntax unpackingAssignment
                => new LoweredAssignmentStatement(
                    unpackingAssignment,
                    LowerExpression(unpackingAssignment.Expression)),
            SubscriptAssignmentStatementSyntax subscriptAssignment
                => new LoweredAssignmentStatement(
                    subscriptAssignment,
                    LowerExpression(subscriptAssignment.Expression),
                    Target: LowerExpression(subscriptAssignment.Target),
                    Index: LowerExpression(subscriptAssignment.Index)),
            SliceAssignmentStatementSyntax sliceAssignment
                => new LoweredAssignmentStatement(
                    sliceAssignment,
                    LowerExpression(sliceAssignment.Expression),
                    Target: LowerExpression(sliceAssignment.Target),
                    Start: sliceAssignment.Start is null ? null : LowerExpression(sliceAssignment.Start),
                    End: sliceAssignment.End is null ? null : LowerExpression(sliceAssignment.End),
                    Step: sliceAssignment.Step is null ? null : LowerExpression(sliceAssignment.Step)),
            MemberAssignmentStatementSyntax memberAssignment
                => new LoweredAssignmentStatement(
                    memberAssignment,
                    LowerExpression(memberAssignment.Expression),
                    Target: LowerExpression(memberAssignment.Target),
                    MemberName: memberAssignment.MemberName),
            ExpressionStatementSyntax expressionStatement => new LoweredExpressionStatement(expressionStatement, LowerExpression(expressionStatement.Expression)),
            IfStatementSyntax ifStatement => new LoweredIfStatement(
                ifStatement,
                LowerExpression(ifStatement.Condition),
                LowerStatements(ifStatement.ThenStatements),
                ifStatement.ElseStatements is null ? null : LowerStatements(ifStatement.ElseStatements)),
            ForStatementSyntax forStatement => new LoweredForStatement(
                forStatement,
                LowerExpression(forStatement.Iterable),
                forStatement.ElseStatements is null ? null : LowerStatements(forStatement.ElseStatements),
                LowerStatements(forStatement.Body)),
            WhileStatementSyntax whileStatement => new LoweredWhileStatement(
                whileStatement,
                LowerExpression(whileStatement.Condition),
                whileStatement.ElseStatements is null ? null : LowerStatements(whileStatement.ElseStatements),
                LowerStatements(whileStatement.Body)),
            MatchStatementSyntax matchStatement => new LoweredMatchStatement(
                matchStatement,
                LowerExpression(matchStatement.Subject),
                matchStatement.Cases.Select(matchCase => new LoweredMatchCase(
                    matchCase,
                    LowerStatements(matchCase.Body))).ToArray()),
            WithStatementSyntax withStatement => new LoweredWithStatement(
                withStatement,
                LowerExpression(withStatement.ContextExpression),
                LowerStatements(withStatement.Body)),
            TryStatementSyntax tryStatement => new LoweredTryStatement(
                tryStatement,
                LowerStatements(tryStatement.TryBody),
                tryStatement.ExceptBody is null ? null : LowerStatements(tryStatement.ExceptBody),
                tryStatement.ElseBody is null ? null : LowerStatements(tryStatement.ElseBody),
                tryStatement.FinallyBody is null ? null : LowerStatements(tryStatement.FinallyBody)),
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
                LowerExpression(raiseStatement.Expression)),
            _ => new LoweredOtherStatement(statement)
        };
    }

    private static LoweredExpression LowerExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierExpressionSyntax identifier => new LoweredIdentifierExpression(identifier),
            StringLiteralExpressionSyntax text => new LoweredStringLiteralExpression(text),
            BytesLiteralExpressionSyntax bytes => new LoweredBytesLiteralExpression(bytes),
            IntegerLiteralExpressionSyntax integer => new LoweredIntegerLiteralExpression(integer),
            FloatLiteralExpressionSyntax floating => new LoweredFloatLiteralExpression(floating),
            BooleanLiteralExpressionSyntax boolean => new LoweredBooleanLiteralExpression(boolean),
            NoneLiteralExpressionSyntax none => new LoweredNoneLiteralExpression(none),
            FormattedStringExpressionSyntax formatted => new LoweredFormattedStringExpression(
                formatted,
                formatted.Parts.Select(LowerFormattedStringPart).ToArray()),
            ParenthesizedExpressionSyntax parenthesized => new LoweredParenthesizedExpression(
                parenthesized,
                LowerExpression(parenthesized.Inner)),
            ListLiteralExpressionSyntax list => new LoweredListLiteralExpression(
                list,
                list.Items.Select(LowerExpression).ToArray()),
            ListComprehensionExpressionSyntax comprehension => new LoweredListComprehensionExpression(
                comprehension,
                LowerExpression(comprehension.ItemExpression),
                comprehension.Clauses.Select(LowerComprehensionClause).ToArray()),
            GeneratorExpressionSyntax generator => new LoweredGeneratorExpression(
                generator,
                LowerExpression(generator.ItemExpression),
                generator.Clauses.Select(LowerComprehensionClause).ToArray()),
            TupleLiteralExpressionSyntax tuple => new LoweredTupleLiteralExpression(
                tuple,
                tuple.Items.Select(LowerExpression).ToArray()),
            SetLiteralExpressionSyntax set => new LoweredSetLiteralExpression(
                set,
                set.Items.Select(LowerExpression).ToArray()),
            DictLiteralExpressionSyntax dict => new LoweredDictLiteralExpression(
                dict,
                dict.Items.Select(item => new KeyValuePair<LoweredExpression, LoweredExpression>(
                    LowerExpression(item.Key),
                    LowerExpression(item.Value))).ToArray()),
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
                    argument.Name,
                    LowerExpression(argument.Expression),
                    argument.Kind)).ToArray()),
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
            LambdaExpressionSyntax lambda => new LoweredLambdaExpression(
                lambda,
                LowerExpression(lambda.Body)),
            _ => new LoweredOtherExpression(expression)
        };
    }

    private static LoweredFormattedStringPart LowerFormattedStringPart(FormattedStringPartSyntax part)
    {
        return part switch
        {
            FormattedStringTextPartSyntax text => new LoweredFormattedStringTextPart(text.Text),
            FormattedStringExpressionPartSyntax expression => new LoweredFormattedStringExpressionPart(
                LowerExpression(expression.Expression),
                expression.Conversion,
                expression.FormatSpecifier),
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
