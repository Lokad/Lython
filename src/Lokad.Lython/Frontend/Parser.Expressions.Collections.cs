using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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
            return new TupleLiteralExpressionSyntax(Array.Empty<ExpressionSyntax>(), Array.Empty<bool>(), Merge(SpanOf(openParen), SpanOf(closeEmpty)));
        }

        var firstIsUnpacking = CurrentToken == Token.Star;
        if (firstIsUnpacking)
        {
            ReadToken();
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

        var items = new List<ExpressionSyntax> { first };
        var unpackingFlags = new List<bool> { firstIsUnpacking };
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
            if (isUnpacking)
            {
                ReadToken();
            }

            var item = ParseExpression();
            if (item is null)
            {
                return null;
            }

            items.Add(item);
            unpackingFlags.Add(isUnpacking);
            SkipGroupedExpressionTrivia();
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseParen, out var closeTuple))
        {
            AddDiagnostic("LA1045", "Expected ')' after tuple literal.", openParen);
            return null;
        }

        return new TupleLiteralExpressionSyntax(items, unpackingFlags, Merge(SpanOf(openParen), SpanOf(closeTuple)));
    }

    private ExpressionSyntax? ParseDictLiteral()
    {
        var openBrace = ReadToken();
        var dictionaryItems = new List<DictionaryDisplayItemSyntax>();
        var setItems = new List<ExpressionSyntax>();
        var setUnpackingFlags = new List<bool>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
            return null;
        }

        if (CurrentToken != Token.CloseBrace)
        {
            while (true)
            {
                if (CurrentToken == Token.End)
                {
                    AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
                    return null;
                }

                if (CurrentToken == Token.StarStar)
                {
                    if (setItems.Count != 0)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'dictionary unpacking in set display'.", _position);
                        return null;
                    }

                    var unpackToken = ReadToken();
                    SkipGroupedExpressionTrivia();
                    var mapping = ParseExpression();
                    if (mapping is null)
                    {
                        AddDiagnostic("LA1025", "Expected mapping after '**' in dictionary literal.", unpackToken);
                        return null;
                    }

                    dictionaryItems.Add(new DictionaryUnpackingItemSyntax(
                        mapping,
                        Merge(SpanOf(unpackToken), mapping.Span)));
                    SkipGroupedExpressionTrivia();
                }
                else
                {
                    var isSetUnpacking = CurrentToken == Token.Star;
                    if (isSetUnpacking)
                    {
                        if (dictionaryItems.Count != 0)
                        {
                            AddDiagnostic("LA2000", "Unsupported Python construct 'set unpacking in dictionary display'.", _position);
                            return null;
                        }

                        ReadToken();
                    }

                    var key = ParseExpression();
                    if (key is null)
                    {
                        return null;
                    }
                    SkipGroupedExpressionTrivia();

                    if (TryRead(Token.Colon, out var colonToken))
                    {
                        if (isSetUnpacking || setItems.Count != 0)
                        {
                            AddDiagnostic("LA1024", "Cannot mix set items with dictionary entries.", key.Span);
                            return null;
                        }

                        SkipGroupedExpressionTrivia();
                        var value = ParseExpression();
                        if (value is null)
                        {
                            AddDiagnostic("LA1025", "Expected value in dictionary literal.", colonToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();

                        dictionaryItems.Add(new DictionaryKeyValueItemSyntax(
                            key,
                            value,
                            Merge(key.Span, value.Span)));

                        if (CurrentToken == Token.For)
                        {
                            if (dictionaryItems.Count != 1 || dictionaryItems[0] is not DictionaryKeyValueItemSyntax)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'comprehension'.", _position);
                                return null;
                            }

                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            SkipGroupedExpressionTrivia();
                            if (!TryRead(Token.CloseBrace, out var closeComprehension))
                            {
                                AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", openBrace);
                                return null;
                            }

                            return new DictComprehensionExpressionSyntax(
                                key,
                                value,
                                clauses,
                                Merge(SpanOf(openBrace), SpanOf(closeComprehension)));
                        }
                    }
                    else
                    {
                        if (dictionaryItems.Count != 0)
                        {
                            AddDiagnostic("LA1024", "Expected ':' in dictionary literal.", key.Span);
                            return null;
                        }

                        if (CurrentToken == Token.For)
                        {
                            if (isSetUnpacking || setItems.Count != 0)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'iterable unpacking in comprehension'.", _position);
                                return null;
                            }

                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            SkipGroupedExpressionTrivia();
                            if (!TryRead(Token.CloseBrace, out var closeComprehension))
                            {
                                AddDiagnostic("LA1026", "Expected '}' after set comprehension.", openBrace);
                                return null;
                            }

                            return new SetComprehensionExpressionSyntax(
                                key,
                                clauses,
                                Merge(SpanOf(openBrace), SpanOf(closeComprehension)));
                        }

                        setItems.Add(key);
                        setUnpackingFlags.Add(isSetUnpacking);
                    }
                }

                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                SkipGroupedExpressionTrivia();
                if (CurrentToken == Token.CloseBrace)
                {
                    break;
                }
            }
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseBrace, out var closeBrace))
        {
            AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", openBrace);
            return null;
        }

        return setItems.Count != 0
            ? new SetLiteralExpressionSyntax(setItems, setUnpackingFlags, Merge(SpanOf(openBrace), SpanOf(closeBrace)))
            : new DictLiteralExpressionSyntax(dictionaryItems, Merge(SpanOf(openBrace), SpanOf(closeBrace)));
    }
}
