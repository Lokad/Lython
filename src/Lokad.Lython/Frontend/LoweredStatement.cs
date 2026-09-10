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
    /// <summary>Gets the stable lowered statement discriminator used by interpreters and analyzers.</summary>
    public abstract LoweredStatementKind Kind { get; }
}

internal sealed record LoweredImportStatement(
    ImportStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Import;
}

internal sealed record LoweredScopeDirectiveStatement(
    ScopeDirectiveStatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
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

internal abstract record LoweredAssignmentStatement(
    StatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Assignment;
}

internal sealed record LoweredNameAssignmentStatement(
    AssignmentStatementSyntax Assignment,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredChainedAssignmentStatement(
    ChainedAssignmentStatementSyntax Assignment,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredAnnotatedAssignmentStatement(
    AnnotatedAssignmentStatementSyntax Assignment,
    LoweredExpression Annotation,
    LoweredExpression? Expression) : LoweredAssignmentStatement(Assignment);

internal abstract record LoweredAugmentedAssignmentTarget(
    AssignmentTargetSyntax Syntax)
{
    public LythonSourceSpan Span => Syntax.Span;
}

internal sealed record LoweredNameAugmentedAssignmentTarget(
    NameAssignmentTargetSyntax Target) : LoweredAugmentedAssignmentTarget(Target);

internal sealed record LoweredSubscriptAugmentedAssignmentTarget(
    SubscriptAssignmentTargetSyntax Target,
    LoweredExpression Receiver,
    LoweredExpression Index) : LoweredAugmentedAssignmentTarget(Target);

internal sealed record LoweredSliceAugmentedAssignmentTarget(
    SliceAssignmentTargetSyntax Target,
    LoweredExpression Receiver,
    LoweredExpression? Start,
    LoweredExpression? End,
    LoweredExpression? Step) : LoweredAugmentedAssignmentTarget(Target);

internal sealed record LoweredMemberAugmentedAssignmentTarget(
    MemberAssignmentTargetSyntax Target,
    LoweredExpression Receiver) : LoweredAugmentedAssignmentTarget(Target);

internal sealed record LoweredAugmentedAssignmentStatement(
    AugmentedAssignmentStatementSyntax Assignment,
    LoweredAugmentedAssignmentTarget Target,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredUnpackingAssignmentStatement(
    UnpackingAssignmentStatementSyntax Assignment,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredSubscriptAssignmentStatement(
    SubscriptAssignmentStatementSyntax Assignment,
    LoweredExpression Receiver,
    LoweredExpression Index,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredSliceAssignmentStatement(
    SliceAssignmentStatementSyntax Assignment,
    LoweredExpression Receiver,
    LoweredExpression? Start,
    LoweredExpression? End,
    LoweredExpression? Step,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

internal sealed record LoweredMemberAssignmentStatement(
    MemberAssignmentStatementSyntax Assignment,
    LoweredExpression Receiver,
    LoweredExpression Expression) : LoweredAssignmentStatement(Assignment);

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

internal sealed record LoweredExceptClause(
    ExceptClauseSyntax Syntax,
    IReadOnlyList<LoweredStatement> Body);

internal sealed record LoweredTryStatement(
    TryStatementSyntax Syntax,
    IReadOnlyList<LoweredStatement> TryBody,
    IReadOnlyList<LoweredExceptClause> ExceptClauses,
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
    LoweredExpression? Expression,
    LoweredExpression? CauseExpression) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}

internal sealed record LoweredOtherStatement(
    StatementSyntax Syntax) : LoweredStatement(Syntax.Span)
{
    public override LoweredStatementKind Kind => LoweredStatementKind.Other;
}
