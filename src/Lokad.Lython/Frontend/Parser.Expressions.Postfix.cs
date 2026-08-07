using System.Text;
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
            if (CurrentToken == Token.Dot)
            {
                var dotToken = ReadToken();
                if (!TryReadMemberName(out var memberToken))
                {
                    AddDiagnostic("LA1005", "Expected attribute name after '.'.", dotToken);
                    return null;
                }

                expression = new MemberExpressionSyntax(
                    expression,
                    IdentifierText(memberToken),
                    Merge(expression.Span, SpanOf(memberToken)));
                continue;
            }

            if (CurrentToken == Token.OpenParen)
            {
                var openParenToken = ReadToken();
                var arguments = new List<CallArgumentSyntax>();
                SkipGroupedExpressionTrivia();

                if (CurrentToken != Token.CloseParen)
                {
                    var sawKeywordArgument = false;
                    while (true)
                    {
                        var form = CallArgumentForm.Positional;
                        if (CurrentToken == Token.StarStar)
                        {
                            ReadToken();
                            form = CallArgumentForm.StarredDictionary;
                            sawKeywordArgument = true;
                        }
                        else if (CurrentToken == Token.Star)
                        {
                            if (sawKeywordArgument)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", _position);
                                return null;
                            }

                            ReadToken();
                            form = CallArgumentForm.StarredList;
                        }
                        else if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Assign)
                        {
                            var nameToken = ReadToken();
                            ReadToken();
                            form = CallArgumentForm.Keyword(IdentifierText(nameToken));
                            sawKeywordArgument = true;
                        }
                        else if (sawKeywordArgument)
                        {
                            AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", _position);
                            return null;
                        }

                        var argument = ParseExpression();
                        if (argument is null)
                        {
                            return null;
                        }

                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.For && form.Kind == CallArgumentKind.Positional)
                        {
                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            argument = new GeneratorExpressionSyntax(
                                argument,
                                clauses,
                                Merge(argument.Span, clauses[^1].Span));
                        }
                        SkipGroupedExpressionTrivia();

                        arguments.Add(new CallArgumentSyntax(form, argument));

                        if (CurrentToken != Token.Comma)
                        {
                            break;
                        }

                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.CloseParen)
                        {
                            break;
                        }
                    }
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseParen, out var closeParenToken))
                {
                    AddDiagnostic("LA1006", "Expected ')' after call arguments.", openParenToken);
                    return null;
                }

                expression = new CallExpressionSyntax(
                    expression,
                    arguments,
                    Merge(expression.Span, SpanOf(closeParenToken)));
                continue;
            }

            if (CurrentToken == Token.OpenBracket)
            {
                var openBracketToken = ReadToken();
                SkipGroupedExpressionTrivia();
                ExpressionSyntax? start = null;
                if (CurrentToken != Token.Colon)
                {
                    start = ParseExpression();
                    if (start is null)
                    {
                        AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                        return null;
                    }
                    SkipGroupedExpressionTrivia();
                }

                if (CurrentToken == Token.Colon)
                {
                    ReadToken();
                    SkipGroupedExpressionTrivia();

                    ExpressionSyntax? end = null;
                    if (CurrentToken != Token.CloseBracket && CurrentToken != Token.Colon)
                    {
                        end = ParseExpression();
                        if (end is null)
                        {
                            AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();
                    }

                    ExpressionSyntax? step = null;
                    if (CurrentToken == Token.Colon)
                    {
                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken != Token.CloseBracket)
                        {
                            step = ParseExpression();
                            if (step is null)
                            {
                                AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                                return null;
                            }
                            SkipGroupedExpressionTrivia();
                        }
                    }

                    SkipGroupedExpressionTrivia();
                    if (!TryRead(Token.CloseBracket, out var closeSliceToken))
                    {
                        AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                        return null;
                    }

                    expression = new SliceExpressionSyntax(
                        expression,
                        start,
                        end,
                        step,
                        Merge(expression.Span, SpanOf(closeSliceToken)));
                    continue;
                }

                if (start is null)
                {
                    AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                    return null;
                }

                if (CurrentToken == Token.Comma)
                {
                    var items = new List<ExpressionSyntax> { start };
                    while (CurrentToken == Token.Comma)
                    {
                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.CloseBracket)
                        {
                            break;
                        }

                        var next = ParseExpression();
                        if (next is null)
                        {
                            AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();

                        items.Add(next);
                    }

                    start = new TupleLiteralExpressionSyntax(
                        items,
                        Enumerable.Repeat(false, items.Count).ToArray(),
                        Merge(items[0].Span, items[^1].Span));
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseBracket, out var closeBracketToken))
                {
                    AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                    return null;
                }

                expression = new SubscriptExpressionSyntax(
                    expression,
                    start,
                    Merge(expression.Span, SpanOf(closeBracketToken)));
                continue;
            }

            break;
        }

        return expression;
    }
}
