using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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

    private string IdentifierText(int tokenIndex)
        => _tokens.GetString(tokenIndex).Normalize(NormalizationForm.FormKC);

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
        var groupedIndentDepth = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens.Tokens[i].Token;
            if (depth > 0 && token is Token.Eol or Token.Indent or Token.Dedent)
            {
                hidden[i] = true;
                if (token == Token.Indent)
                {
                    groupedIndentDepth++;
                }
                else if (token == Token.Dedent && groupedIndentDepth > 0)
                {
                    groupedIndentDepth--;
                }

                continue;
            }

            // Lokad.Parsing's indentation stack observes physical continuation
            // indentation even though Python ignores it inside delimiters. When a
            // closing delimiter stays on that continuation indentation, the lexer
            // emits the balancing Dedent only on the following physical line. Hide
            // exactly those delayed balances, while retaining any enclosing-suite
            // Dedent that follows them.
            if (depth == 0 && token == Token.Dedent && groupedIndentDepth > 0)
            {
                hidden[i] = true;
                groupedIndentDepth--;
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

    private void AddDiagnostic(LythonDiagnosticCode code, string message, int tokenIndex)
    {
        var span = tokenIndex >= 0 && tokenIndex < _tokens.Count
            ? SpanOf(tokenIndex)
            : null;

        _diagnostics.Add(new LythonDiagnostic(
            code.Value,
            message,
            LythonDiagnosticSeverity.Error,
            span));
    }

    private void AddDiagnostic(LythonDiagnosticCode code, string message, LythonSourceSpan span)
    {
        _diagnostics.Add(new LythonDiagnostic(
            code.Value,
            message,
            LythonDiagnosticSeverity.Error,
            span));
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
