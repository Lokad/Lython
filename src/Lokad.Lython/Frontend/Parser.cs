using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private readonly LexerResult<Token> _tokens;
    private readonly bool[] _implicitLineJoinTrivia;
    private readonly Queue<StatementSyntax> _pendingStatements = new();
    private readonly List<LythonDiagnostic> _diagnostics = new();
    private int _position;
    private int _functionDepth;
    private int _unaryOperatorDepth;
    private int _nestingDepth;

    /// <summary>Maximum simultaneous parser nesting levels. Sized from isolated
    /// crash probes (descent-heavy shapes overflow near 190 levels on 1MB stacks;
    /// archive/executable builders tolerate less than 600) with at least 2x margin.</summary>
    internal const int MaxNestingDepth = 64;

    public Parser(LexerResult<Token> tokens)
    {
        _tokens = tokens;
        _implicitLineJoinTrivia = ComputeImplicitLineJoinTrivia(tokens);
    }

    public FrontendResult Parse()
    {
        var statements = new List<StatementSyntax>();

        ValidateNumberIdentifierGluing();
        SkipEndOfLines();

        StatementSyntax? previousStatement = null;
        var previousEndTokenIndex = 0;
        while (CurrentToken != Token.End)
        {
            var gapStartTokenIndex = _position;
            var pendingBeforeParse = _pendingStatements.Count;
            var statement = ParseStatement();
            if (statement is null)
            {
                Synchronize();
                previousStatement = null;
            }
            else
            {
                statements.Add(statement);
                if (previousStatement is not null)
                {
                    CheckStatementSeparation(previousStatement.Span, statement.Span, previousEndTokenIndex, gapStartTokenIndex);
                }

                previousStatement = statement;
                previousEndTokenIndex = SeparationGapEnd(previousEndTokenIndex, gapStartTokenIndex, pendingBeforeParse);
            }

            SkipEndOfLines();
        }

        if (_diagnostics.Count > 0)
        {
            return new FrontendResult(null, _diagnostics);
        }

        return new FrontendResult(new ScriptSyntax(statements), Array.Empty<LythonDiagnostic>());
    }

    // Statements on the same source line must be separated by a semicolon.
    // Compares statement spans (not bare tokens) so compound statements ending
    // on an earlier line never trip: only a strictly advancing statement that
    // starts where the previous one ended fails. Queued splits (import a, b)
    // overlap and are exempt.
    private void CheckStatementSeparation(LythonSourceSpan previousSpan, LythonSourceSpan currentSpan, int gapStartTokenIndex, int gapEndTokenIndex)
    {
        var previousEnd = previousSpan.Start + previousSpan.Length;
        // Strictly overlapping only: merely abutting spans (f(1)f(2)) still
        if (currentSpan.Start < previousEnd)
        {
            return;
        }

        _tokens.LineOfPosition(previousEnd, out var previousLine, out _);
        _tokens.LineOfPosition(currentSpan.Start, out var currentLine, out _);
        if (currentLine != previousLine)
        {
            return;
        }

        for (var index = gapStartTokenIndex; index < gapEndTokenIndex; index++)
        {
            if (_tokens.Tokens[index].Token == Token.Semicolon)
            {
                return;
            }
        }

        AddDiagnostic("LA1001", "Expected end-of-line after statement.", currentSpan);
    }

    // Queued splits (import a, b) drain without consuming tokens: the separator
    // after the head statement was already skipped, so the gap edge stays where
    // the head left it instead of collapsing the separation window to empty.
    private int SeparationGapEnd(int previousEndTokenIndex, int gapStartTokenIndex, int pendingBeforeParse)
        => pendingBeforeParse != 0 && _position == gapStartTokenIndex
            ? previousEndTokenIndex
            : _position;

    // Numbers glued to identifiers (1x, 0x1F, 1j) lex as two adjacent tokens
    // but are one invalid literal in Python. Keywords lex as their own tokens,
    // so `1in[1,2]` stays valid: only a true Identifier abutting the number
    // fails. Runs before parsing; diagnostics accumulate with the parse below.
    private void ValidateNumberIdentifierGluing()
    {
        for (var index = 0; index + 1 < _tokens.Count; index++)
        {
            var current = _tokens.Tokens[index];
            if (current.Token is not Token.Integer and not Token.Float)
            {
                continue;
            }

            var next = _tokens.Tokens[index + 1];
            if (next.Token == Token.Identifier && next.Start == current.Start + current.Length)
            {
                AddDiagnostic("LA1009", "Invalid number literal.", Merge(index, index + 1));
            }
        }
    }

    private StatementSyntax? ParseStatement()
    {
        if (_pendingStatements.Count != 0)
        {
            return _pendingStatements.Dequeue();
        }

        if (CurrentToken == Token.Indent)
        {
            AddDiagnostic("LA1000", "Unexpected indentation.", _position);
            return null;
        }

        if (CurrentToken == Token.Dedent)
        {
            AddDiagnostic("LA1000", "Unexpected dedentation.", _position);
            return null;
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

        if (CurrentToken is Token.Global or Token.Nonlocal)
        {
            return ParseScopeDirectiveStatement();
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

        if (CurrentToken is Token.Global or Token.Nonlocal)
        {
            return ParseScopeDirectiveStatement();
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

        if (IsNameToken(CurrentToken))
        {
            var startDiagnosticCount = _diagnostics.Count;
            var augmentedAssignment = TryParseAugmentedAssignmentStatement();
            if (augmentedAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return augmentedAssignment;
            }
        }

        if (CurrentToken == Token.Star || IsUnpackingAssignmentStart())
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

            var complexAnnotatedAssignment = TryParseComplexAnnotatedAssignmentStatement();
            if (complexAnnotatedAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return complexAnnotatedAssignment;
            }
        }

        // A leading parenthesis may parenthesize a single assignment target like CPython.
        if (CurrentToken == Token.OpenParen)
        {
            var startDiagnosticCount = _diagnostics.Count;
            var parenthesizedAugmentedAssignment = TryParseAugmentedAssignmentStatement();
            if (parenthesizedAugmentedAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return parenthesizedAugmentedAssignment;
            }

            var parenthesizedAssignment = TryParsePostfixAssignmentStatement();
            if (parenthesizedAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return parenthesizedAssignment;
            }

            var parenthesizedAnnotatedAssignment = TryParseComplexAnnotatedAssignmentStatement();
            if (parenthesizedAnnotatedAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return parenthesizedAnnotatedAssignment;
            }
        }

        // A leading bracket may open a list-display unpacking target like CPython.
        if (CurrentToken == Token.OpenBracket)
        {
            var startDiagnosticCount = _diagnostics.Count;
            var listDisplayAssignment = TryParsePostfixAssignmentStatement();
            if (listDisplayAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return listDisplayAssignment;
            }
        }

        {
            var startDiagnosticCount = _diagnostics.Count;
            var unsupportedTargetAssignment = TryParseUnsupportedAssignmentTargetStatement();
            if (unsupportedTargetAssignment is not null || _diagnostics.Count != startDiagnosticCount)
            {
                return unsupportedTargetAssignment;
            }
        }

        var expression = ParseExpressionList();
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

        // Elif clauses read as a flat chain but execute as nested statements:
        // collect them first, then fold back-to-front so every clause (and a
        // trailing else) attaches to the deepest node. Each clause holds one
        // level of the shared syntax-nesting budget for the rest of this
        // statement, bounding lowering recursion exactly like nested suites.
        var elifClauses = new List<(ExpressionSyntax Condition, IReadOnlyList<StatementSyntax> Body, LythonSourceSpan Span)>();
        var heldElifDepths = 0;
        try
        {
            while (CurrentToken == Token.Elif)
            {
                if (!EnterNestingDepth(_position))
                {
                    return null;
                }

                heldElifDepths++;
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

                elifClauses.Add((elifCondition, elifBody, Merge(SpanOf(elifToken), elifBody[^1].Span)));
            }

            IReadOnlyList<StatementSyntax>? elifTail = null;
            LythonSourceSpan tailEnd = thenStatements[^1].Span;
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

                elifTail = parsedElse;
                tailEnd = parsedElse[^1].Span;
            }

            for (var i = elifClauses.Count - 1; i >= 0; i--)
            {
                var (clauseCondition, clauseBody, clauseSpan) = elifClauses[i];
                var nodeSpan = Merge(clauseSpan, tailEnd);
                elifTail = [new IfStatementSyntax(clauseCondition, clauseBody, elifTail, nodeSpan)];
                tailEnd = nodeSpan;
            }

            elseStatements = elifTail;
            span = Merge(span, tailEnd);
        }
        finally
        {
            while (heldElifDepths-- > 0)
            {
                LeaveNestingDepth();
            }
        }

        _ = colonToken;
        return new IfStatementSyntax(condition, thenStatements, elseStatements, span);
    }

}
