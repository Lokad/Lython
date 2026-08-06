namespace Lokad.Lython.Frontend;

internal sealed record FrontendResult(
    ScriptSyntax? Script,
    IReadOnlyList<LythonDiagnostic> Diagnostics);

internal sealed record ScriptSyntax(
    IReadOnlyList<StatementSyntax> Statements);

internal abstract record StatementSyntax(
    LythonSourceSpan Span);

internal sealed record ImportStatementSyntax(
    string ModuleName,
    string BindingName,
    IReadOnlyList<ImportedMemberSyntax>? ImportedMembers,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ImportedMemberSyntax(
    string Name,
    string BindingName);

internal enum ScopeDirectiveKind
{
    Global,
    Nonlocal,
}

internal sealed record ScopeDirectiveStatementSyntax(
    ScopeDirectiveKind Kind,
    IReadOnlyList<string> Names,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record AssignmentStatementSyntax(
    string Name,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal abstract record AssignmentTargetSyntax(
    LythonSourceSpan Span);

internal sealed record NameAssignmentTargetSyntax(
    string Name,
    LythonSourceSpan Span) : AssignmentTargetSyntax(Span);

internal sealed record SubscriptAssignmentTargetSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax Index,
    LythonSourceSpan Span) : AssignmentTargetSyntax(Span);

internal sealed record SliceAssignmentTargetSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax? Start,
    ExpressionSyntax? End,
    ExpressionSyntax? Step,
    LythonSourceSpan Span) : AssignmentTargetSyntax(Span);

internal sealed record MemberAssignmentTargetSyntax(
    ExpressionSyntax Target,
    string MemberName,
    LythonSourceSpan Span) : AssignmentTargetSyntax(Span);

internal sealed record UnpackingAssignmentTargetGroupSyntax(
    IReadOnlyList<UnpackingTargetSyntax> Targets,
    LythonSourceSpan Span) : AssignmentTargetSyntax(Span);

internal sealed record ChainedAssignmentStatementSyntax(
    IReadOnlyList<AssignmentTargetSyntax> Targets,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record AnnotatedAssignmentStatementSyntax(
    string Name,
    ExpressionSyntax Annotation,
    ExpressionSyntax? Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record SubscriptAssignmentStatementSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax Index,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record SliceAssignmentStatementSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax? Start,
    ExpressionSyntax? End,
    ExpressionSyntax? Step,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record MemberAssignmentStatementSyntax(
    ExpressionSyntax Target,
    string MemberName,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal enum AugmentedAssignmentOperatorSyntax
{
    Add,
    Subtract,
    Multiply,
    Divide,
    FloorDivide,
    Modulo,
    Power,
    BitwiseOr,
    BitwiseXor,
    BitwiseAnd,
    LeftShift,
    RightShift,
}

internal sealed record AugmentedAssignmentStatementSyntax(
    AssignmentTargetSyntax Target,
    AugmentedAssignmentOperatorSyntax Operator,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record UnpackingTargetSyntax(
    string Name,
    bool IsStarred);

internal sealed record UnpackingAssignmentStatementSyntax(
    IReadOnlyList<UnpackingTargetSyntax> Targets,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ExpressionStatementSyntax(
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record WithStatementSyntax(
    ExpressionSyntax ContextExpression,
    string? VariableName,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record IfStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> ThenStatements,
    IReadOnlyList<StatementSyntax>? ElseStatements,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ForStatementSyntax(
    LoopTargetSyntax Target,
    ExpressionSyntax Iterable,
    IReadOnlyList<StatementSyntax>? ElseStatements,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record WhileStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax>? ElseStatements,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record MatchStatementSyntax(
    ExpressionSyntax Subject,
    IReadOnlyList<MatchCaseSyntax> Cases,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record MatchCaseSyntax(
    PatternSyntax Pattern,
    ExpressionSyntax? Guard,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span);

internal sealed record PassStatementSyntax(
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record BreakStatementSyntax(
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ContinueStatementSyntax(
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record AssertStatementSyntax(
    ExpressionSyntax Condition,
    ExpressionSyntax? Message,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record DeleteStatementSyntax(
    ExpressionSyntax Target,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record FunctionDefinitionStatementSyntax(
    string Name,
    IReadOnlyList<ExpressionSyntax> Decorators,
    IReadOnlyList<FunctionParameterSyntax> Parameters,
    ExpressionSyntax? ReturnAnnotation,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ClassDefinitionStatementSyntax(
    string Name,
    DataclassDecoratorSyntax? DataclassDecorator,
    IReadOnlyList<ExpressionSyntax> Decorators,
    IReadOnlyList<ExpressionSyntax> Bases,
    IReadOnlyList<ClassKeywordArgumentSyntax> KeywordArguments,
    IReadOnlyList<StatementSyntax> Body,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record ClassKeywordArgumentSyntax(
    string Name,
    ExpressionSyntax Value,
    LythonSourceSpan Span);

internal sealed record DataclassDecoratorSyntax(
    bool Init,
    bool Repr,
    bool Eq,
    bool Order,
    bool UnsafeHash,
    bool Frozen,
    bool KwOnly,
    bool MatchArgs,
    LythonSourceSpan Span);

internal enum FunctionParameterKind
{
    Positional,
    KeywordOnly,
    VariadicList,
    VariadicDictionary,
}

internal sealed record FunctionParameterSyntax(
    string Name,
    ExpressionSyntax? Annotation,
    ExpressionSyntax? DefaultValue,
    FunctionParameterKind Kind = FunctionParameterKind.Positional);

internal sealed record ReturnStatementSyntax(
    ExpressionSyntax? Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record RaiseStatementSyntax(
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal sealed record TryStatementSyntax(
    IReadOnlyList<StatementSyntax> TryBody,
    IReadOnlyList<string>? ExceptionTypeNames,
    string? ExceptionVariableName,
    IReadOnlyList<StatementSyntax>? ExceptBody,
    IReadOnlyList<StatementSyntax>? ElseBody,
    IReadOnlyList<StatementSyntax>? FinallyBody,
    LythonSourceSpan Span) : StatementSyntax(Span);

internal abstract record ExpressionSyntax(
    LythonSourceSpan Span);

internal sealed record IdentifierExpressionSyntax(
    string Name,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record StringLiteralExpressionSyntax(
    string Value,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record BytesLiteralExpressionSyntax(
    byte[] Value,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record IntegerLiteralExpressionSyntax(
    string ValueText,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record FloatLiteralExpressionSyntax(
    string ValueText,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record BooleanLiteralExpressionSyntax(
    bool Value,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record NoneLiteralExpressionSyntax(
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal abstract record FormattedStringPartSyntax;

internal sealed record FormattedStringTextPartSyntax(
    string Text) : FormattedStringPartSyntax;

internal sealed record FormattedStringExpressionPartSyntax(
    ExpressionSyntax Expression,
    char? Conversion = null,
    string? FormatSpecifier = null,
    IReadOnlyList<FormattedStringPartSyntax>? FormatSpecifierParts = null) : FormattedStringPartSyntax;

internal sealed record FormattedStringExpressionSyntax(
    IReadOnlyList<FormattedStringPartSyntax> Parts,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal static class FormattedStringSyntaxTraversal
{
    public static IEnumerable<ExpressionSyntax> EnumerateExpressions(
        IReadOnlyList<FormattedStringPartSyntax> parts)
    {
        foreach (var part in parts)
        {
            if (part is not FormattedStringExpressionPartSyntax expressionPart)
            {
                continue;
            }

            yield return expressionPart.Expression;
            if (expressionPart.FormatSpecifierParts is null)
            {
                continue;
            }

            foreach (var nested in EnumerateExpressions(expressionPart.FormatSpecifierParts))
            {
                yield return nested;
            }
        }
    }
}

internal sealed record ListLiteralExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Items,
    IReadOnlyList<bool> UnpackingFlags,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record ListComprehensionExpressionSyntax(
    ExpressionSyntax ItemExpression,
    IReadOnlyList<ComprehensionClauseSyntax> Clauses,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record GeneratorExpressionSyntax(
    ExpressionSyntax ItemExpression,
    IReadOnlyList<ComprehensionClauseSyntax> Clauses,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal abstract record LoopTargetSyntax;

internal sealed record LoopNameTargetSyntax(
    string Name) : LoopTargetSyntax;

internal sealed record LoopTupleTargetSyntax(
    IReadOnlyList<LoopTargetSyntax> Items) : LoopTargetSyntax;

internal abstract record DictionaryDisplayItemSyntax(
    ExpressionSyntax Key,
    ExpressionSyntax Value,
    bool IsUnpacking,
    LythonSourceSpan Span);

internal sealed record DictionaryKeyValueItemSyntax(
    ExpressionSyntax Key,
    ExpressionSyntax Value,
    LythonSourceSpan Span) : DictionaryDisplayItemSyntax(Key, Value, false, Span);

internal sealed record DictionaryUnpackingItemSyntax(
    ExpressionSyntax Mapping,
    LythonSourceSpan Span) : DictionaryDisplayItemSyntax(Mapping, Mapping, true, Span);

internal sealed record DictLiteralExpressionSyntax(
    IReadOnlyList<DictionaryDisplayItemSyntax> Items,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record SetLiteralExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Items,
    IReadOnlyList<bool> UnpackingFlags,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record SetComprehensionExpressionSyntax(
    ExpressionSyntax ItemExpression,
    IReadOnlyList<ComprehensionClauseSyntax> Clauses,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record ComprehensionClauseSyntax(
    LoopTargetSyntax Target,
    ExpressionSyntax Iterable,
    ExpressionSyntax? Condition,
    LythonSourceSpan Span);

internal sealed record DictComprehensionExpressionSyntax(
    ExpressionSyntax KeyExpression,
    ExpressionSyntax ValueExpression,
    IReadOnlyList<ComprehensionClauseSyntax> Clauses,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record TupleLiteralExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Items,
    IReadOnlyList<bool> UnpackingFlags,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record ParenthesizedExpressionSyntax(
    ExpressionSyntax Inner,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record MemberExpressionSyntax(
    ExpressionSyntax Target,
    string MemberName,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record CallExpressionSyntax(
    ExpressionSyntax Target,
    IReadOnlyList<CallArgumentSyntax> Arguments,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal enum CallArgumentKind
{
    Positional,
    Keyword,
    StarredList,
    StarredDictionary,
}

internal sealed record CallArgumentSyntax(
    string? Name,
    ExpressionSyntax Expression,
    CallArgumentKind Kind = CallArgumentKind.Positional);

internal sealed record SubscriptExpressionSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax Index,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record SliceExpressionSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax? Start,
    ExpressionSyntax? End,
    ExpressionSyntax? Step,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal enum BinaryOperatorSyntax
{
    Or,
    And,
    Add,
    Subtract,
    Multiply,
    Divide,
    FloorDivide,
    Modulo,
    Power,
    BitwiseOr,
    BitwiseXor,
    BitwiseAnd,
    LeftShift,
    RightShift,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Is,
    IsNot,
    In,
    NotIn,
}

internal sealed record BinaryExpressionSyntax(
    ExpressionSyntax Left,
    BinaryOperatorSyntax Operator,
    ExpressionSyntax Right,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record ChainedComparisonExpressionSyntax(
    IReadOnlyList<ExpressionSyntax> Operands,
    IReadOnlyList<BinaryOperatorSyntax> Operators,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal enum UnaryOperatorSyntax
{
    Not,
    Plus,
    Minus,
    BitwiseNot,
}

internal sealed record UnaryExpressionSyntax(
    UnaryOperatorSyntax Operator,
    ExpressionSyntax Operand,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record ConditionalExpressionSyntax(
    ExpressionSyntax Consequent,
    ExpressionSyntax Condition,
    ExpressionSyntax Alternative,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record AssignmentExpressionSyntax(
    string Name,
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal sealed record LambdaExpressionSyntax(
    IReadOnlyList<FunctionParameterSyntax> Parameters,
    ExpressionSyntax Body,
    LythonSourceSpan Span) : ExpressionSyntax(Span);

internal abstract record PatternSyntax(
    LythonSourceSpan Span);

internal sealed record MatchValuePatternSyntax(
    ExpressionSyntax Expression,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal enum MatchSingletonKind
{
    None,
    True,
    False,
}

internal sealed record MatchSingletonPatternSyntax(
    MatchSingletonKind Value,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchCapturePatternSyntax(
    string Name,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchWildcardPatternSyntax(
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchSequencePatternSyntax(
    IReadOnlyList<PatternSyntax> Items,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchMappingPatternItemSyntax(
    ExpressionSyntax Key,
    PatternSyntax Pattern);

internal sealed record MatchMappingPatternSyntax(
    IReadOnlyList<MatchMappingPatternItemSyntax> Items,
    string? RestName,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchClassKeywordPatternSyntax(
    string Name,
    PatternSyntax Pattern);

internal sealed record MatchClassPatternSyntax(
    ExpressionSyntax ClassExpression,
    IReadOnlyList<PatternSyntax> PositionalPatterns,
    IReadOnlyList<MatchClassKeywordPatternSyntax> KeywordPatterns,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchStarPatternSyntax(
    string? Name,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchAsPatternSyntax(
    PatternSyntax Pattern,
    string Name,
    LythonSourceSpan Span) : PatternSyntax(Span);

internal sealed record MatchOrPatternSyntax(
    IReadOnlyList<PatternSyntax> Patterns,
    LythonSourceSpan Span) : PatternSyntax(Span);
