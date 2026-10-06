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

        var postfixCount = 0;
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

            postfixCount++;
            if (postfixCount > MaxNestingDepth)
            {
                AddDiagnostic(
                    "LA0003",
                    $"Expression chain exceeds the maximum of {MaxNestingDepth} operands.",
                    _position);
                return null;
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
                var sawDictionaryUnpacking = false;
                while (true)
                {
                    var form = CallArgumentForm.Positional;
                    if (parser.CurrentToken == Token.StarStar)
                    {
                        parser.ReadToken();
                        form = CallArgumentForm.StarredDictionary;
                        sawKeywordArgument = true;
                        sawDictionaryUnpacking = true;
                    }
                    else if (parser.CurrentToken == Token.Star)
                    {
                        if (sawDictionaryUnpacking)
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
                        form = CallArgumentForm.Keyword(parser.RawIdentifierText(nameToken));
                        sawKeywordArgument = true;
                    }
                    else if (sawKeywordArgument)
                    {
                        parser.AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", parser._position);
                        return null;
                    }

                    var argument = parser.ParseNestedExpression(parser._position);
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
                // Python evaluates positional and * arguments before keyword
                // arguments, even when a * argument is written after one.
                arguments.Where(a => a.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList)
                    .Concat(arguments.Where(a => a.Kind is CallArgumentKind.Keyword or CallArgumentKind.StarredDictionary)).ToArray(),
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
            var items = new List<CollectionDisplayItemSyntax>();
            var tupleKey = false;
            while (true)
            {
                var unpacked = parser.CurrentToken == Token.Star;
                var unpackingSpan = unpacked ? parser.SpanOf(parser.ReadToken()) : default(LythonSourceSpan?);
                var item = ParseItem(parser, openBracketToken, allowSlice: !unpacked);
                if (item is null) return null;
                items.Add(unpacked
                    ? new CollectionUnpackingItemSyntax(item, Merge(unpackingSpan ?? item.Span, item.Span))
                    : new CollectionValueItemSyntax(item));
                tupleKey |= unpacked;
                parser.SkipGroupedExpressionTrivia();
                if (parser.CurrentToken != Token.Comma) break;
                tupleKey = true;
                parser.ReadToken();
                parser.SkipGroupedExpressionTrivia();
                if (parser.CurrentToken == Token.CloseBracket) break;
            }

            if (!parser.TryRead(Token.CloseBracket, out var closeBracketToken))
            {
                parser.AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                return null;
            }

            var span = Merge(target.Span, parser.SpanOf(closeBracketToken));
            if (!tupleKey && items[0].Expression is SliceValueExpressionSyntax slice)
                return new SliceExpressionSyntax(target, slice.Start, slice.End, slice.Step, span);
            var key = tupleKey
                ? new TupleLiteralExpressionSyntax(items, Merge(items[0].Span, items[^1].Span))
                : items[0].Expression;
            return new SubscriptExpressionSyntax(target, key, span);
        }

        private static ExpressionSyntax? ParseItem(Parser parser, int openBracketToken, bool allowSlice)
        {
            var firstToken = parser._position;
            ExpressionSyntax? start = null;
            if (parser.CurrentToken != Token.Colon || !allowSlice)
            {
                start = parser.ParseNestedExpression(parser._position);
                if (start is null)
                {
                    parser.AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                    return null;
                }
                parser.SkipGroupedExpressionTrivia();
            }
            if (parser.CurrentToken != Token.Colon || !allowSlice) return start;

            var lastSpan = parser.SpanOf(parser.ReadToken());
            parser.SkipGroupedExpressionTrivia();
            ExpressionSyntax? end = null;
            if (parser.CurrentToken is not (Token.Comma or Token.CloseBracket or Token.Colon))
            {
                end = parser.ParseNestedExpression(parser._position);
                if (end is null)
                {
                    parser.AddDiagnostic("LA1022", "Expected slice stop expression.", openBracketToken);
                    return null;
                }
                lastSpan = end.Span;
                parser.SkipGroupedExpressionTrivia();
            }

            ExpressionSyntax? step = null;
            if (parser.CurrentToken == Token.Colon)
            {
                lastSpan = parser.SpanOf(parser.ReadToken());
                parser.SkipGroupedExpressionTrivia();
                if (parser.CurrentToken is not (Token.Comma or Token.CloseBracket))
                {
                    step = parser.ParseNestedExpression(parser._position);
                    if (step is null)
                    {
                        parser.AddDiagnostic("LA1022", "Expected slice step expression.", openBracketToken);
                        return null;
                    }
                    lastSpan = step.Span;
                    parser.SkipGroupedExpressionTrivia();
                }
            }
            if (start is AssignmentExpressionSyntax || end is AssignmentExpressionSyntax || step is AssignmentExpressionSyntax)
            {
                parser.AddDiagnostic("LA1100", "Assignment expressions in slice bounds require parentheses.", firstToken);
                return null;
            }
            return new SliceValueExpressionSyntax(start, end, step, Merge(parser.SpanOf(firstToken), lastSpan));
        }
    }
}
