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

}
