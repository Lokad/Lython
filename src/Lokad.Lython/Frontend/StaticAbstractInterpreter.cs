namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractInterpreter
{
    private static readonly IStatementAnalyzer[] StatementAnalyzers =
    [
        AssignmentStatementAnalyzer.Instance,
        ControlFlowStatementAnalyzer.Instance,
        DefinitionStatementAnalyzer.Instance,
        SimpleStatementAnalyzer.Instance,
    ];

    public static void Analyze(StaticAnalysisContext context)
    {
        AnalyzeStatements(context.Script.Statements, context.DiagnosticList, new AbstractState());
    }

    private static void AnalyzeStatements(
        IReadOnlyList<StatementSyntax> statements,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var statement in statements)
        {
            AnalyzeStatement(statement, diagnostics, bindings);
            StaticBindingEngine.UpdateBindings(statement, bindings);
        }
    }

    private static void AnalyzeStatement(
        StatementSyntax statement,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        foreach (var analyzer in StatementAnalyzers)
        {
            if (analyzer.TryAnalyze(statement, diagnostics, bindings))
            {
                return;
            }
        }
    }

    private static void AnalyzeAssignmentTarget(
        AssignmentTargetSyntax target,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax:
                // StaticBindingEngine owns its binding effect.
                break;

            case UnpackingAssignmentTargetGroupSyntax unpacking:
                AnalyzeUnpackingTargetGroup(unpacking, diagnostics, bindings);
                break;

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

    private static void AnalyzeUnpackingTargetGroup(
        UnpackingAssignmentTargetGroupSyntax unpacking,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        // Names bind through StaticBindingEngine; every other item reads like
        // the matching single-target assignment.
        foreach (var target in unpacking.Targets)
        {
            switch (target)
            {
                case UnpackingSubscriptTargetSyntax subscript:
                    AnalyzeExpression(subscript.Target, diagnostics, bindings);
                    AnalyzeExpression(subscript.Index, diagnostics, bindings);
                    break;

                case UnpackingSliceTargetSyntax slice:
                    AnalyzeExpression(slice.Target, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Start, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.End, diagnostics, bindings);
                    AnalyzeExpressionIfPresent(slice.Step, diagnostics, bindings);
                    break;

                case UnpackingMemberTargetSyntax member:
                    AnalyzeExpression(member.Target, diagnostics, bindings);
                    break;
            }
        }
    }

    private static void AnalyzeExpressionIfPresent(

        ExpressionSyntax? expression,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (expression is not null)
        {
            AnalyzeExpression(expression, diagnostics, bindings);
        }
    }

    private interface IStatementAnalyzer
    {
        /// <summary>Analyzes the statement when it belongs to this analyzer's syntax family.</summary>
        bool TryAnalyze(StatementSyntax statement, List<LythonDiagnostic> diagnostics, AbstractState bindings);
    }
}
