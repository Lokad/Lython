using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private StatementSyntax? ParseReturnStatement()
    {
        var returnToken = ReadToken();
        if (CurrentToken is Token.Eol or Token.Semicolon or Token.Dedent or Token.End)
        {
            return new ReturnStatementSyntax(null, SpanOf(returnToken));
        }

        var expression = ParseExpressionList();
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
            IdentifierExpressionSyntax or SubscriptExpressionSyntax or SliceExpressionSyntax or MemberExpressionSyntax => new DeleteStatementSyntax(target, Merge(SpanOf(delToken), target.Span)),
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
                        if (!TryReadExceptionTypeName("LA1045", "Expected exception type in except tuple.", out var typeName))
                        {
                            return null;
                        }

                        parsedTypes.Add(typeName);
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
                    if (!TryReadExceptionTypeName("LA1045", "Expected exception type after 'except'.", out var typeName))
                    {
                        return null;
                    }

                    parsedTypes.Add(typeName);
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

                    exceptionVariable = IdentifierText(variableToken);
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

    private bool TryReadExceptionTypeName(string diagnosticCode, string diagnosticMessage, out string typeName)
    {
        typeName = string.Empty;
        if (!TryReadNameToken(out var typeToken))
        {
            AddDiagnostic(diagnosticCode, diagnosticMessage, _position);
            return false;
        }

        typeName = IdentifierText(typeToken);
        while (CurrentToken == Token.Dot)
        {
            ReadToken();
            if (!TryReadNameToken(out var partToken))
            {
                AddDiagnostic(diagnosticCode, "Expected exception type name after '.'.", _position);
                return false;
            }

            typeName = IdentifierText(partToken);
        }

        return true;
    }
}
