namespace Lokad.Lython.Frontend;

internal abstract record LoweredExpression
{
    public abstract ExpressionSyntax Syntax { get; }

    public LythonSourceSpan Span => Syntax.Span;
}

internal sealed record LoweredIdentifierExpression(
    IdentifierExpressionSyntax Identifier) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Identifier;
}

internal sealed record LoweredStringLiteralExpression(
    StringLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal sealed record LoweredBytesLiteralExpression(
    BytesLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal sealed record LoweredIntegerLiteralExpression(
    IntegerLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal sealed record LoweredFloatLiteralExpression(
    FloatLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal sealed record LoweredBooleanLiteralExpression(
    BooleanLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal sealed record LoweredNoneLiteralExpression(
    NoneLiteralExpressionSyntax Literal) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Literal;
}

internal abstract record LoweredFormattedStringPart;

internal sealed record LoweredFormattedStringTextPart(
    string Text) : LoweredFormattedStringPart;

internal sealed record LoweredFormattedStringExpressionPart(
    LoweredExpression Expression,
    char? Conversion,
    string? FormatSpecifier,
    IReadOnlyList<LoweredFormattedStringPart>? FormatSpecifierParts) : LoweredFormattedStringPart;

internal sealed record LoweredFormattedStringExpression(
    FormattedStringExpressionSyntax FormattedString,
    IReadOnlyList<LoweredFormattedStringPart> Parts) : LoweredExpression
{
    public override ExpressionSyntax Syntax => FormattedString;
}

internal sealed record LoweredParenthesizedExpression(
    ParenthesizedExpressionSyntax Parenthesized,
    LoweredExpression Inner) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Parenthesized;
}

internal sealed record LoweredListLiteralExpression(
    ListLiteralExpressionSyntax List,
    IReadOnlyList<LoweredExpression> Items) : LoweredExpression
{
    public override ExpressionSyntax Syntax => List;
}

internal sealed record LoweredTupleLiteralExpression(
    TupleLiteralExpressionSyntax Tuple,
    IReadOnlyList<LoweredExpression> Items) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Tuple;
}

internal sealed record LoweredSetLiteralExpression(
    SetLiteralExpressionSyntax Set,
    IReadOnlyList<LoweredExpression> Items) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Set;
}

internal sealed record LoweredSetComprehensionExpression(
    SetComprehensionExpressionSyntax SetComprehension,
    LoweredExpression ItemExpression,
    IReadOnlyList<LoweredComprehensionClause> Clauses) : LoweredExpression
{
    public override ExpressionSyntax Syntax => SetComprehension;
}

internal sealed record LoweredComprehensionClause(
    LoopTargetSyntax Target,
    LoweredExpression Iterable,
    LoweredExpression? Condition,
    LythonSourceSpan Span);

internal sealed record LoweredListComprehensionExpression(
    ListComprehensionExpressionSyntax ListComprehension,
    LoweredExpression ItemExpression,
    IReadOnlyList<LoweredComprehensionClause> Clauses) : LoweredExpression
{
    public override ExpressionSyntax Syntax => ListComprehension;
}

internal sealed record LoweredGeneratorExpression(
    GeneratorExpressionSyntax Generator,
    LoweredExpression ItemExpression,
    IReadOnlyList<LoweredComprehensionClause> Clauses) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Generator;
}

internal abstract record LoweredDictionaryDisplayItem(
    DictionaryDisplayItemSyntax Syntax,
    LoweredExpression Key,
    LoweredExpression Value,
    bool IsUnpacking);

internal sealed record LoweredDictionaryKeyValueItem(
    DictionaryKeyValueItemSyntax Item,
    LoweredExpression Key,
    LoweredExpression Value) : LoweredDictionaryDisplayItem(Item, Key, Value, false);

internal sealed record LoweredDictionaryUnpackingItem(
    DictionaryUnpackingItemSyntax Item,
    LoweredExpression Mapping) : LoweredDictionaryDisplayItem(Item, Mapping, Mapping, true);

internal sealed record LoweredDictLiteralExpression(
    DictLiteralExpressionSyntax Dict,
    IReadOnlyList<LoweredDictionaryDisplayItem> Items) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Dict;
}

internal sealed record LoweredDictComprehensionExpression(
    DictComprehensionExpressionSyntax DictComprehension,
    LoweredExpression KeyExpression,
    LoweredExpression ValueExpression,
    IReadOnlyList<LoweredComprehensionClause> Clauses) : LoweredExpression
{
    public override ExpressionSyntax Syntax => DictComprehension;
}

internal sealed record LoweredMemberExpression(
    MemberExpressionSyntax Member,
    LoweredExpression Target) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Member;
}

internal sealed record LoweredCallArgument(
    string? Name,
    LoweredExpression Expression,
    CallArgumentKind Kind = CallArgumentKind.Positional);

internal sealed record LoweredCallExpression(
    CallExpressionSyntax Call,
    LoweredExpression Target,
    IReadOnlyList<LoweredCallArgument> Arguments) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Call;
}

internal sealed record LoweredSubscriptExpression(
    SubscriptExpressionSyntax Subscript,
    LoweredExpression Target,
    LoweredExpression Index) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Subscript;
}

internal sealed record LoweredSliceExpression(
    SliceExpressionSyntax Slice,
    LoweredExpression Target,
    LoweredExpression? Start,
    LoweredExpression? End,
    LoweredExpression? Step) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Slice;
}

internal sealed record LoweredBinaryExpression(
    BinaryExpressionSyntax Binary,
    LoweredExpression Left,
    LoweredExpression Right) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Binary;
}

internal sealed record LoweredChainedComparisonExpression(
    ChainedComparisonExpressionSyntax ChainedComparison,
    IReadOnlyList<LoweredExpression> Operands) : LoweredExpression
{
    public override ExpressionSyntax Syntax => ChainedComparison;
}

internal sealed record LoweredUnaryExpression(
    UnaryExpressionSyntax Unary,
    LoweredExpression Operand) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Unary;
}

internal sealed record LoweredConditionalExpression(
    ConditionalExpressionSyntax Conditional,
    LoweredExpression Consequent,
    LoweredExpression Condition,
    LoweredExpression Alternative) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Conditional;
}

internal sealed record LoweredAssignmentExpression(
    AssignmentExpressionSyntax Assignment,
    LoweredExpression Expression) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Assignment;
}

internal sealed record LoweredLambdaExpression(
    LambdaExpressionSyntax Lambda,
    LoweredExpression Body) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Lambda;
}

internal sealed record LoweredOtherExpression(
    ExpressionSyntax Expression) : LoweredExpression
{
    public override ExpressionSyntax Syntax => Expression;
}
