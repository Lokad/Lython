namespace Lokad.Lython.Frontend;

internal enum LoweredStatementKind
{
    Import,
    FunctionDefinition,
    ClassDefinition,
    ControlFlow,
    Assignment,
    Expression,
    Other,
}

internal abstract record LoweredStatement(LythonSourceSpan Span)
{
    public abstract LoweredStatementKind Kind { get; }
}

internal sealed record LoweredImportStatement(
    ImportStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Import;
}

internal sealed record LoweredFunctionParameter(
    string Name,
    FunctionParameterKind Kind,
    LoweredExpression? Annotation,
    LoweredExpression? DefaultValue);

internal sealed record LoweredFunctionDefinitionStatement(
    FunctionDefinitionStatementSyntax Syntax,
    IReadOnlyList<LoweredExpression> Decorators,
    IReadOnlyList<LoweredFunctionParameter> Parameters,
    LoweredExpression? ReturnAnnotation,
    IReadOnlyList<LoweredStatement> Body) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.FunctionDefinition;
}

internal sealed record LoweredClassDefinitionStatement(
    ClassDefinitionStatementSyntax Syntax,
    IReadOnlyList<LoweredExpression> Decorators,
    IReadOnlyList<LoweredExpression> Bases,
    IReadOnlyList<LoweredCallArgument> KeywordArguments,
    IReadOnlyList<LoweredStatement> Body) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ClassDefinition;
}

internal sealed record LoweredAssignmentStatement(
    StatementSyntax Syntax,
    LoweredExpression? Expression,
    LoweredExpression? Annotation = null,
    LoweredExpression? Target = null,
    LoweredExpression? Index = null,
    string? MemberName = null) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Assignment;
}

internal sealed record LoweredExpressionStatement(
    ExpressionStatementSyntax Syntax,
    LoweredExpression Expression) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Expression;
}

internal sealed record LoweredIfStatement(
    IfStatementSyntax Syntax,
    LoweredExpression Condition,
    IReadOnlyList<LoweredStatement> ThenStatements,
    IReadOnlyList<LoweredStatement>? ElseStatements) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredForStatement(
    ForStatementSyntax Syntax,
    LoweredExpression Iterable,
    IReadOnlyList<LoweredStatement>? ElseStatements,
    IReadOnlyList<LoweredStatement> Body) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredWhileStatement(
    WhileStatementSyntax Syntax,
    LoweredExpression Condition,
    IReadOnlyList<LoweredStatement>? ElseStatements,
    IReadOnlyList<LoweredStatement> Body) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredMatchCase(
    MatchCaseSyntax Syntax,
    IReadOnlyList<LoweredStatement> Body);

internal sealed record LoweredMatchStatement(
    MatchStatementSyntax Syntax,
    LoweredExpression Subject,
    IReadOnlyList<LoweredMatchCase> Cases) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredWithStatement(
    WithStatementSyntax Syntax,
    LoweredExpression ContextExpression,
    IReadOnlyList<LoweredStatement> Body) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredTryStatement(
    TryStatementSyntax Syntax,
    IReadOnlyList<LoweredStatement> TryBody,
    IReadOnlyList<LoweredStatement>? ExceptBody,
    IReadOnlyList<LoweredStatement>? ElseBody,
    IReadOnlyList<LoweredStatement>? FinallyBody) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.ControlFlow;
}

internal sealed record LoweredPassStatement(
    PassStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredBreakStatement(
    BreakStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredContinueStatement(
    ContinueStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredAssertStatement(
    AssertStatementSyntax Syntax,
    LoweredExpression Condition,
    LoweredExpression? Message) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredDeleteStatement(
    DeleteStatementSyntax Syntax,
    LoweredExpression Target) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredReturnStatement(
    ReturnStatementSyntax Syntax,
    LoweredExpression? Expression) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredRaiseStatement(
    RaiseStatementSyntax Syntax,
    LoweredExpression Expression) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredOtherStatement(
    StatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}
