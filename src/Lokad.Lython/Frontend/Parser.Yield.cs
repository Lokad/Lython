namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParseYieldExpression()
    {
        var start = ReadToken();
        var delegated = TryRead(Token.From, out _);
        ExpressionSyntax? value = null;
        if (delegated || CurrentToken is not (Token.Eol or Token.End or Token.Semicolon or Token.CloseParen or Token.CloseBracket or Token.CloseBrace))
            value = delegated ? ParseExpression() : ParseExpressionList();
        if (delegated && value is null)
        {
            AddDiagnostic("LA1000", "Expected expression after 'yield from'.", start);
            return null;
        }
        return new YieldExpressionSyntax(value, delegated, value is null ? SpanOf(start) : Merge(start, value.Span));
    }
}
