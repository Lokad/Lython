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
        foreach (var expression in StatementSyntaxTraversal.EnumerateDirectExpressions(statement))
        {
            AnalyzeRegexStaticExpression(expression, diagnostics, stringBindings, localeFlagBindings);
        }

        if (statement is FunctionDefinitionStatementSyntax functionDefinition)
        {
            var functionStringBindings = new Dictionary<string, string>(stringBindings, StringComparer.Ordinal);
            var functionLocaleBindings = new HashSet<string>(localeFlagBindings, StringComparer.Ordinal);
            foreach (var parameter in functionDefinition.Parameters)
            {
                functionStringBindings.Remove(parameter.Name);
                functionLocaleBindings.Remove(parameter.Name);
            }

            AnalyzeRegexStaticStatements(functionDefinition.Body, diagnostics, functionStringBindings, functionLocaleBindings);
            return;
        }

        foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
        {
            AnalyzeRegexNestedStatements(body, diagnostics, stringBindings, localeFlagBindings);
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
