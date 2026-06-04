using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed class Parser
{
    private readonly LexerResult<Token> _tokens;
    private readonly bool[] _implicitLineJoinTrivia;
    private readonly Queue<StatementSyntax> _pendingStatements = new();
    private readonly List<LythonDiagnostic> _diagnostics = new();
    private int _position;
    private int _functionDepth;

    public Parser(LexerResult<Token> tokens)
    {
        _tokens = tokens;
        _implicitLineJoinTrivia = ComputeImplicitLineJoinTrivia(tokens);
    }

    public FrontendResult Parse()
    {
        var statements = new List<StatementSyntax>();

        SkipEndOfLines();

        while (CurrentToken != Token.End)
        {
            var statement = ParseStatement();
            if (statement is null)
            {
                Synchronize();
            }
            else
            {
                statements.Add(statement);
            }

            SkipEndOfLines();
        }

        if (_diagnostics.Count > 0)
        {
            return new FrontendResult(null, _diagnostics);
        }

        return new FrontendResult(new ScriptSyntax(statements), Array.Empty<LythonDiagnostic>());
    }

    private StatementSyntax? ParseStatement()
    {
        if (_pendingStatements.Count != 0)
        {
            return _pendingStatements.Dequeue();
        }

        if (CurrentToken == Token.With)
        {
            return ParseWithStatement();
        }

        if (CurrentToken == Token.Match && LooksLikeMatchStatement())
        {
            return ParseMatchStatement();
        }

        if (CurrentToken == Token.At)
        {
            return ParseDecoratedStatement();
        }

        if (TryParseUnsupportedStatement(out var unsupported))
        {
            return unsupported;
        }

        if (CurrentToken is Token.Import or Token.From)
        {
            return ParseImportStatement();
        }

        if (CurrentToken == Token.Def)
        {
            return ParseFunctionDefinition(Array.Empty<ExpressionSyntax>());
        }

        if (CurrentToken == Token.Class)
        {
            return ParseClassDefinition();
        }

        if (CurrentToken == Token.Try)
        {
            return ParseTryStatement();
        }

        if (CurrentToken == Token.If)
        {
            return ParseIfStatement();
        }

        if (CurrentToken == Token.For)
        {
            return ParseForStatement();
        }

        if (CurrentToken == Token.While)
        {
            return ParseWhileStatement();
        }

        return ParseSimpleStatement();
    }

    private StatementSyntax? ParseSimpleStatement()
    {
        if (_pendingStatements.Count != 0)
        {
            return _pendingStatements.Dequeue();
        }

        if (TryParseUnsupportedStatement(out var unsupported))
        {
            return unsupported;
        }

        if (CurrentToken is Token.Import or Token.From)
        {
            return ParseImportStatement();
        }

        if (CurrentToken == Token.Pass)
        {
            var passToken = ReadToken();
            return new PassStatementSyntax(SpanOf(passToken));
        }

        if (CurrentToken == Token.Break)
        {
            var breakToken = ReadToken();
            return new BreakStatementSyntax(SpanOf(breakToken));
        }

        if (CurrentToken == Token.Continue)
        {
            var continueToken = ReadToken();
            return new ContinueStatementSyntax(SpanOf(continueToken));
        }

        if (CurrentToken == Token.Assert)
        {
            return ParseAssertStatement();
        }

        if (CurrentToken == Token.Del)
        {
            return ParseDeleteStatement();
        }

        if (CurrentToken == Token.Return)
        {
            return ParseReturnStatement();
        }

        if (CurrentToken == Token.Raise)
        {
            return ParseRaiseStatement();
        }

        if (IsNameToken(CurrentToken) && IsAugmentedAssignmentToken(PeekToken(1)))
        {
            return ParseAugmentedAssignmentStatement();
        }

        if (CurrentToken == Token.Star || (IsNameToken(CurrentToken) && PeekToken(1) == Token.Comma))
        {
            return ParseUnpackingAssignmentStatement();
        }

        if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Colon)
        {
            return ParseAnnotatedAssignmentStatement();
        }

        if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Assign)
        {
            return ParseAssignmentStatement();
        }

        if (IsNameToken(CurrentToken) && PeekToken(1) is Token.OpenBracket or Token.Dot)
        {
            var startDiagnosticCount = _diagnostics.Count;
            var targetAssignment = TryParsePostfixAssignmentStatement();
            if (targetAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return targetAssignment;
            }
        }

        var expression = ParseExpression();
        if (expression is null)
        {
            return null;
        }

        return new ExpressionStatementSyntax(expression, expression.Span);
    }

    private StatementSyntax? ParseIfStatement()
    {
        var ifToken = ReadToken();
        var condition = ParseExpression();
        if (condition is null)
        {
            AddDiagnostic("LA1010", "Expected condition after 'if'.", ifToken);
            return null;
        }

        if (!TryRead(Token.Colon, out var colonToken))
        {
            AddDiagnostic("LA1011", "Expected ':' after if condition.", condition.Span);
            return null;
        }

        var thenStatements = ParseSuite("LA1012", "Expected indented block after 'if'.");
        if (thenStatements is null)
        {
            return null;
        }

        IReadOnlyList<StatementSyntax>? elseStatements = null;
        LythonSourceSpan span = Merge(SpanOf(ifToken), thenStatements[^1].Span);

        IfStatementSyntax? nestedElseIf = null;
        while (CurrentToken == Token.Elif)
        {
            var elifToken = ReadToken();
            var elifCondition = ParseExpression();
            if (elifCondition is null)
            {
                AddDiagnostic("LA1013", "Expected condition after 'elif'.", elifToken);
                return null;
            }

            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1014", "Expected ':' after elif condition.", elifCondition.Span);
                return null;
            }

            var elifBody = ParseSuite("LA1015", "Expected indented block after 'elif'.");
            if (elifBody is null)
            {
                return null;
            }

            var elifSyntax = new IfStatementSyntax(elifCondition, elifBody, null, Merge(SpanOf(elifToken), elifBody[^1].Span));
            if (nestedElseIf is null)
            {
                nestedElseIf = elifSyntax;
            }
            else
            {
                nestedElseIf = nestedElseIf with
                {
                    ElseStatements = [elifSyntax],
                    Span = Merge(nestedElseIf.Span, elifSyntax.Span)
                };
            }

            elseStatements = [nestedElseIf];
            span = Merge(span, nestedElseIf.Span);
            break;
        }

        if (CurrentToken == Token.Else)
        {
            var elseToken = ReadToken();
            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1016", "Expected ':' after 'else'.", elseToken);
                return null;
            }

            var parsedElse = ParseSuite("LA1017", "Expected indented block after 'else'.");
            if (parsedElse is null)
            {
                return null;
            }

            if (nestedElseIf is not null)
            {
                nestedElseIf = nestedElseIf with
                {
                    ElseStatements = parsedElse,
                    Span = Merge(nestedElseIf.Span, parsedElse[^1].Span)
                };
                elseStatements = [nestedElseIf];
            }
            else
            {
                elseStatements = parsedElse;
            }

            span = Merge(span, parsedElse[^1].Span);
        }

        _ = colonToken;
        return new IfStatementSyntax(condition, thenStatements, elseStatements, span);
    }

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

            var name = _tokens.GetString(nameToken);
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

            var name = _tokens.GetString(nameToken);
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

        var name = _tokens.GetString(targetToken);
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

                    restName = _tokens.GetString(restToken);
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

                    keywordPatterns.Add(new MatchClassKeywordPatternSyntax(_tokens.GetString(nameToken), keywordPattern));
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

        var expression = ParseNameOrAttributeExpressionFromName(nameToken, _tokens.GetString(nameToken));
        return expression is MemberExpressionSyntax ? expression : null;
    }

    private bool TryParseLiteralPatternExpression(out ExpressionSyntax expression)
    {
        expression = null!;

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
                _tokens.GetString(memberToken),
                Merge(expression.Span, SpanOf(memberToken)));
        }

        return expression;
    }

    private StatementSyntax? ParseForStatement()
    {
        var forToken = ReadToken();
        if (!TryParseLoopTarget(out var target, out var targetToken))
        {
            AddDiagnostic("LA1015", "Expected loop variable after 'for'.", forToken);
            return null;
        }

        if (!TryRead(Token.In, out _))
        {
            AddDiagnostic("LA1016", "Expected 'in' in for statement.", targetToken);
            return null;
        }

        var iterable = ParseExpression();
        if (iterable is null)
        {
            AddDiagnostic("LA1017", "Expected iterable expression in for statement.", targetToken);
            return null;
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1018", "Expected ':' after for statement.", iterable.Span);
            return null;
        }

        var body = ParseSuite("LA1019", "Expected indented block after 'for'.");
        if (body is null)
        {
            return null;
        }

        IReadOnlyList<StatementSyntax>? elseStatements = null;
        if (CurrentToken == Token.Else)
        {
            var elseToken = ReadToken();
            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1016", "Expected ':' after 'else'.", elseToken);
                return null;
            }

            elseStatements = ParseSuite("LA1019", "Expected indented block after 'else'.");
            if (elseStatements is null)
            {
                return null;
            }
        }

        return new ForStatementSyntax(
            target,
            iterable,
            elseStatements,
            body,
            Merge(SpanOf(forToken), (elseStatements ?? body)[^1].Span));
    }

    private StatementSyntax? ParseWhileStatement()
    {
        var whileToken = ReadToken();
        var condition = ParseExpression();
        if (condition is null)
        {
            AddDiagnostic("LA1027", "Expected condition after 'while'.", whileToken);
            return null;
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1028", "Expected ':' after while condition.", condition.Span);
            return null;
        }

        var body = ParseSuite("LA1029", "Expected indented block after 'while'.");
        if (body is null)
        {
            return null;
        }

        IReadOnlyList<StatementSyntax>? elseStatements = null;
        if (CurrentToken == Token.Else)
        {
            var elseToken = ReadToken();
            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1016", "Expected ':' after 'else'.", elseToken);
                return null;
            }

            elseStatements = ParseSuite("LA1029", "Expected indented block after 'else'.");
            if (elseStatements is null)
            {
                return null;
            }
        }

        return new WhileStatementSyntax(condition, elseStatements, body, Merge(SpanOf(whileToken), (elseStatements ?? body)[^1].Span));
    }

    private StatementSyntax? ParseFunctionDefinition(IReadOnlyList<ExpressionSyntax> decorators)
    {
        var defToken = ReadToken();
        if (!TryReadNameToken(out var nameToken))
        {
            AddDiagnostic("LA1030", "Expected function name after 'def'.", defToken);
            return null;
        }

        if (!TryRead(Token.OpenParen, out var openParen))
        {
            AddDiagnostic("LA1031", "Expected '(' after function name.", nameToken);
            return null;
        }

        if (!TryParseFunctionParameters(Token.CloseParen, "function definition", allowAnnotations: true, out var parameters, out var closeParen))
        {
            return null;
        }

        ExpressionSyntax? returnAnnotation = null;
        if (CurrentToken == Token.Arrow)
        {
            ReadToken();
            returnAnnotation = ParseExpression();
            if (returnAnnotation is null)
            {
                AddDiagnostic("LA1072", "Expected return annotation after '->'.", closeParen);
                return null;
            }
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1034", "Expected ':' after function signature.", closeParen);
            return null;
        }

        IReadOnlyList<StatementSyntax>? body;
        _functionDepth++;
        try
        {
            body = ParseSuite("LA1035", "Expected indented block after function definition.");
        }
        finally
        {
            _functionDepth--;
        }

        if (body is null)
        {
            return null;
        }

        return new FunctionDefinitionStatementSyntax(
            _tokens.GetString(nameToken),
            decorators,
            parameters,
            returnAnnotation,
            body,
            Merge(SpanOf(defToken), body[^1].Span));
    }

    private StatementSyntax? ParseClassDefinition()
        => ParseClassDefinition(null, Array.Empty<ExpressionSyntax>());

    private StatementSyntax? ParseDecoratedStatement()
    {
        var decorators = new List<ExpressionSyntax>();
        DataclassDecoratorSyntax? dataclassDecorator = null;

        while (CurrentToken == Token.At)
        {
            if (!TryParseDecorator(ref dataclassDecorator, decorators))
            {
                return null;
            }
        }

        if (CurrentToken == Token.Class)
        {
            return ParseClassDefinition(dataclassDecorator, decorators);
        }

        if (CurrentToken == Token.Def)
        {
            if (dataclassDecorator is not null)
            {
                AddDiagnostic("LA1109", "@dataclass can only be applied to class definitions.", _position);
                return null;
            }

            return ParseFunctionDefinition(decorators);
        }

        AddDiagnostic("LA1109", "Supported decorators in Lython must apply to a class or function definition.", _position);
        return null;
    }

    private bool TryParseDecorator(ref DataclassDecoratorSyntax? dataclassDecorator, List<ExpressionSyntax> decorators)
    {
        var atToken = ReadToken();
        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1105", "Expected decorator expression after '@'.", atToken);
            return false;
        }

        if (!TryRead(Token.Eol, out var endToken))
        {
            AddDiagnostic("LA1113", "Expected end of line after decorator.", atToken);
            return false;
        }

        if (TryConvertDataclassDecorator(expression, Merge(SpanOf(atToken), SpanOf(endToken)), out var parsedDataclass))
        {
            if (parsedDataclass is not null)
            {
                if (dataclassDecorator is not null)
                {
                    AddDiagnostic("LA1114", "Duplicate @dataclass decorator.", parsedDataclass.Span);
                    return false;
                }

                dataclassDecorator = parsedDataclass;
            }

            return true;
        }

        decorators.Add(expression);
        return true;
    }

    private bool TryConvertDataclassDecorator(ExpressionSyntax expression, LythonSourceSpan span, out DataclassDecoratorSyntax? decorator)
    {
        var init = true;
        var repr = true;
        var eq = true;
        var order = false;
        var unsafeHash = false;
        var frozen = false;
        var kwOnly = false;
        var matchArgs = true;

        switch (expression)
        {
            case IdentifierExpressionSyntax { Name: "dataclass" }:
                decorator = new DataclassDecoratorSyntax(init, repr, eq, order, unsafeHash, frozen, kwOnly, matchArgs, span);
                return true;
            case MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                    MemberName: "dataclass"
                }:
                decorator = new DataclassDecoratorSyntax(init, repr, eq, order, unsafeHash, frozen, kwOnly, matchArgs, span);
                return true;
            case CallExpressionSyntax { Target: var target, Arguments: var arguments } when IsDataclassDecoratorTarget(target):
                foreach (var argument in arguments)
                {
                    if (argument.Kind is not CallArgumentKind.Keyword || argument.Name is null)
                    {
                        AddDiagnostic("LA1107", "@dataclass currently expects keyword boolean options only.", argument.Expression.Span);
                        decorator = null;
                        return true;
                    }

                    if (argument.Expression is not BooleanLiteralExpressionSyntax boolean)
                    {
                        AddDiagnostic("LA1110", "@dataclass options currently expect True or False.", argument.Expression.Span);
                        decorator = null;
                        return true;
                    }

                    switch (argument.Name)
                    {
                        case "init":
                            init = boolean.Value;
                            break;
                        case "repr":
                            repr = boolean.Value;
                            break;
                        case "eq":
                            eq = boolean.Value;
                            break;
                        case "order":
                            order = boolean.Value;
                            break;
                        case "unsafe_hash":
                            unsafeHash = boolean.Value;
                            break;
                        case "frozen":
                            frozen = boolean.Value;
                            break;
                        case "kw_only":
                            kwOnly = boolean.Value;
                            break;
                        case "match_args":
                            matchArgs = boolean.Value;
                            break;
                        default:
                            AddDiagnostic("LA1111", $"Unsupported @dataclass option '{argument.Name}'.", argument.Expression.Span);
                            decorator = null;
                            return true;
                    }
                }

                decorator = new DataclassDecoratorSyntax(init, repr, eq, order, unsafeHash, frozen, kwOnly, matchArgs, span);
                return true;
            default:
                decorator = null;
                return false;
        }
    }

    private static bool IsDataclassDecoratorTarget(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax { Name: "dataclass" } => true,
            MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                MemberName: "dataclass"
            } => true,
            _ => false
        };

    private StatementSyntax? ParseClassDefinition(DataclassDecoratorSyntax? dataclassDecorator, IReadOnlyList<ExpressionSyntax> decorators)
    {
        var classToken = ReadToken();
        if (!TryReadNameToken(out var nameToken))
        {
            AddDiagnostic("LA1100", "Expected class name after 'class'.", classToken);
            return null;
        }

        var bases = new List<ExpressionSyntax>();
        var keywordArguments = new List<ClassKeywordArgumentSyntax>();
        if (TryRead(Token.OpenParen, out var openParen))
        {
            if (CurrentToken != Token.CloseParen)
            {
                while (true)
                {
                    if (IsNameToken(CurrentToken) &&
                        PeekToken(1) == Token.Assign &&
                        TryReadNameToken(out var keywordNameToken))
                    {
                        _ = ReadToken(); // '='
                        var keywordValue = ParseExpression();
                        if (keywordValue is null)
                        {
                            AddDiagnostic("LA1105", "Expected class keyword value in class definition.", keywordNameToken);
                            return null;
                        }

                        keywordArguments.Add(new ClassKeywordArgumentSyntax(
                            _tokens.GetString(keywordNameToken),
                            keywordValue,
                            Merge(SpanOf(keywordNameToken), keywordValue.Span)));
                    }
                    else
                    {
                    var baseExpression = ParseExpression();
                    if (baseExpression is null)
                    {
                        AddDiagnostic("LA1101", "Expected base class expression in class definition.", openParen);
                        return null;
                    }

                    bases.Add(baseExpression);
                    }
                    if (!TryRead(Token.Comma, out _))
                    {
                        break;
                    }

                    if (CurrentToken == Token.CloseParen)
                    {
                        break;
                    }
                }
            }

            if (!TryRead(Token.CloseParen, out var closeParen))
            {
                AddDiagnostic("LA1102", "Expected ')' after base class list.", nameToken);
                return null;
            }
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1103", "Expected ':' after class definition header.", nameToken);
            return null;
        }

        var body = ParseSuite("LA1104", "Expected indented block after class definition.");
        if (body is null)
        {
            return null;
        }

        return new ClassDefinitionStatementSyntax(
            _tokens.GetString(nameToken),
            dataclassDecorator,
            decorators,
            bases,
            keywordArguments,
            body,
            Merge(SpanOf(classToken), body[^1].Span));
    }

    private bool TryParseFunctionParameters(Token terminator, string owner, bool allowAnnotations, out IReadOnlyList<FunctionParameterSyntax> parameters, out int terminatorToken)
    {
        parameters = Array.Empty<FunctionParameterSyntax>();
        terminatorToken = _position;

        var parsed = new List<FunctionParameterSyntax>();
        var seenDefault = false;
        var seenVariadicList = false;
        var seenVariadicDictionary = false;
        var keywordOnly = false;

        if (CurrentToken == terminator)
        {
            terminatorToken = ReadToken();
            parameters = parsed;
            return true;
        }

        while (true)
        {
            var kind = keywordOnly ? FunctionParameterKind.KeywordOnly : FunctionParameterKind.Positional;
            if (CurrentToken == Token.StarStar)
            {
                if (seenVariadicDictionary)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'variadic parameter'.", _position);
                    return false;
                }

                ReadToken();
                kind = FunctionParameterKind.VariadicDictionary;
                seenVariadicDictionary = true;
                keywordOnly = true;
            }
            else if (CurrentToken == Token.Star)
            {
                if (seenVariadicList)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'variadic parameter'.", _position);
                    return false;
                }

                ReadToken();
                seenVariadicList = true;
                keywordOnly = true;

                if (CurrentToken == Token.Comma || CurrentToken == terminator)
                {
                    if (CurrentToken == Token.Comma)
                    {
                        ReadToken();
                    }

                    continue;
                }

                kind = FunctionParameterKind.VariadicList;
            }

            if (!TryReadNameToken(out var parameterToken))
            {
                AddDiagnostic(owner == "lambda" ? "LA1070" : "LA1032", $"Expected parameter name in {owner}.", _position);
                return false;
            }

            ExpressionSyntax? annotation = null;
            if (allowAnnotations && CurrentToken == Token.Colon)
            {
                ReadToken();
                annotation = ParseExpression();
                if (annotation is null)
                {
                    AddDiagnostic(owner == "lambda" ? "LA1073" : "LA1074", $"Expected annotation expression in {owner}.", parameterToken);
                    return false;
                }
            }

            ExpressionSyntax? defaultValue = null;
            if (CurrentToken == Token.Assign)
            {
                if (kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'variadic parameter default'.", parameterToken);
                    return false;
                }

                ReadToken();
                defaultValue = ParseExpression();
                if (defaultValue is null)
                {
                    AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", parameterToken);
                    return false;
                }

                if (kind == FunctionParameterKind.Positional)
                {
                    seenDefault = true;
                }
            }
            else if (seenDefault && kind == FunctionParameterKind.Positional && !seenVariadicList && !seenVariadicDictionary)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'non-default parameter after default parameter'.", parameterToken);
                return false;
            }

            parsed.Add(new FunctionParameterSyntax(_tokens.GetString(parameterToken), annotation, defaultValue, kind));

            if (CurrentToken == terminator)
            {
                terminatorToken = ReadToken();
                parameters = parsed;
                return true;
            }

            if (CurrentToken != Token.Comma)
            {
                AddDiagnostic(owner == "lambda" ? "LA1071" : "LA1033", $"Expected {TokenNamer.Instance.TokenName(terminator, Array.Empty<Token>())} after parameter list.", _position);
                return false;
            }

            ReadToken();
            if (CurrentToken == terminator)
            {
                terminatorToken = ReadToken();
                parameters = parsed;
                return true;
            }
        }
    }

    private StatementSyntax? ParseReturnStatement()
    {
        var returnToken = ReadToken();
        if (CurrentToken is Token.Eol or Token.Semicolon or Token.Dedent or Token.End)
        {
            return new ReturnStatementSyntax(null, SpanOf(returnToken));
        }

        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1036", "Expected expression after 'return'.", returnToken);
            return null;
        }

        return new ReturnStatementSyntax(expression, Merge(SpanOf(returnToken), expression.Span));
    }

    private StatementSyntax? ParseRaiseStatement()
    {
        var raiseToken = ReadToken();
        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1042", "Expected expression after 'raise'.", raiseToken);
            return null;
        }

        return new RaiseStatementSyntax(expression, Merge(SpanOf(raiseToken), expression.Span));
    }

    private StatementSyntax? ParseAssertStatement()
    {
        var assertToken = ReadToken();
        var condition = ParseExpression();
        if (condition is null)
        {
            AddDiagnostic("LA1056", "Expected expression after 'assert'.", assertToken);
            return null;
        }

        ExpressionSyntax? message = null;
        if (CurrentToken == Token.Comma)
        {
            ReadToken();
            message = ParseExpression();
            if (message is null)
            {
                AddDiagnostic("LA1057", "Expected expression after ',' in assert statement.", _position);
                return null;
            }
        }

        return new AssertStatementSyntax(
            condition,
            message,
            Merge(SpanOf(assertToken), (message ?? condition).Span));
    }

    private StatementSyntax? ParseDeleteStatement()
    {
        var delToken = ReadToken();
        var target = ParsePostfixExpression();
        if (target is null)
        {
            AddDiagnostic("LA1069", "Expected target after 'del'.", delToken);
            return null;
        }

        return target switch
        {
            IdentifierExpressionSyntax or SubscriptExpressionSyntax or MemberExpressionSyntax => new DeleteStatementSyntax(target, Merge(SpanOf(delToken), target.Span)),
            _ => AddUnsupportedDeleteTarget(target, "delete target")
        };
    }

    private StatementSyntax? AddUnsupportedDeleteTarget(ExpressionSyntax target, string construct)
    {
        AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", target.Span);
        return null;
    }

    private StatementSyntax? ParseTryStatement()
    {
        var tryToken = ReadToken();
        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1043", "Expected ':' after 'try'.", tryToken);
            return null;
        }

        var tryBody = ParseSuite("LA1044", "Expected indented block after 'try'.");
        if (tryBody is null)
        {
            return null;
        }

        IReadOnlyList<string>? exceptionTypes = null;
        string? exceptionVariable = null;
        IReadOnlyList<StatementSyntax>? exceptBody = null;
        IReadOnlyList<StatementSyntax>? elseBody = null;
        IReadOnlyList<StatementSyntax>? finallyBody = null;
        var span = Merge(SpanOf(tryToken), tryBody[^1].Span);

        if (CurrentToken == Token.Except)
        {
            ReadToken();
            if (CurrentToken != Token.Colon)
            {
                var parsedTypes = new List<string>();
                if (CurrentToken == Token.OpenParen)
                {
                    ReadToken();
                    while (true)
                    {
                        if (!TryRead(Token.Identifier, out var typeToken))
                        {
                            AddDiagnostic("LA1045", "Expected exception type in except tuple.", _position);
                            return null;
                        }

                        parsedTypes.Add(_tokens.GetString(typeToken));
                        if (CurrentToken != Token.Comma)
                        {
                            break;
                        }

                        ReadToken();
                    }

                    if (!TryRead(Token.CloseParen, out _))
                    {
                        AddDiagnostic("LA1045", "Expected ')' after except tuple.", _position);
                        return null;
                    }
                }
                else if (IsNameToken(CurrentToken))
                {
                    var typeToken = ReadToken();
                    parsedTypes.Add(_tokens.GetString(typeToken));
                }
                else
                {
                    AddDiagnostic("LA1045", "Expected exception type after 'except'.", _position);
                    return null;
                }

                exceptionTypes = parsedTypes;

                if (CurrentToken == Token.As)
                {
                    ReadToken();
                    if (!TryReadNameToken(out var variableToken))
                    {
                        AddDiagnostic("LA1045", "Expected identifier after 'as' in except clause.", _position);
                        return null;
                    }

                    exceptionVariable = _tokens.GetString(variableToken);
                }
            }

            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1046", "Expected ':' after except clause.", _position);
                return null;
            }

            exceptBody = ParseSuite("LA1047", "Expected indented block after 'except'.");
            if (exceptBody is null)
            {
                return null;
            }

            span = Merge(span, exceptBody[^1].Span);
        }

        if (CurrentToken == Token.Else)
        {
            ReadToken();
            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1048", "Expected ':' after 'else'.", _position);
                return null;
            }

            elseBody = ParseSuite("LA1048", "Expected indented block after 'else'.");
            if (elseBody is null)
            {
                return null;
            }

            span = Merge(span, elseBody[^1].Span);
        }

        if (CurrentToken == Token.Finally)
        {
            ReadToken();
            if (!TryRead(Token.Colon, out _))
            {
                AddDiagnostic("LA1048", "Expected ':' after 'finally'.", _position);
                return null;
            }

            finallyBody = ParseSuite("LA1049", "Expected indented block after 'finally'.");
            if (finallyBody is null)
            {
                return null;
            }

            span = Merge(span, finallyBody[^1].Span);
        }

        if (exceptBody is null && finallyBody is null)
        {
            AddDiagnostic("LA1050", "Expected 'except' or 'finally' after 'try'.", tryToken);
            return null;
        }

        return new TryStatementSyntax(tryBody, exceptionTypes, exceptionVariable, exceptBody, elseBody, finallyBody, span);
    }

    private StatementSyntax? ParseImportStatement()
    {
        if (CurrentToken == Token.From)
        {
            return ParseFromImportStatement();
        }

        var importToken = ReadToken();
        if (!TryRead(Token.Identifier, out var moduleToken))
        {
            AddDiagnostic("LA1001", "Expected module name after 'import'.", importToken);
            return null;
        }

        var statements = new List<StatementSyntax>();
        while (true)
        {
            var moduleName = _tokens.GetString(moduleToken).Trim();
            var bindingName = moduleName;
            var endToken = moduleToken;

            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1051", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = _tokens.GetString(aliasToken);
                endToken = aliasToken;
            }

            if (!IsSupportedImport(moduleName))
            {
                AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleToken);
                return null;
            }

            statements.Add(new ImportStatementSyntax(
                moduleName,
                bindingName,
                null,
                Merge(importToken, endToken)));

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
            if (!TryRead(Token.Identifier, out moduleToken))
            {
                AddDiagnostic("LA1001", "Expected module name after ','.", _position);
                return null;
            }
        }

        for (var i = 1; i < statements.Count; i++)
        {
            _pendingStatements.Enqueue(statements[i]);
        }

        return statements[0];
    }

    private StatementSyntax? ParseFromImportStatement()
    {
        var fromToken = ReadToken();
        if (!TryRead(Token.Identifier, out var moduleToken))
        {
            AddDiagnostic("LA1052", "Expected module name after 'from'.", fromToken);
            return null;
        }

        var moduleName = _tokens.GetString(moduleToken).Trim();
        if (!IsSupportedImport(moduleName))
        {
            AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleToken);
            return null;
        }

        if (!TryRead(Token.Import, out _))
        {
            AddDiagnostic("LA1053", "Expected 'import' after module name.", moduleToken);
            return null;
        }

        var importedMembers = new List<ImportedMemberSyntax>();
        var grouped = false;
        if (CurrentToken == Token.OpenParen)
        {
            ReadToken();
            grouped = true;
            SkipGroupedImportTrivia();
        }

        while (true)
        {
            if (!TryRead(Token.Identifier, out var memberToken))
            {
                AddDiagnostic("LA1054", "Expected imported member name.", _position);
                return null;
            }

            var memberName = _tokens.GetString(memberToken);
            var bindingName = memberName;
            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1055", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = _tokens.GetString(aliasToken);
            }

            importedMembers.Add(new ImportedMemberSyntax(memberName, bindingName));

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
            if (grouped)
            {
                SkipGroupedImportTrivia();
                if (CurrentToken == Token.CloseParen)
                {
                    break;
                }
            }
        }

        if (grouped)
        {
            SkipGroupedImportTrivia();
        }

        if (grouped && !TryRead(Token.CloseParen, out _))
        {
            AddDiagnostic("LA1067", "Expected ')' after grouped import list.", _position);
            return null;
        }

        return new ImportStatementSyntax(
            moduleName,
            moduleName,
            importedMembers,
            Merge(fromToken, moduleToken));
    }

    private StatementSyntax? ParseAssignmentStatement()
    {
        var nameToken = ReadToken();
        ReadExpected(Token.Assign, "LA1003", "Expected '=' in assignment.");

        return ParseAssignmentAfterFirstTarget(
            new NameAssignmentTargetSyntax(_tokens.GetString(nameToken), SpanOf(nameToken)),
            nameToken);
    }

    private StatementSyntax? ParseAnnotatedAssignmentStatement()
    {
        var nameToken = ReadToken();
        ReadExpected(Token.Colon, "LA1058", "Expected ':' in annotated assignment.");

        var annotation = ParseExpression();
        if (annotation is null)
        {
            AddDiagnostic("LA1059", "Expected annotation expression after ':'.", nameToken);
            return null;
        }

        ExpressionSyntax? expression = null;
        if (CurrentToken == Token.Assign)
        {
            ReadToken();
            expression = ParseExpression();
            if (expression is null)
            {
                AddDiagnostic("LA1060", "Expected expression on the right side of annotated assignment.", nameToken);
                return null;
            }
        }

        return new AnnotatedAssignmentStatementSyntax(
            _tokens.GetString(nameToken),
            annotation,
            expression,
            Merge(nameToken, (expression ?? annotation).Span));
    }

    private StatementSyntax? TryParsePostfixAssignmentStatement()
    {
        var startPosition = _position;
        var startDiagnosticCount = _diagnostics.Count;

        var target = ParsePostfixExpression();
        if (target is null || CurrentToken != Token.Assign)
        {
            _position = startPosition;
            if (_diagnostics.Count > startDiagnosticCount)
            {
                _diagnostics.RemoveRange(startDiagnosticCount, _diagnostics.Count - startDiagnosticCount);
            }

            return null;
        }

        ReadToken();
        return target switch
        {
            SubscriptExpressionSyntax subscript => ParseAssignmentAfterFirstTarget(
                new SubscriptAssignmentTargetSyntax(subscript.Target, subscript.Index, subscript.Span),
                startPosition),
            MemberExpressionSyntax member => ParseAssignmentAfterFirstTarget(
                new MemberAssignmentTargetSyntax(member.Target, member.MemberName, member.Span),
                startPosition),
            _ => AddUnsupportedAssignmentTarget(target)
        };
    }

    private StatementSyntax? AddUnsupportedAssignmentTarget(ExpressionSyntax target)
    {
        AddDiagnostic("LA1068", "Unsupported assignment target.", target.Span);
        return null;
    }

    private StatementSyntax? ParseWithStatement()
    {
        var withToken = ReadToken();
        var managers = new List<(ExpressionSyntax ContextExpression, string? VariableName)>();
        while (true)
        {
            var contextExpression = ParseExpression();
            if (contextExpression is null)
            {
                AddDiagnostic("LA1010", "Expected expression after 'with'.", withToken);
                return null;
            }

            string? variableName = null;
            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryReadNameToken(out var variableToken))
                {
                    AddDiagnostic("LA1045", "Expected identifier after 'as' in with statement.", _position);
                    return null;
                }

                variableName = _tokens.GetString(variableToken);
            }

            managers.Add((contextExpression, variableName));
            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
        }

        if (!TryRead(Token.Colon, out _))
        {
            AddDiagnostic("LA1011", "Expected ':' after with expression.", managers[^1].ContextExpression.Span);
            return null;
        }

        var body = ParseSuite("LA1012", "Expected indented block after 'with'.");
        if (body is null)
        {
            return null;
        }

        IReadOnlyList<StatementSyntax> nestedBody = body;
        for (var i = managers.Count - 1; i >= 0; i--)
        {
            var manager = managers[i];
            nestedBody =
            [
                new WithStatementSyntax(
                    manager.ContextExpression,
                    manager.VariableName,
                    nestedBody,
                    Merge(manager.ContextExpression.Span, nestedBody[^1].Span))
            ];
        }

        return ((WithStatementSyntax)nestedBody[0]) with { Span = Merge(SpanOf(withToken), body[^1].Span) };
    }

    private StatementSyntax? ParseUnpackingAssignmentStatement()
    {
        var targets = new List<UnpackingTargetSyntax>();
        var starredCount = 0;
        var firstToken = _position;

        while (true)
        {
            var isStarred = false;
            if (CurrentToken == Token.Star)
            {
                ReadToken();
                isStarred = true;
                starredCount++;
                if (starredCount > 1)
                {
                    AddDiagnostic("LA2000", "Unsupported Python construct 'multiple starred assignment targets'.", _position);
                    return null;
                }
            }

            if (!TryReadNameToken(out var nameToken))
            {
                AddDiagnostic("LA1032", "Expected assignment target.", firstToken);
                return null;
            }

            targets.Add(new UnpackingTargetSyntax(_tokens.GetString(nameToken), isStarred));
            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
        }

        if (!TryRead(Token.Assign, out _))
        {
            AddDiagnostic("LA1003", "Expected '=' in assignment.", _position);
            return null;
        }

        return ParseAssignmentAfterFirstTarget(
            new UnpackingAssignmentTargetGroupSyntax(targets, SpanOf(firstToken)),
            firstToken);
    }

    private StatementSyntax? ParseAssignmentAfterFirstTarget(AssignmentTargetSyntax firstTarget, int startToken)
    {
        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", startToken);
            return null;
        }

        if (CurrentToken != Token.Assign)
        {
            return CreateAssignmentStatement(firstTarget, expression);
        }

        var targets = new List<AssignmentTargetSyntax> { firstTarget };
        var currentExpression = expression;
        while (CurrentToken == Token.Assign)
        {
            if (!TryConvertExpressionToAssignmentTarget(currentExpression, out var nextTarget))
            {
                AddUnsupportedAssignmentTarget(currentExpression);
                return null;
            }

            targets.Add(nextTarget!);
            ReadToken();

            currentExpression = ParseExpression();
            if (currentExpression is null)
            {
                AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", startToken);
                return null;
            }
        }

        return new ChainedAssignmentStatementSyntax(
            targets,
            currentExpression,
            Merge(SpanOf(startToken), currentExpression.Span));
    }

    private StatementSyntax CreateAssignmentStatement(AssignmentTargetSyntax target, ExpressionSyntax expression)
    {
        return target switch
        {
            NameAssignmentTargetSyntax name => new AssignmentStatementSyntax(
                name.Name,
                expression,
                Merge(name.Span, expression.Span)),
            UnpackingAssignmentTargetGroupSyntax unpacking => new UnpackingAssignmentStatementSyntax(
                unpacking.Targets,
                expression,
                Merge(unpacking.Span, expression.Span)),
            SubscriptAssignmentTargetSyntax subscript => new SubscriptAssignmentStatementSyntax(
                subscript.Target,
                subscript.Index,
                expression,
                Merge(subscript.Span, expression.Span)),
            MemberAssignmentTargetSyntax member => new MemberAssignmentStatementSyntax(
                member.Target,
                member.MemberName,
                expression,
                Merge(member.Span, expression.Span)),
            _ => throw new InvalidOperationException($"Unsupported assignment target syntax: {target.GetType().Name}")
        };
    }

    private bool TryConvertExpressionToAssignmentTarget(ExpressionSyntax expression, out AssignmentTargetSyntax? target)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                target = new NameAssignmentTargetSyntax(identifier.Name, identifier.Span);
                return true;
            case SubscriptExpressionSyntax subscript:
                target = new SubscriptAssignmentTargetSyntax(subscript.Target, subscript.Index, subscript.Span);
                return true;
            case MemberExpressionSyntax member:
                target = new MemberAssignmentTargetSyntax(member.Target, member.MemberName, member.Span);
                return true;
            case TupleLiteralExpressionSyntax tuple when TryConvertSequenceExpressionToTargets(tuple.Items, out var tupleTargets):
                target = new UnpackingAssignmentTargetGroupSyntax(tupleTargets, expression.Span);
                return true;
            case ListLiteralExpressionSyntax list when TryConvertSequenceExpressionToTargets(list.Items, out var listTargets):
                target = new UnpackingAssignmentTargetGroupSyntax(listTargets, expression.Span);
                return true;
            default:
                target = null;
                return false;
        }
    }

    private static bool TryConvertSequenceExpressionToTargets(
        IReadOnlyList<ExpressionSyntax> items,
        out IReadOnlyList<UnpackingTargetSyntax> targets)
    {
        if (items.Count == 0)
        {
            targets = Array.Empty<UnpackingTargetSyntax>();
            return false;
        }

        var converted = new UnpackingTargetSyntax[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not IdentifierExpressionSyntax identifier)
            {
                targets = Array.Empty<UnpackingTargetSyntax>();
                return false;
            }

            converted[i] = new UnpackingTargetSyntax(identifier.Name, false);
        }

        targets = converted;
        return true;
    }

    private StatementSyntax? ParseAugmentedAssignmentStatement()
    {
        var nameToken = ReadToken();
        var operatorToken = ReadToken();
        if (!TryMapAugmentedAssignmentOperator(_tokens.Tokens[operatorToken].Token, out var op))
        {
            AddDiagnostic("LA2000", "Unsupported Python construct 'augmented assignment'.", operatorToken);
            return null;
        }

        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", nameToken);
            return null;
        }

        return new AugmentedAssignmentStatementSyntax(
            _tokens.GetString(nameToken),
            op,
            expression,
            Merge(nameToken, expression.Span));
    }

    private ExpressionSyntax? ParseExpression()
    {
        var expression = ParseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (TryGetUnsupportedTrailingExpressionConstruct(CurrentToken, out var construct))
        {
            AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", _position);
            return null;
        }

        if (CurrentToken == Token.ColonEqual)
        {
            var operatorToken = ReadToken();
            if (expression is not IdentifierExpressionSyntax identifier)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'assignment expression target'.", operatorToken);
                return null;
            }

            var assignedExpression = ParseExpression();
            if (assignedExpression is null)
            {
                AddDiagnostic("LA1046", "Expected expression after ':='.", operatorToken);
                return null;
            }

            expression = new AssignmentExpressionSyntax(
                identifier.Name,
                assignedExpression,
                Merge(expression.Span, assignedExpression.Span));
        }

        if (CurrentToken == Token.If)
        {
            var ifToken = ReadToken();
            var condition = ParseOrExpression();
            if (condition is null)
            {
                AddDiagnostic("LA1010", "Expected condition after 'if'.", ifToken);
                return null;
            }

            if (!TryRead(Token.Else, out var elseToken))
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'conditional expression'.", ifToken);
                return null;
            }

            var alternative = ParseExpression();
            if (alternative is null)
            {
                AddDiagnostic("LA1016", "Expected expression after 'else'.", elseToken);
                return null;
            }

            expression = new ConditionalExpressionSyntax(expression, condition, alternative, Merge(expression.Span, alternative.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseOrExpression()
    {
        var expression = ParseAndExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Or)
        {
            ReadToken();
            var right = ParseAndExpression();
            if (right is null)
            {
                AddDiagnostic("LA1037", "Expected expression after 'or'.", _position);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.Or, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseAndExpression()
    {
        var expression = ParseComparisonExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.And)
        {
            ReadToken();
            var right = ParseComparisonExpression();
            if (right is null)
            {
                AddDiagnostic("LA1038", "Expected expression after 'and'.", _position);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.And, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseComparisonExpression()
    {
        var expression = ParseBitwiseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (CurrentToken != Token.Less &&
            CurrentToken != Token.LessEqual &&
            CurrentToken != Token.Greater &&
            CurrentToken != Token.GreaterEqual &&
            CurrentToken != Token.EqualEqual &&
            CurrentToken != Token.BangEqual &&
            CurrentToken != Token.In &&
            CurrentToken != Token.Is &&
            CurrentToken != Token.Not)
        {
            return expression;
        }

        var operands = new List<ExpressionSyntax> { expression };
        var operators = new List<BinaryOperatorSyntax>();

        while (CurrentToken is Token.Less or Token.LessEqual or Token.Greater or Token.GreaterEqual or Token.EqualEqual or Token.BangEqual or Token.In or Token.Is or Token.Not)
        {
            var operatorToken = ReadToken();
            BinaryOperatorSyntax op;
            if (_tokens.Tokens[operatorToken].Token == Token.Not)
            {
                if (!TryRead(Token.In, out var inToken))
                {
                    AddDiagnostic("LA1039", "Expected 'in' after 'not'.", operatorToken);
                    return null;
                }

                op = BinaryOperatorSyntax.NotIn;
                operatorToken = inToken;
            }
            else if (_tokens.Tokens[operatorToken].Token == Token.Is)
            {
                if (CurrentToken == Token.Not)
                {
                    ReadToken();
                    op = BinaryOperatorSyntax.IsNot;
                }
                else
                {
                    op = BinaryOperatorSyntax.Is;
                }
            }
            else
            {
                op = _tokens.Tokens[operatorToken].Token switch
                {
                    Token.EqualEqual => BinaryOperatorSyntax.Equal,
                    Token.BangEqual => BinaryOperatorSyntax.NotEqual,
                    Token.Less => BinaryOperatorSyntax.Less,
                    Token.LessEqual => BinaryOperatorSyntax.LessEqual,
                    Token.Greater => BinaryOperatorSyntax.Greater,
                    Token.GreaterEqual => BinaryOperatorSyntax.GreaterEqual,
                    Token.In => BinaryOperatorSyntax.In,
                    _ => throw new InvalidOperationException()
                };
            }

            var right = ParseBitwiseOrExpression();
            if (right is null)
            {
                AddDiagnostic("LA1040", "Expected expression after comparison operator.", operatorToken);
                return null;
            }

            operators.Add(op);
            operands.Add(right);
            expression = right;
        }

        return operators.Count == 1
            ? new BinaryExpressionSyntax(operands[0], operators[0], operands[1], Merge(operands[0].Span, operands[1].Span))
            : new ChainedComparisonExpressionSyntax(operands, operators, Merge(operands[0].Span, operands[^1].Span));
    }

    private ExpressionSyntax? ParseBitwiseOrExpression()
    {
        var expression = ParseBitwiseXorExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Pipe)
        {
            var operatorKind = ReadToken();
            var right = ParseBitwiseXorExpression();
            if (right is null)
            {
                AddDiagnostic("LA1061", "Expected expression after bitwise '|'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseOr, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseBitwiseXorExpression()
    {
        var expression = ParseBitwiseAndExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Caret)
        {
            var operatorKind = ReadToken();
            var right = ParseBitwiseAndExpression();
            if (right is null)
            {
                AddDiagnostic("LA1062", "Expected expression after bitwise '^'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseXor, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseBitwiseAndExpression()
    {
        var expression = ParseShiftExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Ampersand)
        {
            var operatorKind = ReadToken();
            var right = ParseShiftExpression();
            if (right is null)
            {
                AddDiagnostic("LA1063", "Expected expression after bitwise '&'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseAnd, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseShiftExpression()
    {
        var expression = ParseAdditiveExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.LessLess or Token.GreaterGreater)
        {
            var operatorKind = ReadToken();
            var right = ParseAdditiveExpression();
            if (right is null)
            {
                AddDiagnostic("LA1064", "Expected expression after shift operator.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(
                expression,
                _tokens.Tokens[operatorKind].Token == Token.LessLess ? BinaryOperatorSyntax.LeftShift : BinaryOperatorSyntax.RightShift,
                right,
                Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.Plus or Token.Minus)
        {
            var operatorKind = ReadToken();
            var right = ParseMultiplicativeExpression();
            if (right is null)
            {
                AddDiagnostic("LA1041", "Expected expression after additive operator.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(
                expression,
                _tokens.Tokens[operatorKind].Token == Token.Plus ? BinaryOperatorSyntax.Add : BinaryOperatorSyntax.Subtract,
                right,
                Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.Star or Token.Slash or Token.SlashSlash or Token.Percent)
        {
            var operatorKind = ReadToken();
            var right = ParseUnaryExpression();
            if (right is null)
            {
                AddDiagnostic("LA1042", "Expected expression after multiplicative operator.", operatorKind);
                return null;
            }

            var op = _tokens.Tokens[operatorKind].Token switch
            {
                Token.Star => BinaryOperatorSyntax.Multiply,
                Token.Slash => BinaryOperatorSyntax.Divide,
                Token.SlashSlash => BinaryOperatorSyntax.FloorDivide,
                Token.Percent => BinaryOperatorSyntax.Modulo,
                _ => throw new InvalidOperationException()
            };

            expression = new BinaryExpressionSyntax(expression, op, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseUnaryExpression()
    {
        if (CurrentToken == Token.Not)
        {
            var notToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1043", "Expected expression after 'not'.", notToken);
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.Not, operand, Merge(SpanOf(notToken), operand.Span));
        }

        if (CurrentToken is Token.Plus or Token.Minus)
        {
            var operatorToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1044", "Expected expression after unary operator.", operatorToken);
                return null;
            }

            return new UnaryExpressionSyntax(
                _tokens.Tokens[operatorToken].Token == Token.Plus ? UnaryOperatorSyntax.Plus : UnaryOperatorSyntax.Minus,
                operand,
                Merge(SpanOf(operatorToken), operand.Span));
        }

        if (CurrentToken == Token.Tilde)
        {
            var operatorToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1065", "Expected expression after '~'.", operatorToken);
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.BitwiseNot, operand, Merge(SpanOf(operatorToken), operand.Span));
        }

        return ParsePowerExpression();
    }

    private ExpressionSyntax? ParsePowerExpression()
    {
        var expression = ParsePostfixExpression();
        if (expression is null)
        {
            return null;
        }

        if (CurrentToken != Token.StarStar)
        {
            return expression;
        }

        var operatorToken = ReadToken();
        var right = ParseUnaryExpression();
        if (right is null)
        {
            AddDiagnostic("LA1066", "Expected expression after '**'.", operatorToken);
            return null;
        }

        return new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.Power, right, Merge(expression.Span, right.Span));
    }

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
                    _tokens.GetString(memberToken),
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
                        string? argumentName = null;
                        var kind = CallArgumentKind.Positional;
                        if (CurrentToken == Token.StarStar)
                        {
                            ReadToken();
                            kind = CallArgumentKind.StarredDictionary;
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
                            kind = CallArgumentKind.StarredList;
                        }
                        else if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Assign)
                        {
                            var nameToken = ReadToken();
                            ReadToken();
                            argumentName = _tokens.GetString(nameToken);
                            kind = CallArgumentKind.Keyword;
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
                        if (CurrentToken == Token.For && kind == CallArgumentKind.Positional && argumentName is null)
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

                        arguments.Add(new CallArgumentSyntax(argumentName, argument, kind));

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

    private ExpressionSyntax? ParsePrimaryExpression()
    {
        if (TryParseUnsupportedExpression(out var unsupported))
        {
            return unsupported;
        }

        if (CurrentToken == Token.Lambda)
        {
            return ParseLambdaExpression();
        }

        if (CurrentToken == Token.String)
        {
            var tokenIndex = ReadToken();
            var literal = _tokens.GetString(tokenIndex);
            if (!TryDecodeStringLiteral(literal, out var value))
            {
                AddDiagnostic("LA1007", "Invalid string literal.", tokenIndex);
                return null;
            }

            return new StringLiteralExpressionSyntax(value, SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.Integer)
        {
            var tokenIndex = ReadToken();
            return new IntegerLiteralExpressionSyntax(
                _tokens.GetString(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.Float)
        {
            var tokenIndex = ReadToken();
            return new FloatLiteralExpressionSyntax(
                _tokens.GetString(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.True)
        {
            var tokenIndex = ReadToken();
            return new BooleanLiteralExpressionSyntax(true, SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.False)
        {
            var tokenIndex = ReadToken();
            return new BooleanLiteralExpressionSyntax(false, SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.None)
        {
            var tokenIndex = ReadToken();
            return new NoneLiteralExpressionSyntax(SpanOf(tokenIndex));
        }

        if (IsNameToken(CurrentToken))
        {
            if (PeekToken(1) == Token.String)
            {
                var prefix = _tokens.GetString(_position);
                if (IsFormattedStringPrefix(prefix))
                {
                    var prefixToken = ReadToken();
                    var stringToken = ReadToken();
                    if (!TryParseFormattedStringLiteral(prefix, _tokens.GetString(stringToken), out var parts))
                    {
                        AddDiagnostic("LA1007", "Invalid string literal.", prefixToken);
                        return null;
                    }

                    return new FormattedStringExpressionSyntax(parts, Merge(SpanOf(prefixToken), SpanOf(stringToken)));
                }

                if (IsBytesStringPrefix(prefix))
                {
                    var prefixToken = ReadToken();
                    var stringToken = ReadToken();
                    if (!TryDecodeBytesLiteral(_tokens.GetString(stringToken), out var bytes))
                    {
                        AddDiagnostic("LA1007", "Invalid string literal.", prefixToken);
                        return null;
                    }

                    return new BytesLiteralExpressionSyntax(bytes, Merge(SpanOf(prefixToken), SpanOf(stringToken)));
                }

                if (TryGetUnsupportedStringPrefix(prefix, out var construct))
                {
                    AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", _position);
                    return null;
                }
            }

            var tokenIndex = ReadToken();
            return new IdentifierExpressionSyntax(
                _tokens.GetString(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.OpenBracket)
        {
            return ParseListLiteral();
        }

        if (CurrentToken == Token.OpenBrace)
        {
            return ParseDictLiteral();
        }

        if (CurrentToken == Token.OpenParen)
        {
            return ParseTupleOrParenthesized();
        }

        AddDiagnostic("LA1000", "Expected expression.", _position);
        return null;
    }

    private ExpressionSyntax? ParseLambdaExpression()
    {
        var lambdaToken = ReadToken();
        if (!TryParseFunctionParameters(Token.Colon, "lambda", allowAnnotations: false, out var parameters, out _))
        {
            return null;
        }

        var body = ParseExpression();
        if (body is null)
        {
            AddDiagnostic("LA1072", "Expected expression body in lambda.", _position);
            return null;
        }

        return new LambdaExpressionSyntax(parameters, body, Merge(SpanOf(lambdaToken), body.Span));
    }

    private ExpressionSyntax? ParseListLiteral()
    {
        var openBracket = ReadToken();
        var items = new List<ExpressionSyntax>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.CloseBracket)
        {
            while (true)
            {
                var item = ParseExpression();
                if (item is null)
                {
                    return null;
                }

                items.Add(item);
                SkipGroupedExpressionTrivia();

                if (CurrentToken == Token.For)
                {
                    if (items.Count != 1)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'comprehension'.", _position);
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

        if (CurrentToken == Token.CloseParen)
        {
            var closeEmpty = ReadToken();
            return new TupleLiteralExpressionSyntax(Array.Empty<ExpressionSyntax>(), Merge(SpanOf(openParen), SpanOf(closeEmpty)));
        }

        var first = ParseExpression();
        if (first is null)
        {
            return null;
        }
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.Comma)
        {
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
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            SkipGroupedExpressionTrivia();
            if (CurrentToken == Token.CloseParen)
            {
                break;
            }

            var item = ParseExpression();
            if (item is null)
            {
                return null;
            }

            items.Add(item);
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
        var items = new List<KeyValuePair<ExpressionSyntax, ExpressionSyntax>>();
        var setItems = new List<ExpressionSyntax>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.CloseBrace)
        {
            while (true)
            {
                var key = ParseExpression();
                if (key is null)
                {
                    return null;
                }
                SkipGroupedExpressionTrivia();

                if (!TryRead(Token.Colon, out var colonToken))
                {
                    if (CurrentToken == Token.For)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'comprehension'.", _position);
                    }
                    else if (CurrentToken is Token.Comma or Token.CloseBrace)
                    {
                        setItems.Add(key);
                        while (CurrentToken == Token.Comma)
                        {
                            ReadToken();
                            SkipGroupedExpressionTrivia();
                            if (CurrentToken == Token.CloseBrace)
                            {
                                break;
                            }

                            var setItem = ParseExpression();
                            if (setItem is null)
                            {
                                return null;
                            }

                            setItems.Add(setItem);
                            SkipGroupedExpressionTrivia();
                        }

                        SkipGroupedExpressionTrivia();
                        if (!TryRead(Token.CloseBrace, out var closeSet))
                        {
                            AddDiagnostic("LA1026", "Expected '}' after set literal.", openBrace);
                            return null;
                        }

                        return new SetLiteralExpressionSyntax(setItems, Merge(SpanOf(openBrace), SpanOf(closeSet)));
                    }
                    else
                    {
                        AddDiagnostic("LA1024", "Expected ':' in dictionary literal.", key.Span);
                    }

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

                items.Add(new KeyValuePair<ExpressionSyntax, ExpressionSyntax>(key, value));

                if (CurrentToken == Token.For)
                {
                    if (items.Count != 1)
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

        return new DictLiteralExpressionSyntax(items, Merge(SpanOf(openBrace), SpanOf(closeBrace)));
    }

    private bool TryParseComprehensionClauses(
        out IReadOnlyList<ComprehensionClauseSyntax> clauses,
        out LythonSourceSpan span)
    {
        clauses = Array.Empty<ComprehensionClauseSyntax>();
        span = new LythonSourceSpan(0, 0, 0, 0);

        var parsedClauses = new List<ComprehensionClauseSyntax>();
        while (true)
        {
            SkipGroupedExpressionTrivia();
            var forToken = ReadToken();
            SkipGroupedExpressionTrivia();
            if (!TryParseLoopTarget(out var parsedTarget, out var targetToken))
            {
                AddDiagnostic("LA1015", "Expected loop variable after 'for'.", forToken);
                return false;
            }

            SkipGroupedExpressionTrivia();
            if (!TryRead(Token.In, out _))
            {
                AddDiagnostic("LA1016", "Expected 'in' in comprehension.", targetToken);
                return false;
            }

            SkipGroupedExpressionTrivia();
            var iterable = ParseComprehensionIterableExpression();
            if (iterable is null)
            {
                AddDiagnostic("LA1017", "Expected iterable expression in comprehension.", targetToken);
                return false;
            }
            SkipGroupedExpressionTrivia();

            ExpressionSyntax? condition = null;
            if (CurrentToken == Token.If)
            {
                ReadToken();
                SkipGroupedExpressionTrivia();
                condition = ParseExpression();
                if (condition is null)
                {
                    AddDiagnostic("LA1010", "Expected condition after 'if'.", _position);
                    return false;
                }
                SkipGroupedExpressionTrivia();
            }

            parsedClauses.Add(new ComprehensionClauseSyntax(
                parsedTarget,
                iterable,
                condition,
                Merge(SpanOf(forToken), (condition ?? iterable).Span)));

            SkipGroupedExpressionTrivia();
            if (CurrentToken != Token.For)
            {
                break;
            }
        }

        clauses = parsedClauses;
        span = Merge(parsedClauses[0].Span, parsedClauses[^1].Span);
        return true;
    }

    private bool TryParseLoopTarget(out LoopTargetSyntax target, out int tokenIndex)
    {
        target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
        tokenIndex = _position;

        if (!TryParseLoopTargetAtom(out var first, out tokenIndex))
        {
            return false;
        }

        if (CurrentToken != Token.Comma)
        {
            target = first;
            return true;
        }

        var items = new List<LoopTargetSyntax> { first };
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            if (!TryParseLoopTargetAtom(out var item, out _))
            {
                AddDiagnostic("LA1015", "Expected loop variable after ','.", _position);
                return false;
            }

            items.Add(item);
        }

        target = new LoopTupleTargetSyntax(items);
        return true;
    }

    private bool TryParseLoopTargetAtom(out LoopTargetSyntax target, out int tokenIndex)
    {
        if (TryReadNameToken(out tokenIndex))
        {
            target = new LoopNameTargetSyntax(_tokens.GetString(tokenIndex));
            return true;
        }

        if (TryRead(Token.OpenParen, out var openParen))
        {
            if (!TryParseLoopTarget(out target, out _))
            {
                target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
                return false;
            }

            if (!TryRead(Token.CloseParen, out _))
            {
                AddDiagnostic("LA1045", "Expected ')' after loop target.", _position);
                target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
                return false;
            }

            tokenIndex = openParen;
            return true;
        }

        target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
        tokenIndex = _position;
        return false;
    }

    private ExpressionSyntax? ParseComprehensionIterableExpression()
    {
        var expression = ParseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (TryGetUnsupportedTrailingExpressionConstruct(CurrentToken, out var construct))
        {
            AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", _position);
            return null;
        }

        return expression;
    }

    private IReadOnlyList<StatementSyntax>? ParseSuite(string code, string message)
    {
        if (TryRead(Token.Eol, out _))
        {
            return ParseIndentedSuite(code, message);
        }

        return ParseSimpleStatementSuite(code);
    }

    private IReadOnlyList<StatementSyntax>? ParseSimpleStatementSuite(string code)
    {
        var statements = new List<StatementSyntax>();
        while (true)
        {
            var statement = ParseSimpleStatement();
            if (statement is null)
            {
                return null;
            }

            statements.Add(statement);
            DrainPendingStatements(statements);

            if (CurrentToken != Token.Semicolon)
            {
                break;
            }

            ReadToken();
            if (CurrentToken is Token.Eol or Token.Dedent or Token.End)
            {
                break;
            }
        }

        if (CurrentToken == Token.Eol)
        {
            ReadToken();
            SkipEndOfLines();
            return statements;
        }

        if (CurrentToken is Token.Dedent or Token.End)
        {
            return statements;
        }

        AddDiagnostic(code, "Expected end-of-line after one-line suite.", _position);
        return null;
    }

    private void DrainPendingStatements(List<StatementSyntax> statements)
    {
        while (_pendingStatements.Count != 0)
        {
            statements.Add(_pendingStatements.Dequeue());
        }
    }

    private IReadOnlyList<StatementSyntax>? ParseIndentedSuite(string code, string message)
    {
        if (CurrentToken == Token.Eol)
        {
            AddDiagnostic(code, message, _position);
            return null;
        }

        if (!TryRead(Token.Indent, out _))
        {
            AddDiagnostic(code, message, _position);
            return null;
        }

        var statements = new List<StatementSyntax>();
        SkipEndOfLines();

        while (CurrentToken is not Token.Dedent and not Token.End)
        {
            var statement = ParseStatement();
            if (statement is null)
            {
                Synchronize();
            }
            else
            {
                statements.Add(statement);
            }

            SkipEndOfLines();
        }

        if (statements.Count == 0)
        {
            AddDiagnostic(code, "Expected at least one statement in block.", _position);
            return null;
        }

        if (!TryRead(Token.Dedent, out _))
        {
            AddDiagnostic(code, "Expected dedent after block.", _position);
            return null;
        }

        return statements;
    }

    private void Synchronize()
    {
        if (CurrentToken == Token.Dedent)
        {
            _position++;
            return;
        }

        while (CurrentToken is not Token.End and not Token.Eol and not Token.Dedent and not Token.Semicolon)
        {
            _position++;
        }
    }

    private void SkipEndOfLines()
    {
        while (CurrentToken is Token.Eol or Token.Semicolon)
        {
            _position++;
        }
    }

    private void SkipGroupedExpressionTrivia()
    {
        while (CurrentToken is Token.Eol or Token.Indent or Token.Dedent)
        {
            _position++;
        }
    }

    private void SkipGroupedImportTrivia()
    {
        while (CurrentToken is Token.Eol or Token.Indent or Token.Dedent)
        {
            _position++;
        }
    }

    private Token CurrentToken
    {
        get
        {
            SkipImplicitLineJoinTrivia();
            return _tokens.Tokens[_position].Token;
        }
    }

    private Token PeekToken(int offset)
    {
        SkipImplicitLineJoinTrivia();
        var position = _position;
        for (var remaining = offset; remaining > 0; remaining--)
        {
            position++;
            while (IsImplicitLineJoinTrivia(position))
            {
                position++;
            }
        }

        return position < _tokens.Count ? _tokens.Tokens[position].Token : Token.End;
    }

    private int ReadToken()
    {
        SkipImplicitLineJoinTrivia();
        var tokenIndex = _position;
        _position++;
        return tokenIndex;
    }

    private bool TryRead(Token token, out int tokenIndex)
    {
        if (CurrentToken == token)
        {
            tokenIndex = ReadToken();
            return true;
        }

        tokenIndex = -1;
        return false;
    }

    private bool TryReadNameToken(out int tokenIndex)
    {
        if (IsNameToken(CurrentToken))
        {
            tokenIndex = ReadToken();
            return true;
        }

        tokenIndex = -1;
        return false;
    }

    private bool TryReadMemberName(out int tokenIndex) => TryReadNameToken(out tokenIndex);

    private static bool IsNameToken(Token token) => token is Token.Identifier or Token.Match or Token.Case;

    private void SkipImplicitLineJoinTrivia()
    {
        while (IsImplicitLineJoinTrivia(_position))
        {
            _position++;
        }
    }

    private bool IsImplicitLineJoinTrivia(int position)
        => position >= 0 &&
           position < _implicitLineJoinTrivia.Length &&
           _implicitLineJoinTrivia[position];

    private static bool[] ComputeImplicitLineJoinTrivia(LexerResult<Token> tokens)
    {
        var hidden = new bool[tokens.Count];
        var depth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens.Tokens[i].Token;
            if (depth > 0 && token is Token.Eol or Token.Indent or Token.Dedent)
            {
                hidden[i] = true;
                continue;
            }

            switch (token)
            {
                case Token.OpenParen:
                case Token.OpenBracket:
                case Token.OpenBrace:
                    depth++;
                    break;
                case Token.CloseParen:
                case Token.CloseBracket:
                case Token.CloseBrace:
                    if (depth > 0)
                    {
                        depth--;
                    }
                    break;
            }
        }

        return hidden;
    }

    private void ReadExpected(Token token, string code, string message)
    {
        if (TryRead(token, out _))
        {
            return;
        }

        AddDiagnostic(code, message, _position);
    }

    private void AddDiagnostic(string code, string message, int tokenIndex)
    {
        var span = tokenIndex >= 0 && tokenIndex < _tokens.Count
            ? SpanOf(tokenIndex)
            : null;

        _diagnostics.Add(new LythonDiagnostic(
            code,
            message,
            LythonDiagnosticSeverity.Error,
            span));
    }

    private void AddDiagnostic(string code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(
            code,
            message,
            LythonDiagnosticSeverity.Error,
            span));
    }

    private static bool TryDecodeStringLiteral(string literal, out string value)
    {
        value = string.Empty;

        if (literal.Length < 2)
        {
            return false;
        }

        var index = 0;
        var isRaw = false;
        if (literal[0] is 'r' or 'R')
        {
            isRaw = true;
            index = 1;
        }

        if (index >= literal.Length)
        {
            return false;
        }

        var quote = literal[index];
        var isTriple = index + 2 < literal.Length &&
            literal[index + 1] == quote &&
            literal[index + 2] == quote;
        var prefixLength = isTriple ? 3 : 1;
        var start = index + prefixLength;
        var end = literal.Length - prefixLength;

        if ((quote != '"' && quote != '\'') || end < start)
        {
            return false;
        }

        if (!isTriple && literal[^1] != quote)
        {
            return false;
        }

        if (isTriple &&
            (literal[^1] != quote || literal[^2] != quote || literal[^3] != quote))
        {
            return false;
        }

        if (isRaw)
        {
            value = literal[start..end];
            return true;
        }

        var builder = new System.Text.StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            var c = literal[i];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (i + 1 >= end)
            {
                return false;
            }

            i++;
            switch (literal[i])
            {
                case '\\':
                    builder.Append('\\');
                    break;
                case '\'':
                    builder.Append('\'');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'x':
                    if (i + 2 >= end ||
                        !byte.TryParse(literal.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
                    {
                        return false;
                    }

                    builder.Append((char)hex);
                    i += 2;
                    break;
                default:
                    builder.Append(literal[i]);
                    break;
            }
        }

        value = builder.ToString();
        return true;
    }

    private static bool IsSupportedImport(string moduleName)
    {
        return !string.IsNullOrWhiteSpace(moduleName);
    }

    private static bool IsAugmentedAssignmentToken(Token token)
    {
        return token is
            Token.PlusEqual or
            Token.MinusEqual or
            Token.StarEqual or
            Token.SlashEqual or
            Token.PercentEqual or
            Token.SlashSlashEqual or
            Token.StarStarEqual or
            Token.AmpersandEqual or
            Token.PipeEqual or
            Token.CaretEqual or
            Token.LessLessEqual or
            Token.GreaterGreaterEqual;
    }

    private static bool TryMapAugmentedAssignmentOperator(Token token, out AugmentedAssignmentOperatorSyntax op)
    {
        switch (token)
        {
            case Token.PlusEqual:
                op = AugmentedAssignmentOperatorSyntax.Add;
                return true;
            case Token.MinusEqual:
                op = AugmentedAssignmentOperatorSyntax.Subtract;
                return true;
            case Token.StarEqual:
                op = AugmentedAssignmentOperatorSyntax.Multiply;
                return true;
            case Token.SlashEqual:
                op = AugmentedAssignmentOperatorSyntax.Divide;
                return true;
            case Token.SlashSlashEqual:
                op = AugmentedAssignmentOperatorSyntax.FloorDivide;
                return true;
            case Token.PercentEqual:
                op = AugmentedAssignmentOperatorSyntax.Modulo;
                return true;
            default:
                op = default;
                return false;
        }
    }

    private static bool TryGetUnsupportedStringPrefix(string text, out string construct)
    {
        construct = text switch
        {
            _ => string.Empty
        };

        return construct.Length != 0;
    }

    private static bool IsBytesStringPrefix(string text)
    {
        return text is "b" or "B" or "br" or "Br" or "bR" or "BR" or "rb" or "rB" or "Rb" or "RB";
    }

    private static bool IsFormattedStringPrefix(string text)
    {
        return text is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
    }

    private static bool TryDecodeBytesLiteral(string literal, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (!TryDecodeStringLiteral(literal, out var decoded))
        {
            return false;
        }

        value = new byte[decoded.Length];
        for (var i = 0; i < decoded.Length; i++)
        {
            if (decoded[i] > byte.MaxValue)
            {
                return false;
            }

            value[i] = (byte)decoded[i];
        }

        return true;
    }

    private static bool TryParseFormattedStringLiteral(string prefix, string literal, out IReadOnlyList<FormattedStringPartSyntax> parts)
    {
        parts = Array.Empty<FormattedStringPartSyntax>();
        var isRaw = prefix.Contains('r', StringComparison.OrdinalIgnoreCase);
        if (!TryExtractStringContent(literal, out var content))
        {
            return false;
        }

        var parsedParts = new List<FormattedStringPartSyntax>();
        var text = new System.Text.StringBuilder();

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '{')
            {
                if (i + 1 < content.Length && content[i + 1] == '{')
                {
                    text.Append('{');
                    i++;
                    continue;
                }

                if (text.Length > 0)
                {
                    if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText))
                    {
                        return false;
                    }

                    parsedParts.Add(new FormattedStringTextPartSyntax(decodedText));
                    text.Clear();
                }

                if (!TryParseFormattedStringField(content, i + 1, out var end, out var expressionPart))
                {
                    return false;
                }

                parsedParts.Add(expressionPart);
                i = end;
                continue;
            }

            if (c == '}')
            {
                if (i + 1 < content.Length && content[i + 1] == '}')
                {
                    text.Append('}');
                    i++;
                    continue;
                }

                return false;
            }

            text.Append(c);
        }

        if (text.Length > 0)
        {
            if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText))
            {
                return false;
            }

            parsedParts.Add(new FormattedStringTextPartSyntax(decodedText));
        }

        parts = parsedParts;
        return true;
    }

    private static bool TryParseFormattedStringField(
        string content,
        int start,
        out int end,
        out FormattedStringExpressionPartSyntax part)
    {
        end = -1;
        part = null!;

        if (!TryFindFormattedStringFieldEnd(content, start, out end))
        {
            return false;
        }

        var field = content[start..end];
        if (!TrySplitFormattedStringField(field, out var expressionText, out var conversion, out var formatSpecifier) ||
            expressionText.Length == 0 ||
            !TryParseEmbeddedExpression(expressionText, out var expression))
        {
            return false;
        }

        part = new FormattedStringExpressionPartSyntax(expression, conversion, formatSpecifier);
        return true;
    }

    private static bool TryFindFormattedStringFieldEnd(string content, int start, out int end)
    {
        end = -1;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];
            if (c is '"' or '\'')
            {
                if (!TrySkipStringLiteral(content, ref i))
                {
                    return false;
                }

                continue;
            }

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth == 0)
                    {
                        return false;
                    }

                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        return false;
                    }

                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        end = i;
                        return true;
                    }

                    if (braceDepth == 0)
                    {
                        return false;
                    }

                    braceDepth--;
                    break;
            }
        }

        return false;
    }

    private static bool TrySplitFormattedStringField(
        string field,
        out string expressionText,
        out char? conversion,
        out string? formatSpecifier)
    {
        expressionText = string.Empty;
        conversion = null;
        formatSpecifier = null;

        if (!TryFindFormattedStringSeparators(field, out var conversionIndex, out var formatIndex))
        {
            return false;
        }

        var expressionEnd = conversionIndex >= 0
            ? conversionIndex
            : formatIndex >= 0
                ? formatIndex
                : field.Length;
        expressionText = field[..expressionEnd].Trim();

        if (conversionIndex >= 0)
        {
            if (conversionIndex + 1 >= field.Length)
            {
                return false;
            }

            conversion = field[conversionIndex + 1];
            if (conversion is not ('s' or 'r' or 'a'))
            {
                return false;
            }

            var afterConversion = conversionIndex + 2;
            if (afterConversion < field.Length)
            {
                if (field[afterConversion] != ':')
                {
                    return false;
                }

                formatIndex = afterConversion;
            }
        }

        if (formatIndex >= 0)
        {
            formatSpecifier = field[(formatIndex + 1)..];
            if (formatSpecifier.Contains('{', StringComparison.Ordinal) ||
                formatSpecifier.Contains('}', StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryFindFormattedStringSeparators(string field, out int conversionIndex, out int formatIndex)
    {
        conversionIndex = -1;
        formatIndex = -1;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var i = 0; i < field.Length; i++)
        {
            var c = field[i];
            if (c is '"' or '\'')
            {
                if (!TrySkipStringLiteral(field, ref i))
                {
                    return false;
                }

                continue;
            }

            if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                if (c == ':' && formatIndex < 0)
                {
                    formatIndex = i;
                    return true;
                }

                if (c == '!' && i + 1 < field.Length && field[i + 1] != '=' && conversionIndex < 0)
                {
                    conversionIndex = i;
                    continue;
                }
            }

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth == 0)
                    {
                        return false;
                    }

                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        return false;
                    }

                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (braceDepth == 0)
                    {
                        return false;
                    }

                    braceDepth--;
                    break;
            }
        }

        return parenDepth == 0 && bracketDepth == 0 && braceDepth == 0;
    }

    private static bool TrySkipStringLiteral(string text, ref int index)
    {
        var quote = text[index];
        var triple = index + 2 < text.Length && text[index + 1] == quote && text[index + 2] == quote;
        index += triple ? 3 : 1;

        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (triple)
            {
                if (index + 2 < text.Length &&
                    text[index] == quote &&
                    text[index + 1] == quote &&
                    text[index + 2] == quote)
                {
                    index += 2;
                    return true;
                }

                index++;
                continue;
            }

            if (text[index] == quote)
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool TryParseEmbeddedExpression(string expressionText, out ExpressionSyntax expression)
    {
        expression = null!;
        var frontend = LythonFrontend.Compile("value = " + expressionText + "\n");
        if (frontend.Script?.Statements is not [AssignmentStatementSyntax assignment] || frontend.Diagnostics.Count != 0)
        {
            return false;
        }

        expression = assignment.Expression;
        return true;
    }

    private static bool TryExtractStringContent(string literal, out string content)
    {
        content = string.Empty;
        if (literal.Length < 2)
        {
            return false;
        }

        if ((literal.StartsWith("\"\"\"", StringComparison.Ordinal) && literal.EndsWith("\"\"\"", StringComparison.Ordinal)) ||
            (literal.StartsWith("'''", StringComparison.Ordinal) && literal.EndsWith("'''", StringComparison.Ordinal)))
        {
            content = literal[3..^3];
            return true;
        }

        if ((literal[0] == '"' && literal[^1] == '"') || (literal[0] == '\'' && literal[^1] == '\''))
        {
            content = literal[1..^1];
            return true;
        }

        return false;
    }

    private static bool TryDecodeEscapedText(string text, bool isRaw, out string value)
    {
        if (isRaw)
        {
            value = text;
            return true;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\')
            {
                builder.Append(text[i]);
                continue;
            }

            if (i + 1 >= text.Length)
            {
                value = string.Empty;
                return false;
            }

            i++;
            builder.Append(text[i] switch
            {
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => text[i],
            });
        }

        value = builder.ToString();
        return true;
    }

    private static bool TryGetUnsupportedTrailingExpressionConstruct(Token token, out string construct)
    {
        construct = token switch
        {
            Token.PlusEqual or Token.MinusEqual or Token.StarEqual or Token.SlashEqual or Token.PercentEqual or Token.SlashSlashEqual => "augmented assignment",
            _ => string.Empty
        };

        return construct.Length != 0;
    }

    private bool TryParseUnsupportedStatement(out StatementSyntax? statement)
    {
        if (CurrentToken is Token.From or Token.Del)
        {
            statement = null;
            return false;
        }

        if (!TryGetUnsupportedConstruct(CurrentToken, out var construct))
        {
            statement = null;
            return false;
        }

        var tokenIndex = ReadToken();
        AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", tokenIndex);
        statement = null;
        return true;
    }

    private bool TryParseUnsupportedExpression(out ExpressionSyntax? expression)
    {
        if (CurrentToken == Token.Lambda)
        {
            expression = null;
            return false;
        }

        if (!TryGetUnsupportedConstruct(CurrentToken, out var construct))
        {
            expression = null;
            return false;
        }

        var tokenIndex = ReadToken();
        AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", tokenIndex);
        expression = null;
        return true;
    }

    private bool LooksLikeMatchStatement()
    {
        if (PeekToken(1) is Token.Assign or Token.ColonEqual)
        {
            return false;
        }

        for (var offset = 1; ; offset++)
        {
            var token = PeekToken(offset);
            switch (token)
            {
                case Token.Eol:
                case Token.End:
                    return false;
                case Token.Assign:
                    return false;
                case Token.Colon:
                    return true;
            }
        }
    }

    private static bool TryGetUnsupportedConstruct(Token token, out string construct)
    {
        construct = token switch
        {
            Token.Elif => "elif",
            Token.With => "with",
            Token.Yield => "yield",
            Token.Async => "async",
            Token.Await => "await",
            Token.Lambda => "lambda",
            Token.Global => "global",
            Token.Nonlocal => "nonlocal",
            Token.Del => "del",
            Token.From => "from import",
            Token.At => "decorator",
            _ => string.Empty,
        };

        return construct.Length != 0;
    }

    private LythonSourceSpan SpanOf(int tokenIndex)
    {
        var token = _tokens.Tokens[tokenIndex];
        _tokens.LineOfPosition(token.Start, out var line, out var column);
        return new LythonSourceSpan(token.Start, token.Length, line, column);
    }

    private LythonSourceSpan Merge(int leftTokenIndex, int rightTokenIndex)
    {
        return Merge(SpanOf(leftTokenIndex), SpanOf(rightTokenIndex));
    }

    private LythonSourceSpan Merge(int leftTokenIndex, LythonSourceSpan rightSpan)
    {
        return Merge(SpanOf(leftTokenIndex), rightSpan);
    }

    private static LythonSourceSpan Merge(LythonSourceSpan left, LythonSourceSpan right)
    {
        var start = Math.Min(left.Start, right.Start);
        var end = Math.Max(left.Start + left.Length, right.Start + right.Length);
        var line = left.Start <= right.Start ? left.Line : right.Line;
        var column = left.Start <= right.Start ? left.Column : right.Column;
        return new LythonSourceSpan(start, end - start, line, column);
    }
}
