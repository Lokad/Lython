
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
            var token = CurrentToken;
            if (token is not (Token.Dot or Token.OpenParen or Token.OpenBracket)) return expression;
            if (++postfixCount > MaxNestingDepth)
            {
                AddDiagnostic(
                    "LA0003",
                    $"Expression chain exceeds the maximum of {MaxNestingDepth} operands.",
                    _position);
                return null;
            }

            expression = token switch
            {
                Token.Dot => ParseMemberPostfix(expression),
                Token.OpenParen => ParseCallPostfix(expression),
                Token.OpenBracket => ParseSubscriptPostfix(expression),
                _ => throw new InvalidOperationException("Unknown postfix form."),
            };
            if (expression is null) return null;
        }
    }

    private ExpressionSyntax? ParseMemberPostfix(ExpressionSyntax target)
    {
        var dotToken = ReadToken();
        if (!TryReadMemberName(out var memberToken))
        {
            AddDiagnostic("LA1005", "Expected attribute name after '.'.", dotToken);
            return null;
        }

        return new MemberExpressionSyntax(
            target,
            IdentifierText(memberToken),
            Merge(target.Span, SpanOf(memberToken)));
    }

    private CallExpressionSyntax? ParseCallPostfix(ExpressionSyntax target)
    {
        var arguments = ParseCallArguments(out var closeParenToken);
        return arguments is null
            ? null
            : new CallExpressionSyntax(target, arguments, Merge(target.Span, SpanOf(closeParenToken)));
    }

    private IReadOnlyList<CallArgumentSyntax>? ParseCallArguments(out int closeParenToken)
    {
        var openParenToken = ReadToken();
        closeParenToken = -1;
        List<CallArgumentSyntax>? parsedArguments = null;
        var positionalCount = 0;
        var needsReordering = false;
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.CloseParen)
        {
            parsedArguments = [];
            var sawKeywordArgument = false;
            var sawDictionaryUnpacking = false;
            while (true)
            {
                var form = CallArgumentForm.Positional;
                if (CurrentToken == Token.StarStar)
                {
                    ReadToken();
                    form = CallArgumentForm.StarredDictionary;
                    sawKeywordArgument = true;
                    sawDictionaryUnpacking = true;
                }
                else if (CurrentToken == Token.Star)
                {
                    if (sawDictionaryUnpacking)
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
                    form = CallArgumentForm.Keyword(RawIdentifierText(nameToken));
                    sawKeywordArgument = true;
                }
                else if (sawKeywordArgument)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", _position);
                    return null;
                }

                var argument = ParseNestedExpression(_position);
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

                if (form.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList)
                {
                    positionalCount++;
                    needsReordering |= sawKeywordArgument;
                }
                parsedArguments.Add(new CallArgumentSyntax(form, argument));

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
        if (!TryRead(Token.CloseParen, out closeParenToken))
        {
            AddDiagnostic("LA1006", "Expected ')' after call arguments.", openParenToken);
            return null;
        }

        if (parsedArguments is null) return [];
        // Python evaluates a star expression before preceding keywords.
        // Already ordered forms can transfer their completed argument list.
        return needsReordering ? OrderCallArguments(parsedArguments, positionalCount) : parsedArguments;
    }

    private CallArgumentSyntax[] OrderCallArguments(List<CallArgumentSyntax> arguments, int positionalCount)
    {
        CheckCancellation();
        var ordered = new CallArgumentSyntax[arguments.Count];
        var positional = 0;
        var keyword = positionalCount;
        for (var index = 0; index < arguments.Count; index++)
        {
            CheckCancellation(index);
            var argument = arguments[index];
            var destination = argument.Kind is CallArgumentKind.Positional or CallArgumentKind.StarredList
                ? positional++ : keyword++;
            ordered[destination] = argument;
        }
        return ordered;
    }

    private ExpressionSyntax? ParseSubscriptPostfix(ExpressionSyntax target)
    {
        var openBracketToken = ReadToken();
        SkipGroupedExpressionTrivia();
        var first = ParseSubscriptItem(openBracketToken, out var firstUnpacking);
        if (first is null) return null;
        SkipGroupedExpressionTrivia();

        List<CollectionDisplayItemSyntax>? items = null;
        if (firstUnpacking is not null || CurrentToken == Token.Comma)
        {
            items = [WrapItem(first, firstUnpacking)];
            while (CurrentToken == Token.Comma)
            {
                ReadToken();
                SkipGroupedExpressionTrivia();
                if (CurrentToken == Token.CloseBracket) break;
                var next = ParseSubscriptItem(openBracketToken, out var unpackingSpan);
                if (next is null) return null;
                items.Add(WrapItem(next, unpackingSpan));
                SkipGroupedExpressionTrivia();
            }
        }

        if (!TryRead(Token.CloseBracket, out var closeBracketToken))
        {
            AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
            return null;
        }

        var span = Merge(target.Span, SpanOf(closeBracketToken));
        if (items is null && first is SliceValueExpressionSyntax slice)
            return new SliceExpressionSyntax(target, slice.Start, slice.End, slice.Step, span);

        var key = items is null ? first : new TupleLiteralExpressionSyntax(items, Merge(items[0].Span, items[^1].Span));
        return new SubscriptExpressionSyntax(target, key, span);

        static CollectionDisplayItemSyntax WrapItem(ExpressionSyntax item, LythonSourceSpan? unpackingSpan)
            => unpackingSpan is null ? new CollectionValueItemSyntax(item)
                : new CollectionUnpackingItemSyntax(item, Merge(unpackingSpan, item.Span));
    }

    private ExpressionSyntax? ParseSubscriptItem(int openBracketToken, out LythonSourceSpan? unpackingSpan)
    {
        unpackingSpan = CurrentToken == Token.Star ? SpanOf(ReadToken()) : null;
        var allowSlice = unpackingSpan is null;
        var firstToken = _position;
        ExpressionSyntax? start = null;
        if (CurrentToken != Token.Colon || !allowSlice)
        {
            start = ParseNestedExpression(_position);
            if (start is null)
            {
                AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                return null;
            }
            SkipGroupedExpressionTrivia();
        }
        if (CurrentToken != Token.Colon || !allowSlice) return start;

        var lastSpan = SpanOf(ReadToken());
        SkipGroupedExpressionTrivia();
        ExpressionSyntax? end = null;
        if (CurrentToken is not (Token.Comma or Token.CloseBracket or Token.Colon))
        {
            end = ParseNestedExpression(_position);
            if (end is null)
            {
                AddDiagnostic("LA1022", "Expected slice stop expression.", openBracketToken);
                return null;
            }
            lastSpan = end.Span;
            SkipGroupedExpressionTrivia();
        }

        ExpressionSyntax? step = null;
        if (CurrentToken == Token.Colon)
        {
            lastSpan = SpanOf(ReadToken());
            SkipGroupedExpressionTrivia();
            if (CurrentToken is not (Token.Comma or Token.CloseBracket))
            {
                step = ParseNestedExpression(_position);
                if (step is null)
                {
                    AddDiagnostic("LA1022", "Expected slice step expression.", openBracketToken);
                    return null;
                }
                lastSpan = step.Span;
                SkipGroupedExpressionTrivia();
            }
        }
        if (start is AssignmentExpressionSyntax || end is AssignmentExpressionSyntax || step is AssignmentExpressionSyntax)
        {
            AddDiagnostic("LA1100", "Assignment expressions in slice bounds require parentheses.", firstToken);
            return null;
        }
        return new SliceValueExpressionSyntax(start, end, step, Merge(SpanOf(firstToken), lastSpan));
    }
}
