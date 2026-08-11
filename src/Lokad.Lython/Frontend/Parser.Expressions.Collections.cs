using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParseListLiteral()
    {
        var openBracket = ReadToken();
        var items = new List<CollectionDisplayItemSyntax>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1021", "Unexpected end of file while parsing list literal; expected ']'.", openBracket);
            return null;
        }

        if (CurrentToken != Token.CloseBracket)
        {
            while (true)
            {
                if (CurrentToken == Token.End)
                {
                    AddDiagnostic("LA1021", "Unexpected end of file while parsing list literal; expected ']'.", openBracket);
                    return null;
                }

                var isUnpacking = CurrentToken == Token.Star;
                var unpackingSpan = default(LythonSourceSpan?);
                if (isUnpacking)
                {
                    unpackingSpan = SpanOf(ReadToken());
                }

                var item = ParseExpression();
                if (item is null)
                {
                    return null;
                }

                items.Add(isUnpacking
                    ? new CollectionUnpackingItemSyntax(item, Merge(unpackingSpan ?? item.Span, item.Span))
                    : new CollectionValueItemSyntax(item));
                SkipGroupedExpressionTrivia();

                if (CurrentToken == Token.For)
                {
                    if (items.Count != 1 || isUnpacking)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'iterable unpacking in comprehension'.", _position);
                        return null;
                    }

                    if (!TryParseComprehensionClauses(out var clauses, out _))
                    {
                        return null;
                    }

                    SkipGroupedExpressionTrivia();
                    if (!TryRead(Token.CloseBracket, out var closeComprehension))
                    {
                        AddDiagnostic("LA1021", "Expected ']' after list literal.", openBracket);
                        return null;
                    }

                    return new ListComprehensionExpressionSyntax(
                        item,
                        clauses,
                        Merge(SpanOf(openBracket), SpanOf(closeComprehension)));
                }

                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                SkipGroupedExpressionTrivia();
                if (CurrentToken == Token.CloseBracket)
                {
                    break;
                }
            }
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseBracket, out var closeBracket))
        {
            AddDiagnostic("LA1021", "Expected ']' after list literal.", openBracket);
            return null;
        }

        return new ListLiteralExpressionSyntax(items, Merge(SpanOf(openBracket), SpanOf(closeBracket)));
    }

    private ExpressionSyntax? ParseTupleOrParenthesized()
    {
        var openParen = ReadToken();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1008", "Unexpected end of file while parsing parenthesized expression; expected ')'.", openParen);
            return null;
        }

        if (CurrentToken == Token.CloseParen)
        {
            var closeEmpty = ReadToken();
            return new TupleLiteralExpressionSyntax(Array.Empty<CollectionDisplayItemSyntax>(), Merge(SpanOf(openParen), SpanOf(closeEmpty)));
        }

        var firstIsUnpacking = CurrentToken == Token.Star;
        var firstUnpackingSpan = default(LythonSourceSpan?);
        if (firstIsUnpacking)
        {
            firstUnpackingSpan = SpanOf(ReadToken());
        }

        var first = ParseExpression();
        if (first is null)
        {
            return null;
        }
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.Comma)
        {
            if (firstIsUnpacking)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'bare starred expression'.", first.Span);
                return null;
            }

            if (CurrentToken == Token.For)
            {
                if (!TryParseComprehensionClauses(out var clauses, out _))
                {
                    return null;
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseParen, out var closeComprehension))
                {
                    AddDiagnostic("LA1045", "Expected ')' after generator expression.", openParen);
                    return null;
                }

                return new GeneratorExpressionSyntax(
                    first,
                    clauses,
                    Merge(SpanOf(openParen), SpanOf(closeComprehension)));
            }

            SkipGroupedExpressionTrivia();
            if (!TryRead(Token.CloseParen, out var closeParen))
            {
                AddDiagnostic("LA1008", "Expected ')' after expression.", first.Span);
                return null;
            }

            return new ParenthesizedExpressionSyntax(first, Merge(first.Span, SpanOf(closeParen)));
        }

        var items = new List<CollectionDisplayItemSyntax>
        {
            firstIsUnpacking
                ? new CollectionUnpackingItemSyntax(first, Merge(firstUnpackingSpan ?? first.Span, first.Span))
                : new CollectionValueItemSyntax(first)
        };
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            SkipGroupedExpressionTrivia();
            if (CurrentToken == Token.CloseParen)
            {
                break;
            }

            if (CurrentToken == Token.End)
            {
                AddDiagnostic("LA1045", "Unexpected end of file while parsing tuple literal; expected ')'.", openParen);
                return null;
            }

            var isUnpacking = CurrentToken == Token.Star;
            var unpackingSpan = default(LythonSourceSpan?);
            if (isUnpacking)
            {
                unpackingSpan = SpanOf(ReadToken());
            }

            var item = ParseExpression();
            if (item is null)
            {
                return null;
            }

            items.Add(isUnpacking
                ? new CollectionUnpackingItemSyntax(item, Merge(unpackingSpan ?? item.Span, item.Span))
                : new CollectionValueItemSyntax(item));
            SkipGroupedExpressionTrivia();
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseParen, out var closeTuple))
        {
            AddDiagnostic("LA1045", "Expected ')' after tuple literal.", openParen);
            return null;
        }

        return new TupleLiteralExpressionSyntax(items, Merge(SpanOf(openParen), SpanOf(closeTuple)));
    }
}
