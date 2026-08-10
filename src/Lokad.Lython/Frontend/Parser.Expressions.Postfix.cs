using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        if (expression is null)
        {
            return null;
        }

        while (true)
        {
            IPostfixParser? matchingParser = CurrentToken switch
            {
                Token.Dot => MemberPostfixParser.Instance,
                Token.OpenParen => CallPostfixParser.Instance,
                Token.OpenBracket => SubscriptPostfixParser.Instance,
                _ => null,
            };

            if (matchingParser is null)
            {
                return expression;
            }

            expression = matchingParser.Parse(this, expression);
            if (expression is null)
            {
                return null;
            }
        }
    }

    private interface IPostfixParser
    {
        /// <summary>Consumes one postfix form and combines it with its target expression.</summary>
        ExpressionSyntax? Parse(Parser parser, ExpressionSyntax target);
    }

    private sealed class MemberPostfixParser : IPostfixParser
    {
        public static readonly MemberPostfixParser Instance = new();

        public ExpressionSyntax? Parse(Parser parser, ExpressionSyntax target)
        {
            var dotToken = parser.ReadToken();
            if (!parser.TryReadMemberName(out var memberToken))
            {
                parser.AddDiagnostic("LA1005", "Expected attribute name after '.'.", dotToken);
                return null;
            }

            return new MemberExpressionSyntax(
                target,
                parser.IdentifierText(memberToken),
                Merge(target.Span, parser.SpanOf(memberToken)));
        }
    }

    private sealed class CallPostfixParser : IPostfixParser
    {
        public static readonly CallPostfixParser Instance = new();

        public ExpressionSyntax? Parse(Parser parser, ExpressionSyntax target)
        {
            var openParenToken = parser.ReadToken();
            var arguments = new List<CallArgumentSyntax>();
            parser.SkipGroupedExpressionTrivia();

            if (parser.CurrentToken != Token.CloseParen)
            {
                var sawKeywordArgument = false;
                while (true)
                {
                    var form = CallArgumentForm.Positional;
                    if (parser.CurrentToken == Token.StarStar)
                    {
                        parser.ReadToken();
                        form = CallArgumentForm.StarredDictionary;
                        sawKeywordArgument = true;
                    }
                    else if (parser.CurrentToken == Token.Star)
                    {
                        if (sawKeywordArgument)
                        {
                            parser.AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", parser._position);
                            return null;
                        }

                        parser.ReadToken();
                        form = CallArgumentForm.StarredList;
                    }
                    else if (IsNameToken(parser.CurrentToken) && parser.PeekToken(1) == Token.Assign)
                    {
                        var nameToken = parser.ReadToken();
                        parser.ReadToken();
                        form = CallArgumentForm.Keyword(parser.IdentifierText(nameToken));
                        sawKeywordArgument = true;
                    }
                    else if (sawKeywordArgument)
                    {
                        parser.AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", parser._position);
                        return null;
                    }

                    var argument = parser.ParseExpression();
                    if (argument is null)
                    {
                        return null;
                    }

                    parser.SkipGroupedExpressionTrivia();
                    if (parser.CurrentToken == Token.For && form.Kind == CallArgumentKind.Positional)
                    {
                        if (!parser.TryParseComprehensionClauses(out var clauses, out _))
                        {
                            return null;
                        }

                        argument = new GeneratorExpressionSyntax(
                            argument,
                            clauses,
                            Merge(argument.Span, clauses[^1].Span));
                    }
                    parser.SkipGroupedExpressionTrivia();

                    arguments.Add(new CallArgumentSyntax(form, argument));

                    if (parser.CurrentToken != Token.Comma)
                    {
                        break;
                    }

                    parser.ReadToken();
                    parser.SkipGroupedExpressionTrivia();
                    if (parser.CurrentToken == Token.CloseParen)
                    {
                        break;
                    }
                }
            }

            parser.SkipGroupedExpressionTrivia();
            if (!parser.TryRead(Token.CloseParen, out var closeParenToken))
            {
                parser.AddDiagnostic("LA1006", "Expected ')' after call arguments.", openParenToken);
                return null;
            }

            return new CallExpressionSyntax(
                target,
                arguments,
                Merge(target.Span, parser.SpanOf(closeParenToken)));
        }
    }

    private sealed class SubscriptPostfixParser : IPostfixParser
    {
        public static readonly SubscriptPostfixParser Instance = new();

        public ExpressionSyntax? Parse(Parser parser, ExpressionSyntax target)
        {
            var openBracketToken = parser.ReadToken();
            parser.SkipGroupedExpressionTrivia();
            ExpressionSyntax? start = null;
            if (parser.CurrentToken != Token.Colon)
            {
                start = parser.ParseExpression();
                if (start is null)
                {
                    parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                    return null;
                }
                parser.SkipGroupedExpressionTrivia();
            }

            if (parser.CurrentToken == Token.Colon)
            {
                parser.ReadToken();
                parser.SkipGroupedExpressionTrivia();

                ExpressionSyntax? end = null;
                if (parser.CurrentToken != Token.CloseBracket && parser.CurrentToken != Token.Colon)
                {
                    end = parser.ParseExpression();
                    if (end is null)
                    {
                        parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                        return null;
                    }
                    parser.SkipGroupedExpressionTrivia();
                }

                ExpressionSyntax? step = null;
                if (parser.CurrentToken == Token.Colon)
                {
                    parser.ReadToken();
                    parser.SkipGroupedExpressionTrivia();
                    if (parser.CurrentToken != Token.CloseBracket)
                    {
                        step = parser.ParseExpression();
                        if (step is null)
                        {
                            parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                            return null;
                        }
                        parser.SkipGroupedExpressionTrivia();
                    }
                }

                parser.SkipGroupedExpressionTrivia();
                if (!parser.TryRead(Token.CloseBracket, out var closeSliceToken))
                {
                    parser.AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                    return null;
                }

                return new SliceExpressionSyntax(
                    target,
                    start,
                    end,
                    step,
                    Merge(target.Span, parser.SpanOf(closeSliceToken)));
            }

            if (start is null)
            {
                parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                return null;
            }

            if (parser.CurrentToken == Token.Comma)
            {
                var items = new List<ExpressionSyntax> { start };
                while (parser.CurrentToken == Token.Comma)
                {
                    parser.ReadToken();
                    parser.SkipGroupedExpressionTrivia();
                    if (parser.CurrentToken == Token.CloseBracket)
                    {
                        break;
                    }

                    var next = parser.ParseExpression();
                    if (next is null)
                    {
                        parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                        return null;
                    }
                    parser.SkipGroupedExpressionTrivia();

                    items.Add(next);
                }

                start = new TupleLiteralExpressionSyntax(
                    items,
                    Enumerable.Repeat(false, items.Count).ToArray(),
                    Merge(items[0].Span, items[^1].Span));
            }

            parser.SkipGroupedExpressionTrivia();
            if (!parser.TryRead(Token.CloseBracket, out var closeBracketToken))
            {
                parser.AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                return null;
            }

            return new SubscriptExpressionSyntax(
                target,
                start,
                Merge(target.Span, parser.SpanOf(closeBracketToken)));
        }
    }
}
