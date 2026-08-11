namespace Lokad.Lython.Frontend;

internal static class AnnotationDiagnostics
{
    [Flags]
    private enum SimpleType
    {
        Unknown = 0,
        None = 1 << 0,
        Bool = 1 << 1,
        Int = 1 << 2,
        Float = 1 << 3,
        String = 1 << 4,
        Bytes = 1 << 5,
        List = 1 << 6,
        Tuple = 1 << 7,
        Dict = 1 << 8,
        Set = 1 << 9,
    }

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script)
    {
        var diagnostics = new List<LythonDiagnostic>();
        AnalyzeStatements(script.Statements, diagnostics, new AbstractState(), returnAnnotation: null);
        return diagnostics;
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        ExpressionSyntax? returnAnnotation)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, diagnostics, bindings, returnAnnotation);
            StaticAbstractValueResolver.UpdateBindings(statement, bindings);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        ExpressionSyntax? returnAnnotation)
    {
        void AnalyzeLoop(
            IReadOnlyList<StatementSyntax> body,
            IReadOnlyList<StatementSyntax>? elseStatements)
        {
            // A loop may execute zero times, so facts from its body must be joined with entry facts.
            var bodyBindings = bindings.Clone();
            AnalyzeStatements(body, diagnostics, bodyBindings, returnAnnotation);
            var merged = AbstractState.Merge(bindings, bodyBindings);
            if (elseStatements is not null)
            {
                var elseBindings = bindings.Clone();
                AnalyzeStatements(elseStatements, diagnostics, elseBindings, returnAnnotation);
                merged = AbstractState.Merge(merged, elseBindings);
            }

            bindings.ReplaceWith(merged);
        }

        switch (statement)
        {
            case AnnotatedAssignmentStatementSyntax annotatedAssignment:
                if (annotatedAssignment.Expression is not null)
                {
                    AddMismatchDiagnostic(
                        diagnostics,
                        "LA1080",
                        "annotated assignment",
                        annotatedAssignment.Annotation,
                        annotatedAssignment.Expression,
                        annotatedAssignment.Span,
                        bindings);
                }
                break;

            case FunctionDefinitionStatementSyntax functionDefinition:
                AnalyzeFunctionDefinition(functionDefinition, diagnostics, bindings);
                break;

            case ReturnStatementSyntax returnStatement when returnAnnotation is not null:
                AddMismatchDiagnostic(
                    diagnostics,
                    "LA1082",
                    "return value",
                    returnAnnotation,
                    returnStatement.Expression,
                    returnStatement.Span,
                    bindings);
                break;

            case IfStatementSyntax ifStatement:
                {
                    var thenBindings = bindings.Clone();
                    AnalyzeStatements(ifStatement.ThenStatements, diagnostics, thenBindings, returnAnnotation);
                    AbstractState elseBindings;
                    if (ifStatement.ElseStatements is not null)
                    {
                        elseBindings = bindings.Clone();
                        AnalyzeStatements(ifStatement.ElseStatements, diagnostics, elseBindings, returnAnnotation);
                    }
                    else
                    {
                        elseBindings = bindings.Clone();
                    }

                    bindings.MergeFrom(thenBindings, elseBindings);
                    break;
                }

            case ForStatementSyntax forStatement:
                AnalyzeLoop(forStatement.Body, forStatement.ElseStatements);
                break;

            case WhileStatementSyntax whileStatement:
                AnalyzeLoop(whileStatement.Body, whileStatement.ElseStatements);
                break;

            case WithStatementSyntax withStatement:
                {
                    var withBindings = bindings.Clone();
                    if (withStatement.VariableName is not null &&
                        StaticAbstractValueResolver.TryResolve(withStatement.ContextExpression, bindings, out var contextValue) &&
                        contextValue.Kind == AbstractValueKind.TextFileHandle)
                    {
                        withBindings.Set(withStatement.VariableName, contextValue);
                    }

                    AnalyzeStatements(withStatement.Body, diagnostics, withBindings, returnAnnotation);
                    bindings.ReplaceWith(withBindings);
                    if (withStatement.VariableName is not null)
                    {
                        bindings.Remove(withStatement.VariableName);
                    }
                    break;
                }

            case MatchStatementSyntax matchStatement:
                {
                    var merged = bindings.Clone();
                    var hasCase = false;
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        var caseBindings = bindings.Clone();
                        AnalyzeStatements(matchCase.Body, diagnostics, caseBindings, returnAnnotation);
                        merged = hasCase ? AbstractState.Merge(merged, caseBindings) : caseBindings;
                        hasCase = true;
                    }

                    if (hasCase)
                    {
                        bindings.ReplaceWith(AbstractState.Merge(bindings, merged));
                    }
                    break;
                }

            case TryStatementSyntax tryStatement:
                {
                    // Join every reachable continuation; finally then observes and updates that joined state.
                    var merged = bindings.Clone();
                    var tryBindings = bindings.Clone();
                    AnalyzeStatements(tryStatement.TryBody, diagnostics, tryBindings, returnAnnotation);
                    merged = AbstractState.Merge(merged, tryBindings);

                    if (tryStatement.ExceptBody is not null)
                    {
                        var exceptBindings = bindings.Clone();
                        AnalyzeStatements(tryStatement.ExceptBody, diagnostics, exceptBindings, returnAnnotation);
                        merged = AbstractState.Merge(merged, exceptBindings);
                    }

                    if (tryStatement.ElseBody is not null)
                    {
                        var elseBindings = tryBindings.Clone();
                        AnalyzeStatements(tryStatement.ElseBody, diagnostics, elseBindings, returnAnnotation);
                        merged = AbstractState.Merge(merged, elseBindings);
                    }

                    if (tryStatement.FinallyBody is not null)
                    {
                        AnalyzeStatements(tryStatement.FinallyBody, diagnostics, merged, returnAnnotation);
                    }

                    bindings.ReplaceWith(merged);
                    break;
                }

            case ClassDefinitionStatementSyntax classDefinition:
                AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone(), returnAnnotation: null);
                break;
        }
    }

    private static void AnalyzeFunctionDefinition(
        FunctionDefinitionStatementSyntax functionDefinition,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var parameter in functionDefinition.Parameters)
        {
            if (parameter.Annotation is not null && parameter.DefaultValue is not null)
            {
                AddMismatchDiagnostic(
                    diagnostics,
                    "LA1081",
                    $"default value for parameter '{parameter.Name}'",
                    parameter.Annotation,
                    parameter.DefaultValue,
                    parameter.DefaultValue.Span,
                    bindings);
            }
        }

        var functionBindings = bindings.Clone();
        foreach (var parameter in functionDefinition.Parameters)
        {
            functionBindings.Remove(parameter.Name);
        }

        AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings, functionDefinition.ReturnAnnotation);
    }

    private static void AddMismatchDiagnostic(
        List<LythonDiagnostic> diagnostics,
        string code,
        string label,
        ExpressionSyntax annotation,
        ExpressionSyntax? value,
        LythonSourceSpan span,
        AbstractState bindings)
    {
        var expected = TryGetAnnotationType(annotation);
        var actual = TryGetValueType(value, bindings);
        if (expected == SimpleType.Unknown || actual == SimpleType.Unknown || (expected & actual) != 0)
        {
            return;
        }

        diagnostics.Add(new LythonDiagnostic(
            code,
            $"Annotated {label} expects {DescribeType(expected)}, but the provided value is {DescribeType(actual)}.",
            LythonDiagnosticSeverity.Error,
            span));
    }

    private static SimpleType TryGetAnnotationType(ExpressionSyntax annotation)
    {
        return annotation switch
        {
            IdentifierExpressionSyntax identifier => identifier.Name switch
            {
                "None" => SimpleType.None,
                "bool" => SimpleType.Bool,
                "int" => SimpleType.Int,
                "float" => SimpleType.Float,
                "str" => SimpleType.String,
                "bytes" => SimpleType.Bytes,
                "list" => SimpleType.List,
                "tuple" => SimpleType.Tuple,
                "dict" => SimpleType.Dict,
                "set" => SimpleType.Set,
                _ => SimpleType.Unknown
            },
            NoneLiteralExpressionSyntax => SimpleType.None,
            ParenthesizedExpressionSyntax parenthesized => TryGetAnnotationType(parenthesized.Inner),
            SubscriptExpressionSyntax subscript => TryGetAnnotationType(subscript.Target),
            BinaryExpressionSyntax binary when binary.Operator == BinaryOperatorSyntax.BitwiseOr
                => TryGetAnnotationType(binary.Left) | TryGetAnnotationType(binary.Right),
            _ => SimpleType.Unknown
        };
    }

    private static SimpleType TryGetValueType(ExpressionSyntax? value, AbstractState bindings)
    {
        if (value is null)
        {
            return SimpleType.None;
        }

        if (StaticAbstractValueResolver.TryResolve(value, bindings, out var abstractValue))
        {
            var abstractType = TryGetValueType(abstractValue);
            if (abstractType != SimpleType.Unknown)
            {
                return abstractType;
            }
        }

        return value switch
        {
            ParenthesizedExpressionSyntax parenthesized => TryGetValueType(parenthesized.Inner, bindings),
            UnaryExpressionSyntax unary when unary.Operator == UnaryOperatorSyntax.Plus || unary.Operator == UnaryOperatorSyntax.Minus
                => TryGetValueType(unary.Operand, bindings),
            _ => SimpleType.Unknown
        };
    }

    private static SimpleType TryGetValueType(AbstractValue value)
    {
        return value.Kind switch
        {
            AbstractValueKind.String or AbstractValueKind.StringType => SimpleType.String,
            AbstractValueKind.Bytes or AbstractValueKind.BytesType => SimpleType.Bytes,
            AbstractValueKind.Integer or AbstractValueKind.IntegerType => SimpleType.Int,
            AbstractValueKind.Float or AbstractValueKind.FloatType => SimpleType.Float,
            AbstractValueKind.Boolean or AbstractValueKind.BooleanType => SimpleType.Bool,
            AbstractValueKind.None => SimpleType.None,
            AbstractValueKind.List or AbstractValueKind.ListType => SimpleType.List,
            AbstractValueKind.Tuple => SimpleType.Tuple,
            AbstractValueKind.Dict => SimpleType.Dict,
            AbstractValueKind.Set or AbstractValueKind.SetType => SimpleType.Set,
            _ => SimpleType.Unknown
        };
    }

    private static string DescribeType(SimpleType type)
    {
        if (type == SimpleType.Unknown)
        {
            return "an unknown type";
        }

        var parts = new List<string>();
        if ((type & SimpleType.None) != 0) parts.Add("None");
        if ((type & SimpleType.Bool) != 0) parts.Add("bool");
        if ((type & SimpleType.Int) != 0) parts.Add("int");
        if ((type & SimpleType.Float) != 0) parts.Add("float");
        if ((type & SimpleType.String) != 0) parts.Add("str");
        if ((type & SimpleType.Bytes) != 0) parts.Add("bytes");
        if ((type & SimpleType.List) != 0) parts.Add("list");
        if ((type & SimpleType.Tuple) != 0) parts.Add("tuple");
        if ((type & SimpleType.Dict) != 0) parts.Add("dict");
        if ((type & SimpleType.Set) != 0) parts.Add("set");
        return string.Join(" | ", parts);
    }
}
