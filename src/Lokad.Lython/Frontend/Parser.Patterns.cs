using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private StatementSyntax? ParseMatchStatement()
    {
        var matchToken = ReadToken();
        var subject = ParseExpression();
        if (subject is null)
        {
            AddDiagnostic("LA1080", "Expected subject expression after 'match'.", matchToken);
            return null;
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1081", "Expected ':' after match subject.", subject.Span);
            return null;
        }

        if (!TryRead(Token.Eol, out _))
        {
            AddDiagnostic("LA1082", "Expected end-of-line before match cases.", _position);
            return null;
        }

        if (!TryRead(Token.Indent, out _))
        {
            AddDiagnostic("LA1083", "Expected indented case block after 'match'.", _position);
            return null;
        }

        var cases = new List<MatchCaseSyntax>();
        SkipEndOfLines();

        while (CurrentToken is not Token.Dedent and not Token.End)
        {
            if (CurrentToken != Token.Case)
            {
                AddDiagnostic("LA1084", "Expected 'case' inside match block.", _position);
                return null;
            }

            var parsedCase = ParseMatchCase();
            if (parsedCase is null)
            {
                return null;
            }

            cases.Add(parsedCase);
            SkipEndOfLines();
        }

        if (cases.Count == 0)
        {
            AddDiagnostic("LA1085", "Expected at least one case in match block.", _position);
            return null;
        }

        if (!TryRead(Token.Dedent, out _))
        {
            AddDiagnostic("LA1086", "Expected dedent after match block.", _position);
            return null;
        }

        return new MatchStatementSyntax(subject, cases, Merge(SpanOf(matchToken), cases[^1].Span));
    }

    private MatchCaseSyntax? ParseMatchCase()
    {
        var caseToken = ReadToken();
        var pattern = ParsePattern();
        if (pattern is null)
        {
            AddDiagnostic("LA1087", "Expected pattern after 'case'.", caseToken);
            return null;
        }

        ExpressionSyntax? guard = null;
        if (CurrentToken == Token.If)
        {
            ReadToken();
            guard = ParseExpression();
            if (guard is null)
            {
                AddDiagnostic("LA1088", "Expected guard expression after 'if' in case.", pattern.Span);
                return null;
            }
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1089", "Expected ':' after case pattern.", pattern.Span);
            return null;
        }

        var body = ParseSuite("LA1090", "Expected indented block after 'case'.");
        if (body is null)
        {
            return null;
        }

        return new MatchCaseSyntax(pattern, guard, body, Merge(SpanOf(caseToken), body[^1].Span));
    }

    private PatternSyntax? ParsePattern()
    {
        var pattern = ParseOrPattern();
        if (pattern is null)
        {
            return null;
        }

        if (CurrentToken == Token.As)
        {
            ReadToken();
            if (!TryReadNameToken(out var nameToken))
            {
                AddDiagnostic("LA1091", "Expected capture name after 'as' in pattern.", _position);
                return null;
            }

            var name = IdentifierText(nameToken);
            if (name == "_")
            {
                AddDiagnostic("LA1092", "Wildcard '_' cannot be used as an 'as' capture target.", nameToken);
                return null;
            }

            pattern = new MatchAsPatternSyntax(pattern, name, Merge(pattern.Span, SpanOf(nameToken)));
        }

        return pattern;
    }

    private PatternSyntax? ParseOrPattern()
    {
        var patterns = new List<PatternSyntax>();
        var first = ParseClosedPattern();
        if (first is null)
        {
            return null;
        }

        patterns.Add(first);
        while (CurrentToken == Token.Pipe)
        {
            ReadToken();
            var next = ParseClosedPattern();
            if (next is null)
            {
                AddDiagnostic("LA1093", "Expected pattern after '|'.", _position);
                return null;
            }

            patterns.Add(next);
        }

        return patterns.Count == 1
            ? patterns[0]
            : new MatchOrPatternSyntax(patterns, Merge(patterns[0].Span, patterns[^1].Span));
    }

    private PatternSyntax? ParseClosedPattern()
    {
        if (CurrentToken == Token.OpenBracket)
        {
            return ParseBracketSequencePattern();
        }

        if (CurrentToken == Token.OpenBrace)
        {
            return ParseMappingPattern();
        }

        if (CurrentToken == Token.OpenParen)
        {
            return ParseParenthesizedPattern();
        }

        if (CurrentToken == Token.True)
        {
            var token = ReadToken();
            return new MatchSingletonPatternSyntax(MatchSingletonKind.True, SpanOf(token));
        }

        if (CurrentToken == Token.False)
        {
            var token = ReadToken();
            return new MatchSingletonPatternSyntax(MatchSingletonKind.False, SpanOf(token));
        }

        if (CurrentToken == Token.None)
        {
            var token = ReadToken();
            return new MatchSingletonPatternSyntax(MatchSingletonKind.None, SpanOf(token));
        }

        if (TryParseLiteralPatternExpression(out var literalExpression))
        {
            return new MatchValuePatternSyntax(literalExpression, literalExpression.Span);
        }

        if (IsNameToken(CurrentToken))
        {
            if (!TryReadNameToken(out var nameToken))
            {
                return null;
            }

            var name = IdentifierText(nameToken);
            var expression = ParseNameOrAttributeExpressionFromName(nameToken, name);
            if (expression is IdentifierExpressionSyntax identifier)
            {
                if (CurrentToken == Token.OpenParen)
                {
                    return ParseClassPattern(expression);
                }

                return name == "_"
                    ? new MatchWildcardPatternSyntax(identifier.Span)
                    : new MatchCapturePatternSyntax(name, identifier.Span);
            }

            if (CurrentToken == Token.OpenParen)
            {
                return ParseClassPattern(expression);
            }

            return new MatchValuePatternSyntax(expression, expression.Span);
        }

        AddDiagnostic("LA1094", "Expected pattern.", _position);
        return null;
    }

    private PatternSyntax? ParseParenthesizedPattern()
    {
        var openParen = ReadToken();
        if (CurrentToken == Token.CloseParen)
        {
            var closeEmpty = ReadToken();
            return new MatchSequencePatternSyntax(Array.Empty<PatternSyntax>(), Merge(SpanOf(openParen), SpanOf(closeEmpty)));
        }

        var first = ParseMaybeStarPattern();
        if (first is null)
        {
            AddDiagnostic("LA1094", "Expected pattern.", openParen);
            return null;
        }

        if (CurrentToken != Token.Comma)
        {
            if (!TryRead(Token.CloseParen, out var closeGroup))
            {
                AddDiagnostic("LA1095", "Expected ')' after grouped pattern.", openParen);
                return null;
            }

            return first;
        }

        var items = new List<PatternSyntax> { first };
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            if (CurrentToken == Token.CloseParen)
            {
                break;
            }

            var item = ParseMaybeStarPattern();
            if (item is null)
            {
                AddDiagnostic("LA1094", "Expected pattern.", _position);
                return null;
            }

            items.Add(item);
        }

        if (!TryRead(Token.CloseParen, out var closeTuple))
        {
            AddDiagnostic("LA1095", "Expected ')' after sequence pattern.", openParen);
            return null;
        }

        if (items.OfType<MatchStarPatternSyntax>().Count() > 1)
        {
            AddDiagnostic("LA1096", "Sequence pattern cannot contain multiple starred patterns.", openParen);
            return null;
        }

        return new MatchSequencePatternSyntax(items, Merge(SpanOf(openParen), SpanOf(closeTuple)));
    }

    private PatternSyntax? ParseBracketSequencePattern()
    {
        var openBracket = ReadToken();
        var items = new List<PatternSyntax>();
        if (CurrentToken != Token.CloseBracket)
        {
            while (true)
            {
                var item = ParseMaybeStarPattern();
                if (item is null)
                {
                    AddDiagnostic("LA1094", "Expected pattern.", openBracket);
                    return null;
                }

                items.Add(item);
                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                if (CurrentToken == Token.CloseBracket)
                {
                    break;
                }
            }
        }

        if (!TryRead(Token.CloseBracket, out var closeBracket))
        {
            AddDiagnostic("LA1097", "Expected ']' after sequence pattern.", openBracket);
            return null;
        }

        if (items.OfType<MatchStarPatternSyntax>().Count() > 1)
        {
            AddDiagnostic("LA1096", "Sequence pattern cannot contain multiple starred patterns.", openBracket);
            return null;
        }

        return new MatchSequencePatternSyntax(items, Merge(SpanOf(openBracket), SpanOf(closeBracket)));
    }

    private PatternSyntax? ParseMaybeStarPattern()
    {
        if (CurrentToken != Token.Star)
        {
            return ParsePattern();
        }

        var starToken = ReadToken();
        if (!TryReadNameToken(out var targetToken))
        {
            AddDiagnostic("LA1098", "Expected capture name after '*' in sequence pattern.", starToken);
            return null;
        }

        var name = IdentifierText(targetToken);
        return name == "_"
            ? new MatchStarPatternSyntax(null, Merge(SpanOf(starToken), SpanOf(targetToken)))
            : new MatchStarPatternSyntax(name, Merge(SpanOf(starToken), SpanOf(targetToken)));
    }

    private PatternSyntax? ParseMappingPattern()
    {
        var openBrace = ReadToken();
        var items = new List<MatchMappingPatternItemSyntax>();
        string? restName = null;

        if (CurrentToken != Token.CloseBrace)
        {
            while (true)
            {
                if (CurrentToken == Token.StarStar)
                {
                    ReadToken();
                    if (!TryReadNameToken(out var restToken))
                    {
                        AddDiagnostic("LA1099", "Expected capture name after '**' in mapping pattern.", _position);
                        return null;
                    }

                    restName = IdentifierText(restToken);
                    if (restName == "_")
                    {
                        AddDiagnostic("LA1100", "Wildcard '_' cannot be used as a mapping rest capture.", restToken);
                        return null;
                    }
                    break;
                }

                var key = ParseMappingPatternKey();
                if (key is null)
                {
                    AddDiagnostic("LA1101", "Expected literal or dotted-name key in mapping pattern.", _position);
                    return null;
                }

                if (!TryRead(Token.Colon, out _))
                {
                    AddDiagnostic("LA1102", "Expected ':' after mapping pattern key.", key.Span);
                    return null;
                }

                var valuePattern = ParsePattern();
                if (valuePattern is null)
                {
                    AddDiagnostic("LA1094", "Expected pattern.", _position);
                    return null;
                }

                items.Add(new MatchMappingPatternItemSyntax(key, valuePattern));
                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                if (CurrentToken == Token.CloseBrace)
                {
                    break;
                }
            }
        }

        if (!TryRead(Token.CloseBrace, out var closeBrace))
        {
            AddDiagnostic("LA1103", "Expected '}' after mapping pattern.", openBrace);
            return null;
        }

        return new MatchMappingPatternSyntax(items, restName, Merge(SpanOf(openBrace), SpanOf(closeBrace)));
    }

    private PatternSyntax? ParseClassPattern(ExpressionSyntax classExpression)
    {
        var openParen = ReadToken();
        var positionalPatterns = new List<PatternSyntax>();
        var keywordPatterns = new List<MatchClassKeywordPatternSyntax>();
        var sawKeyword = false;

        if (CurrentToken != Token.CloseParen)
        {
            while (true)
            {
                if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Assign)
                {
                    sawKeyword = true;
                    var nameToken = ReadToken();
                    ReadToken();
                    var keywordPattern = ParsePattern();
                    if (keywordPattern is null)
                    {
                        AddDiagnostic("LA1094", "Expected pattern.", _position);
                        return null;
                    }

                    keywordPatterns.Add(new MatchClassKeywordPatternSyntax(IdentifierText(nameToken), keywordPattern));
                }
                else
                {
                    if (sawKeyword)
                    {
                        AddDiagnostic("LA1104", "Positional pattern cannot appear after keyword pattern in class pattern.", _position);
                        return null;
                    }

                    var positionalPattern = ParsePattern();
                    if (positionalPattern is null)
                    {
                        AddDiagnostic("LA1094", "Expected pattern.", _position);
                        return null;
                    }

                    positionalPatterns.Add(positionalPattern);
                }

                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                if (CurrentToken == Token.CloseParen)
                {
                    break;
                }
            }
        }

        if (!TryRead(Token.CloseParen, out var closeParen))
        {
            AddDiagnostic("LA1105", "Expected ')' after class pattern.", openParen);
            return null;
        }

        return new MatchClassPatternSyntax(
            classExpression,
            positionalPatterns,
            keywordPatterns,
            Merge(classExpression.Span, SpanOf(closeParen)));
    }

    private ExpressionSyntax? ParseMappingPatternKey()
    {
        if (TryParseLiteralPatternExpression(out var literal))
        {
            return literal;
        }

        if (!IsNameToken(CurrentToken))
        {
            return null;
        }

        if (!TryReadNameToken(out var nameToken))
        {
            return null;
        }

        var expression = ParseNameOrAttributeExpressionFromName(nameToken, IdentifierText(nameToken));
        return expression is MemberExpressionSyntax ? expression : null;
    }

    private bool TryParseLiteralPatternExpression([MaybeNullWhen(false)] out ExpressionSyntax expression)
    {
        expression = null;

        if (CurrentToken is Token.String or Token.Integer or Token.Float or Token.True or Token.False or Token.None)
        {
            var parsed = ParsePrimaryExpression();
            if (parsed is null)
            {
                return false;
            }

            expression = parsed;
            return true;
        }

        if (IsNameToken(CurrentToken) && PeekToken(1) == Token.String)
        {
            var parsed = ParsePrimaryExpression();
            if (parsed is BytesLiteralExpressionSyntax)
            {
                expression = parsed;
                return true;
            }

            return false;
        }

        if ((CurrentToken is Token.Plus or Token.Minus) && (PeekToken(1) is Token.Integer or Token.Float))
        {
            var parsed = ParseUnaryExpression();
            if (parsed is UnaryExpressionSyntax)
            {
                expression = parsed;
                return true;
            }
        }

        return false;
    }

    private ExpressionSyntax ParseNameOrAttributeExpressionFromName(int nameToken, string name)
    {
        ExpressionSyntax expression = new IdentifierExpressionSyntax(name, SpanOf(nameToken));
        while (CurrentToken == Token.Dot)
        {
            ReadToken();
            if (!TryReadMemberName(out var memberToken))
            {
                AddDiagnostic("LA1005", "Expected attribute name after '.'.", _position);
                break;
            }

            expression = new MemberExpressionSyntax(
                expression,
                IdentifierText(memberToken),
                Merge(expression.Span, SpanOf(memberToken)));
        }

        return expression;
    }

}
