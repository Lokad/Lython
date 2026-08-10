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

    private ExpressionSyntax? ParseDictLiteral()
    {
        var openBrace = ReadToken();
        var dictionaryItems = new List<DictionaryDisplayItemSyntax>();
        var setItems = new List<CollectionDisplayItemSyntax>();
        var displayKind = default(BraceDisplayKind?);
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
            return null;
        }

        while (CurrentToken != Token.CloseBrace)
        {
            if (CurrentToken == Token.End)
            {
                AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
                return null;
            }

            if (!TryParseDisplayItem(out var completedComprehension))
            {
                return null;
            }

            if (completedComprehension is not null)
            {
                return completedComprehension;
            }

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
            SkipGroupedExpressionTrivia();
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseBrace, out var closeBrace))
        {
            AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", openBrace);
            return null;
        }

        // Python reserves the empty brace display for a dictionary; a set must
        // establish its kind with at least one value or starred value.
        return displayKind == BraceDisplayKind.Set
            ? new SetLiteralExpressionSyntax(setItems, Merge(SpanOf(openBrace), SpanOf(closeBrace)))
            : new DictLiteralExpressionSyntax(dictionaryItems, Merge(SpanOf(openBrace), SpanOf(closeBrace)));

        bool TryParseDisplayItem(out ExpressionSyntax? completedComprehension)
        {
            completedComprehension = null;
            if (CurrentToken == Token.StarStar)
            {
                return TryParseDictionaryUnpacking();
            }

            var isSetUnpacking = CurrentToken == Token.Star;
            var setUnpackingSpan = default(LythonSourceSpan?);
            if (isSetUnpacking)
            {
                if (displayKind == BraceDisplayKind.Dictionary)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'set unpacking in dictionary display'.", _position);
                    return false;
                }

                setUnpackingSpan = SpanOf(ReadToken());
            }

            var key = ParseExpression();
            if (key is null)
            {
                return false;
            }
            SkipGroupedExpressionTrivia();

            if (TryRead(Token.Colon, out var colonToken))
            {
                if (isSetUnpacking || displayKind == BraceDisplayKind.Set)
                {
                    AddDiagnostic("LA1024", "Cannot mix set items with dictionary entries.", key.Span);
                    return false;
                }

                SkipGroupedExpressionTrivia();
                var value = ParseExpression();
                if (value is null)
                {
                    AddDiagnostic("LA1025", "Expected value in dictionary literal.", colonToken);
                    return false;
                }
                SkipGroupedExpressionTrivia();

                displayKind = BraceDisplayKind.Dictionary;
                dictionaryItems.Add(new DictionaryKeyValueItemSyntax(key, value, Merge(key.Span, value.Span)));
                if (CurrentToken == Token.For)
                {
                    completedComprehension = ParseDictionaryComprehension(key, value);
                    return completedComprehension is not null;
                }

                return true;
            }

            if (displayKind == BraceDisplayKind.Dictionary)
            {
                AddDiagnostic("LA1024", "Expected ':' in dictionary literal.", key.Span);
                return false;
            }

            if (CurrentToken == Token.For)
            {
                if (isSetUnpacking || setItems.Count != 0)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'iterable unpacking in comprehension'.", _position);
                    return false;
                }

                completedComprehension = ParseSetComprehension(key);
                return completedComprehension is not null;
            }

            displayKind = BraceDisplayKind.Set;
            setItems.Add(isSetUnpacking
                ? new CollectionUnpackingItemSyntax(key, Merge(setUnpackingSpan ?? key.Span, key.Span))
                : new CollectionValueItemSyntax(key));
            return true;
        }

        bool TryParseDictionaryUnpacking()
        {
            if (displayKind == BraceDisplayKind.Set)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'dictionary unpacking in set display'.", _position);
                return false;
            }

            var unpackToken = ReadToken();
            SkipGroupedExpressionTrivia();
            var mapping = ParseExpression();
            if (mapping is null)
            {
                AddDiagnostic("LA1025", "Expected mapping after '**' in dictionary literal.", unpackToken);
                return false;
            }

            displayKind = BraceDisplayKind.Dictionary;
            dictionaryItems.Add(new DictionaryUnpackingItemSyntax(mapping, Merge(SpanOf(unpackToken), mapping.Span)));
            SkipGroupedExpressionTrivia();
            return true;
        }

        ExpressionSyntax? ParseDictionaryComprehension(ExpressionSyntax key, ExpressionSyntax value)
        {
            // A comprehension consumes the whole display and is legal only as
            // its first entry; earlier unpacking or entries cannot be combined.
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

        ExpressionSyntax? ParseSetComprehension(ExpressionSyntax item)
        {
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
                item,
                clauses,
                Merge(SpanOf(openBrace), SpanOf(closeComprehension)));
        }
    }

    private enum BraceDisplayKind
    {
        Dictionary,
        Set
    }
}
