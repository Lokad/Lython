namespace Lokad.Lython.Frontend;

internal static class StaticRegexDiagnostics
{
    public static void Analyze(StaticAnalysisContext context)
    {
        Analyze(context.Script, context.DiagnosticList);
    }

    private static void Analyze(ScriptSyntax script, List<LythonDiagnostic> diagnostics)
    {
        AnalyzeRegexStaticStatements(
            script.Statements,
            diagnostics,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static void AnalyzeRegexStaticStatements(
        IReadOnlyList<StatementSyntax> statements,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        foreach (var statement in statements)
        {
            AnalyzeRegexStaticStatement(statement, diagnostics, stringBindings, localeFlagBindings);
            UpdateRegexBindings(statement, stringBindings, localeFlagBindings);
        }
    }

    private static void AnalyzeRegexStaticStatement(
        StatementSyntax statement,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax assignment:
                AnalyzeRegexStaticExpression(assignment.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case ChainedAssignmentStatementSyntax chained:
                AnalyzeRegexStaticExpression(chained.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                AnalyzeRegexStaticExpression(annotated.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case SubscriptAssignmentStatementSyntax subscript:
                AnalyzeRegexStaticExpression(subscript.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(subscript.Index, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(subscript.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case SliceAssignmentStatementSyntax slice:
                AnalyzeRegexStaticExpression(slice.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.Start, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.End, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.Step, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(slice.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case MemberAssignmentStatementSyntax member:
                AnalyzeRegexStaticExpression(member.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(member.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case AugmentedAssignmentStatementSyntax augmented:
                AnalyzeRegexStaticAssignmentTarget(augmented.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(augmented.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case UnpackingAssignmentStatementSyntax unpacking:
                AnalyzeRegexStaticExpression(unpacking.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case ExpressionStatementSyntax expressionStatement:
                AnalyzeRegexStaticExpression(expressionStatement.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case WithStatementSyntax withStatement:
                AnalyzeRegexStaticExpression(withStatement.ContextExpression, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexNestedStatements(withStatement.Body, diagnostics, stringBindings, localeFlagBindings);
                break;

            case IfStatementSyntax ifStatement:
                AnalyzeRegexStaticExpression(ifStatement.Condition, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexNestedStatements(ifStatement.ThenStatements, diagnostics, stringBindings, localeFlagBindings);
                if (ifStatement.ElseStatements is not null)
                {
                    AnalyzeRegexNestedStatements(ifStatement.ElseStatements, diagnostics, stringBindings, localeFlagBindings);
                }
                break;

            case ForStatementSyntax forStatement:
                AnalyzeRegexStaticExpression(forStatement.Iterable, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexNestedStatements(forStatement.Body, diagnostics, stringBindings, localeFlagBindings);
                if (forStatement.ElseStatements is not null)
                {
                    AnalyzeRegexNestedStatements(forStatement.ElseStatements, diagnostics, stringBindings, localeFlagBindings);
                }
                break;

            case WhileStatementSyntax whileStatement:
                AnalyzeRegexStaticExpression(whileStatement.Condition, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexNestedStatements(whileStatement.Body, diagnostics, stringBindings, localeFlagBindings);
                if (whileStatement.ElseStatements is not null)
                {
                    AnalyzeRegexNestedStatements(whileStatement.ElseStatements, diagnostics, stringBindings, localeFlagBindings);
                }
                break;

            case MatchStatementSyntax matchStatement:
                AnalyzeRegexStaticExpression(matchStatement.Subject, diagnostics, stringBindings, localeFlagBindings);
                foreach (var matchCase in matchStatement.Cases)
                {
                    if (matchCase.Guard is not null)
                    {
                        AnalyzeRegexStaticExpression(matchCase.Guard, diagnostics, stringBindings, localeFlagBindings);
                    }

                    AnalyzeRegexNestedStatements(matchCase.Body, diagnostics, stringBindings, localeFlagBindings);
                }
                break;

            case AssertStatementSyntax assertStatement:
                AnalyzeRegexStaticExpression(assertStatement.Condition, diagnostics, stringBindings, localeFlagBindings);
                if (assertStatement.Message is not null)
                {
                    AnalyzeRegexStaticExpression(assertStatement.Message, diagnostics, stringBindings, localeFlagBindings);
                }
                break;

            case DeleteStatementSyntax deleteStatement:
                AnalyzeRegexStaticExpression(deleteStatement.Target, diagnostics, stringBindings, localeFlagBindings);
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                foreach (var decorator in functionDefinition.Decorators)
                {
                    AnalyzeRegexStaticExpression(decorator, diagnostics, stringBindings, localeFlagBindings);
                }
                foreach (var parameter in functionDefinition.Parameters)
                {
                    if (parameter.Annotation is not null)
                    {
                        AnalyzeRegexStaticExpression(parameter.Annotation, diagnostics, stringBindings, localeFlagBindings);
                    }

                    if (parameter.DefaultValue is not null)
                    {
                        AnalyzeRegexStaticExpression(parameter.DefaultValue, diagnostics, stringBindings, localeFlagBindings);
                    }
                }

                if (functionDefinition.ReturnAnnotation is not null)
                {
                    AnalyzeRegexStaticExpression(functionDefinition.ReturnAnnotation, diagnostics, stringBindings, localeFlagBindings);
                }

                var functionStringBindings = new Dictionary<string, string>(stringBindings, StringComparer.Ordinal);
                var functionLocaleBindings = new HashSet<string>(localeFlagBindings, StringComparer.Ordinal);
                foreach (var parameter in functionDefinition.Parameters)
                {
                    functionStringBindings.Remove(parameter.Name);
                    functionLocaleBindings.Remove(parameter.Name);
                }

                AnalyzeRegexStaticStatements(functionDefinition.Body, diagnostics, functionStringBindings, functionLocaleBindings);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                foreach (var decorator in classDefinition.Decorators)
                {
                    AnalyzeRegexStaticExpression(decorator, diagnostics, stringBindings, localeFlagBindings);
                }
                foreach (var @base in classDefinition.Bases)
                {
                    AnalyzeRegexStaticExpression(@base, diagnostics, stringBindings, localeFlagBindings);
                }
                foreach (var keywordArgument in classDefinition.KeywordArguments)
                {
                    AnalyzeRegexStaticExpression(keywordArgument.Value, diagnostics, stringBindings, localeFlagBindings);
                }

                AnalyzeRegexNestedStatements(classDefinition.Body, diagnostics, stringBindings, localeFlagBindings);
                break;

            case ReturnStatementSyntax returnStatement when returnStatement.Expression is not null:
                AnalyzeRegexStaticExpression(returnStatement.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case RaiseStatementSyntax raiseStatement:
                AnalyzeRegexStaticExpression(raiseStatement.Expression, diagnostics, stringBindings, localeFlagBindings);
                break;

            case TryStatementSyntax tryStatement:
                AnalyzeRegexNestedStatements(tryStatement.TryBody, diagnostics, stringBindings, localeFlagBindings);
                if (tryStatement.ExceptBody is not null)
                {
                    AnalyzeRegexNestedStatements(tryStatement.ExceptBody, diagnostics, stringBindings, localeFlagBindings);
                }
                if (tryStatement.ElseBody is not null)
                {
                    AnalyzeRegexNestedStatements(tryStatement.ElseBody, diagnostics, stringBindings, localeFlagBindings);
                }
                if (tryStatement.FinallyBody is not null)
                {
                    AnalyzeRegexNestedStatements(tryStatement.FinallyBody, diagnostics, stringBindings, localeFlagBindings);
                }
                break;
        }
    }

    private static void AnalyzeRegexStaticExpressionIfPresent(
        ExpressionSyntax? expression,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        if (expression is not null)
        {
            AnalyzeRegexStaticExpression(expression, diagnostics, stringBindings, localeFlagBindings);
        }
    }

    private static void AnalyzeRegexStaticAssignmentTarget(
        AssignmentTargetSyntax target,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        switch (target)
        {
            case SubscriptAssignmentTargetSyntax subscript:
                AnalyzeRegexStaticExpression(subscript.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpression(subscript.Index, diagnostics, stringBindings, localeFlagBindings);
                break;

            case SliceAssignmentTargetSyntax slice:
                AnalyzeRegexStaticExpression(slice.Target, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.Start, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.End, diagnostics, stringBindings, localeFlagBindings);
                AnalyzeRegexStaticExpressionIfPresent(slice.Step, diagnostics, stringBindings, localeFlagBindings);
                break;

            case MemberAssignmentTargetSyntax member:
                AnalyzeRegexStaticExpression(member.Target, diagnostics, stringBindings, localeFlagBindings);
                break;
        }
    }

    private static void AnalyzeRegexStaticExpression(
        ExpressionSyntax expression,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        if (expression is CallExpressionSyntax call)
        {
            AnalyzeRegexStaticCall(call, diagnostics, stringBindings, localeFlagBindings);
        }

        foreach (var child in ExpressionSyntaxTraversal.EnumerateChildren(expression))
        {
            AnalyzeRegexStaticExpression(child, diagnostics, stringBindings, localeFlagBindings);
        }
    }

    private static void AnalyzeRegexNestedStatements(
        IReadOnlyList<StatementSyntax> statements,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        // A constant learned inside one control-flow branch cannot safely leak
        // into its siblings or the enclosing flow.
        AnalyzeRegexStaticStatements(
            statements,
            diagnostics,
            new Dictionary<string, string>(stringBindings, StringComparer.Ordinal),
            new HashSet<string>(localeFlagBindings, StringComparer.Ordinal));
    }

    private static void AnalyzeRegexStaticCall(
        CallExpressionSyntax call,
        List<LythonDiagnostic> diagnostics,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, out var arguments))
        {
            return;
        }

        int? flagsPosition = null;
        string? flagsKeyword = null;
        if (call.Target is MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "re" }, MemberName: var memberName })
        {
            switch (memberName)
            {
                case "compile":
                    flagsPosition = 1;
                    flagsKeyword = "flags";
                    break;
                case "search" or "match" or "fullmatch" or "findall" or "finditer":
                    flagsPosition = 2;
                    flagsKeyword = "flags";
                    break;
                case "sub" or "subn":
                    flagsPosition = 4;
                    flagsKeyword = "flags";
                    break;
                case "split":
                    flagsPosition = 3;
                    flagsKeyword = "flags";
                    break;
                default:
                    return;
            }
        }
        else
        {
            return;
        }

        if (arguments.TryGetValue(0, "pattern", out var patternExpression) &&
            TryResolveKnownString(patternExpression, stringBindings, out var patternText) &&
            patternText.Contains(@"\N{", StringComparison.Ordinal))
        {
            AddDiagnostic(diagnostics, "LA3044", @"Regex named character escapes like \N{...} are unsupported.", patternExpression.Span);
        }

        if (flagsPosition is int position &&
            flagsKeyword is not null &&
            arguments.TryGetValue(position, flagsKeyword, out var flagsExpression) &&
            ContainsUnsupportedRegexLocaleFlag(flagsExpression, localeFlagBindings))
        {
            AddDiagnostic(diagnostics, "LA3045", "re.LOCALE is unsupported.", flagsExpression.Span);
        }
    }

    private static void UpdateRegexBindings(
        StatementSyntax statement,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        switch (statement)
        {
            case ImportStatementSyntax importStatement:
                stringBindings.Remove(importStatement.BindingName);
                localeFlagBindings.Remove(importStatement.BindingName);
                if (importStatement.ImportedMembers is not null)
                {
                    foreach (var member in importStatement.ImportedMembers)
                    {
                        stringBindings.Remove(member.BindingName);
                        localeFlagBindings.Remove(member.BindingName);
                    }
                }
                break;

            case AssignmentStatementSyntax assignment:
                UpdateRegexBindingForName(assignment.Name, assignment.Expression, stringBindings, localeFlagBindings);
                break;

            case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                UpdateRegexBindingForName(annotated.Name, annotated.Expression, stringBindings, localeFlagBindings);
                break;

            case ChainedAssignmentStatementSyntax chained:
                foreach (var target in chained.Targets)
                {
                    if (target is NameAssignmentTargetSyntax nameTarget)
                    {
                        UpdateRegexBindingForName(nameTarget.Name, chained.Expression, stringBindings, localeFlagBindings);
                    }
                }
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                stringBindings.Remove(functionDefinition.Name);
                localeFlagBindings.Remove(functionDefinition.Name);
                break;

            case ClassDefinitionStatementSyntax classDefinition:
                stringBindings.Remove(classDefinition.Name);
                localeFlagBindings.Remove(classDefinition.Name);
                break;
        }
    }

    private static void UpdateRegexBindingForName(
        string name,
        ExpressionSyntax expression,
        Dictionary<string, string> stringBindings,
        HashSet<string> localeFlagBindings)
    {
        if (TryResolveKnownString(expression, stringBindings, out var text))
        {
            stringBindings[name] = text;
        }
        else
        {
            stringBindings.Remove(name);
        }

        if (ContainsUnsupportedRegexLocaleFlag(expression, localeFlagBindings))
        {
            localeFlagBindings.Add(name);
        }
        else
        {
            localeFlagBindings.Remove(name);
        }
    }

    private static bool TryResolveKnownString(
        ExpressionSyntax expression,
        Dictionary<string, string> stringBindings,
        out string text)
    {
        switch (expression)
        {
            case StringLiteralExpressionSyntax literal:
                text = literal.Value;
                return true;
            case ParenthesizedExpressionSyntax parenthesized:
                return TryResolveKnownString(parenthesized.Inner, stringBindings, out text);
            case IdentifierExpressionSyntax identifier when stringBindings.TryGetValue(identifier.Name, out var boundText):
                text = boundText;
                return true;
            default:
                text = string.Empty;
                return false;
        }
    }

    private static bool ContainsUnsupportedRegexLocaleFlag(ExpressionSyntax expression, HashSet<string> localeFlagBindings)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return ContainsUnsupportedRegexLocaleFlag(parenthesized.Inner, localeFlagBindings);
            case IdentifierExpressionSyntax identifier:
                return localeFlagBindings.Contains(identifier.Name);
            case MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "re" }, MemberName: "LOCALE" or "L" }:
                return true;
            case BinaryExpressionSyntax { Operator: BinaryOperatorSyntax.BitwiseOr } binary:
                return ContainsUnsupportedRegexLocaleFlag(binary.Left, localeFlagBindings) ||
                    ContainsUnsupportedRegexLocaleFlag(binary.Right, localeFlagBindings);
            default:
                return false;
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
