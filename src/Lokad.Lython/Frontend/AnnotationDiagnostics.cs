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

    public static IReadOnlyList<LythonDiagnostic> Analyze(ScriptSyntax script, StaticAnalysisContext? context = null)
    {
        // Annotation flow deliberately visits dead branches. It cannot share
        // the ordinary analyzer's refined bindings, but no later check can use
        // facts produced after an isolated scope's final annotation-dependent
        // statement. Control-flow bodies still export their complete exit facts.
        var lastChecks = new Dictionary<IReadOnlyList<StatementSyntax>, int>(ReferenceEqualityComparer.Instance);
        var requiresRegexBindings = false;
        var last = Prepare(script.Statements, annotatedReturn: false);
        if (last < 0 && !requiresRegexBindings) return [];
        var diagnostics = new List<LythonDiagnostic>();
        // Constant regex analysis also consumes this conservative flow, even
        // in dead/deferred scopes. Retain it in full when regex bindings can
        // enter the script; aliases and rebinding need the original analysis.
        AnalyzeStatements(script.Statements, diagnostics, new AbstractState(context), returnAnnotation: null,
            requiresRegexBindings ? null : lastChecks);
        return diagnostics;

        int Prepare(IReadOnlyList<StatementSyntax> statements, bool annotatedReturn)
        {
            var last = -1;
            for (var i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                if (statement is ImportStatementSyntax import &&
                    (import.ModuleName == "re" || import.ModuleName.StartsWith("re.", StringComparison.Ordinal)))
                    requiresRegexBindings = true;
                context?.MemoryGovernor?.ThrowIfAdmissionFailed();
                PythonTokenization.CheckCancellation(context?.CancellationToken ?? default, statement.Span.Start);
                bool needed;
                if (statement is FunctionDefinitionStatementSyntax function)
                {
                    needed = Prepare(function.Body, function.ReturnAnnotation is not null) >= 0;
                    needed |= function.Parameters.Any(parameter => parameter.Annotation is not null && parameter.DefaultValue is not null);
                }
                else if (statement is ClassDefinitionStatementSyntax definition)
                    needed = Prepare(definition.Body, annotatedReturn: false) >= 0;
                else
                {
                    needed = statement is AnnotatedAssignmentStatementSyntax { Expression: not null }
                        || annotatedReturn && statement is ReturnStatementSyntax;
                    foreach (var body in StatementSyntaxTraversal.EnumerateChildBodies(statement))
                        needed |= Prepare(body, annotatedReturn) >= 0;
                }
                if (needed) last = i;
            }
            lastChecks[statements] = lastChecks.TryGetValue(statements, out var previous)
                ? Math.Max(previous, last) : last;
            return last;
        }
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        ExpressionSyntax? returnAnnotation,
        IReadOnlyDictionary<IReadOnlyList<StatementSyntax>, int>? lastChecks,
        bool exportBindings = false)
    {
        exportBindings |= lastChecks is null;
        var last = exportBindings ? statements.Count - 1 : lastChecks![statements];
        for (var i = 0; i <= last; i++)
        {
            var statement = statements[i];
            AnalyzeStatement(statement, diagnostics, bindings, returnAnnotation, lastChecks);
            // The check observes incoming facts. No consumer remains after it.
            if (exportBindings || i < last) StaticAbstractValueResolver.UpdateBindings(statement, bindings);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        ExpressionSyntax? returnAnnotation,
        IReadOnlyDictionary<IReadOnlyList<StatementSyntax>, int>? lastChecks)
    {
        void AnalyzeLoop(
            IReadOnlyList<StatementSyntax> body,
            IReadOnlyList<StatementSyntax>? elseStatements)
        {
            // A loop may execute zero times, so facts from its body must be joined with entry facts.
            var bodyBindings = bindings.Clone();
            AnalyzeStatements(body, diagnostics, bodyBindings, returnAnnotation, lastChecks, exportBindings: true);
            var merged = AbstractState.Merge(bindings, bodyBindings);
            if (elseStatements is not null)
            {
                var elseBindings = bindings.Clone();
                AnalyzeStatements(elseStatements, diagnostics, elseBindings, returnAnnotation, lastChecks, exportBindings: true);
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
                AnalyzeFunctionDefinition(functionDefinition, diagnostics, bindings, lastChecks);
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
                    AnalyzeStatements(ifStatement.ThenStatements, diagnostics, thenBindings, returnAnnotation, lastChecks, exportBindings: true);
                    AbstractState elseBindings;
                    if (ifStatement.ElseStatements is not null)
                    {
                        elseBindings = bindings.Clone();
                        AnalyzeStatements(ifStatement.ElseStatements, diagnostics, elseBindings, returnAnnotation, lastChecks, exportBindings: true);
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
                        foreach (var name in withStatement.BoundNames) withBindings.Remove(name);
                    if (withStatement.VariableName is not null &&
                        StaticAbstractValueResolver.TryResolve(withStatement.ContextExpression, bindings, out var contextValue) &&
                        contextValue.Kind is AbstractValueKind.TextFileHandle or AbstractValueKind.BinaryFileHandle or AbstractValueKind.StringIO or AbstractValueKind.BytesIO)
                    {
                        withBindings.Set(withStatement.VariableName, contextValue);
                    }

                    AnalyzeStatements(withStatement.Body, diagnostics, withBindings, returnAnnotation, lastChecks, exportBindings: true);
                    bindings.ReplaceWith(withBindings);
                    foreach (var name in withStatement.BoundNames) bindings.Remove(name);
                    break;
                }

            case MatchStatementSyntax matchStatement:
                {
                    var merged = bindings.Clone();
                    var hasCase = false;
                    foreach (var matchCase in matchStatement.Cases)
                    {
                        var caseBindings = bindings.Clone();
                        AnalyzeStatements(matchCase.Body, diagnostics, caseBindings, returnAnnotation, lastChecks, exportBindings: true);
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
                    AnalyzeStatements(tryStatement.TryBody, diagnostics, tryBindings, returnAnnotation, lastChecks, exportBindings: true);
                    merged = AbstractState.Merge(merged, tryBindings);

                    foreach (var exceptClause in tryStatement.ExceptClauses)
                    {
                        var exceptBindings = bindings.Clone();
                        AnalyzeStatements(exceptClause.Body, diagnostics, exceptBindings, returnAnnotation, lastChecks, exportBindings: true);
                        merged = AbstractState.Merge(merged, exceptBindings);
                    }

                    if (tryStatement.ElseBody is not null)
                    {
                        var elseBindings = tryBindings.Clone();
                        AnalyzeStatements(tryStatement.ElseBody, diagnostics, elseBindings, returnAnnotation, lastChecks, exportBindings: true);
                        merged = AbstractState.Merge(merged, elseBindings);
                    }

                    if (tryStatement.FinallyBody is not null)
                    {
                        AnalyzeStatements(tryStatement.FinallyBody, diagnostics, merged, returnAnnotation, lastChecks, exportBindings: true);
                    }

                    bindings.ReplaceWith(merged);
                    break;
                }

            case ClassDefinitionStatementSyntax classDefinition:
                AnalyzeStatements(classDefinition.Body, diagnostics, bindings.Clone(), returnAnnotation: null, lastChecks);
                break;
        }
    }

    private static void AnalyzeFunctionDefinition(
        FunctionDefinitionStatementSyntax functionDefinition,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        IReadOnlyDictionary<IReadOnlyList<StatementSyntax>, int>? lastChecks)
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

        AnalyzeStatements(functionDefinition.Body, diagnostics, functionBindings, functionDefinition.ReturnAnnotation, lastChecks);
    }

    private static void AddMismatchDiagnostic(
        List<LythonDiagnostic> diagnostics,
        LythonDiagnosticCode code,
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
            code.Value,
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
