using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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

        var iterable = ParseExpressionList();
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
            IdentifierText(nameToken),
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
        var options = DataclassDecoratorSyntax.CreateDefault(span);

        switch (expression)
        {
            case IdentifierExpressionSyntax { Name: "dataclass" }:
                decorator = options;
                return true;
            case MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "dataclasses" },
                MemberName: "dataclass"
            }:
                decorator = options;
                return true;
            case CallExpressionSyntax { Target: var target, Arguments: var arguments } when IsDataclassDecoratorTarget(target):
                foreach (var argument in arguments)
                {
                    if (argument.Kind is not CallArgumentKind.Keyword)
                    {
                        AddDiagnostic("LA1107", "@dataclass expects keyword boolean options only.", argument.Expression.Span);
                        decorator = null;
                        return true;
                    }

                    if (argument.Expression is not BooleanLiteralExpressionSyntax boolean)
                    {
                        AddDiagnostic("LA1110", "@dataclass options expect True or False.", argument.Expression.Span);
                        decorator = null;
                        return true;
                    }

                    switch (argument.KeywordName)
                    {
                        case "init":
                            options = options with { Init = boolean.Value };
                            break;
                        case "repr":
                            options = options with { Repr = boolean.Value };
                            break;
                        case "eq":
                            options = options with { Eq = boolean.Value };
                            break;
                        case "order":
                            options = options with { Order = boolean.Value };
                            break;
                        case "unsafe_hash":
                            options = options with { UnsafeHash = boolean.Value };
                            break;
                        case "frozen":
                            options = options with { Frozen = boolean.Value };
                            break;
                        case "kw_only":
                            options = options with { KwOnly = boolean.Value };
                            break;
                        case "match_args":
                            options = options with { MatchArgs = boolean.Value };
                            break;
                        case "slots":
                        case "weakref_slot":
                            if (boolean.Value)
                            {
                                AddDiagnostic("LA1111", $"Unsupported @dataclass option '{argument.KeywordName}=True'; Lython dataclasses do not implement slots.", argument.Expression.Span);
                                decorator = null;
                                return true;
                            }

                            break;
                        default:
                            AddDiagnostic("LA1111", $"Unsupported @dataclass option '{argument.KeywordName}'.", argument.Expression.Span);
                            decorator = null;
                            return true;
                    }
                }

                decorator = options;
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
                            IdentifierText(keywordNameToken),
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
            IdentifierText(nameToken),
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

            parsed.Add(new FunctionParameterSyntax(IdentifierText(parameterToken), annotation, defaultValue, kind));

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

    private StatementSyntax? ParseImportStatement()
    {
        if (CurrentToken == Token.From)
        {
            return ParseFromImportStatement();
        }

        var importToken = ReadToken();
        var statements = new List<StatementSyntax>();
        while (true)
        {
            if (!TryReadDottedModuleName(
                    "LA1001",
                    statements.Count == 0 ? "Expected module name after 'import'." : "Expected module name after ','.",
                    importToken,
                    out var moduleName,
                    out var moduleStartToken,
                    out var moduleEndToken))
            {
                return null;
            }

            var boundModuleName = moduleName.Split('.')[0];
            var bindingName = boundModuleName;
            var endToken = moduleEndToken;

            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1051", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = IdentifierText(aliasToken);
                boundModuleName = moduleName;
                endToken = aliasToken;
            }

            if (!IsSupportedImport(moduleName))
            {
                AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleStartToken);
                return null;
            }

            statements.Add(new ImportStatementSyntax(
                moduleName,
                bindingName,
                boundModuleName,
                null,
                Merge(importToken, endToken)));

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
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
        if (!TryReadDottedModuleName(
                "LA1052",
                "Expected module name after 'from'.",
                fromToken,
                out var moduleName,
                out var moduleStartToken,
                out var moduleEndToken))
        {
            return null;
        }

        if (!IsSupportedImport(moduleName))
        {
            AddDiagnostic("LA1002", $"Unsupported module '{moduleName}'.", moduleStartToken);
            return null;
        }

        if (!TryRead(Token.Import, out _))
        {
            AddDiagnostic("LA1053", "Expected 'import' after module name.", moduleEndToken);
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

        if (CurrentToken == Token.Star)
        {
            var starToken = ReadToken();
            importedMembers.Add(new ImportedMemberSyntax("*", "*"));
            if (grouped)
            {
                SkipGroupedImportTrivia();
                if (!TryRead(Token.CloseParen, out _))
                {
                    AddDiagnostic("LA1067", "Expected ')' after grouped import list.", _position);
                    return null;
                }
            }

            return new ImportStatementSyntax(
                moduleName,
                moduleName,
                moduleName,
                importedMembers,
                Merge(fromToken, starToken));
        }

        while (true)
        {
            if (!TryRead(Token.Identifier, out var memberToken))
            {
                AddDiagnostic("LA1054", "Expected imported member name.", _position);
                return null;
            }

            var memberName = IdentifierText(memberToken);
            var bindingName = memberName;
            if (CurrentToken == Token.As)
            {
                ReadToken();
                if (!TryRead(Token.Identifier, out var aliasToken))
                {
                    AddDiagnostic("LA1055", "Expected alias name after 'as'.", _position);
                    return null;
                }

                bindingName = IdentifierText(aliasToken);
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
            moduleName,
            importedMembers,
            Merge(fromToken, moduleEndToken));
    }

    private StatementSyntax? ParseScopeDirectiveStatement()
    {
        var directiveToken = ReadToken();
        var kind = _tokens.Tokens[directiveToken].Token == Token.Global
            ? ScopeDirectiveKind.Global
            : ScopeDirectiveKind.Nonlocal;
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var endToken = directiveToken;

        while (true)
        {
            if (!TryReadNameToken(out var nameToken))
            {
                AddDiagnostic(
                    "LA1070",
                    $"Expected identifier after '{(kind == ScopeDirectiveKind.Global ? "global" : "nonlocal")}'.",
                    _position);
                return null;
            }

            var name = IdentifierText(nameToken);
            if (!seen.Add(name))
            {
                AddDiagnostic("LA1071", $"Duplicate scope directive name '{name}'.", nameToken);
                return null;
            }

            names.Add(name);
            endToken = nameToken;

            if (CurrentToken != Token.Comma)
            {
                break;
            }

            ReadToken();
        }

        return new ScopeDirectiveStatementSyntax(kind, names, Merge(directiveToken, endToken));
    }

    private bool TryReadDottedModuleName(
        string diagnosticCode,
        string diagnosticMessage,
        int anchorToken,
        out string moduleName,
        out int startToken,
        out int endToken)
    {
        moduleName = string.Empty;
        startToken = -1;
        endToken = -1;

        if (!TryRead(Token.Identifier, out startToken))
        {
            AddDiagnostic(diagnosticCode, diagnosticMessage, anchorToken);
            return false;
        }

        endToken = startToken;
        var parts = new List<string> { IdentifierText(startToken) };
        while (CurrentToken == Token.Dot)
        {
            ReadToken();
            if (!TryRead(Token.Identifier, out var partToken))
            {
                AddDiagnostic(diagnosticCode, "Expected module name after '.'.", _position);
                return false;
            }

            parts.Add(IdentifierText(partToken));
            endToken = partToken;
        }

        moduleName = string.Join(".", parts);
        return true;
    }

    private StatementSyntax? ParseAssignmentStatement()
    {
        var nameToken = ReadToken();
        ReadExpected(Token.Assign, "LA1003", "Expected '=' in assignment.");

        return ParseAssignmentAfterFirstTarget(
            new NameAssignmentTargetSyntax(IdentifierText(nameToken), SpanOf(nameToken)),
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
            expression = ParseExpressionList();
            if (expression is null)
            {
                AddDiagnostic("LA1060", "Expected expression on the right side of annotated assignment.", nameToken);
                return null;
            }
        }

        return new AnnotatedAssignmentStatementSyntax(
            IdentifierText(nameToken),
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
            SliceExpressionSyntax slice => ParseAssignmentAfterFirstTarget(
                new SliceAssignmentTargetSyntax(slice.Target, slice.Start, slice.End, slice.Step, slice.Span),
                startPosition),
            MemberExpressionSyntax member => ParseAssignmentAfterFirstTarget(
                new MemberAssignmentTargetSyntax(member.Target, member.MemberName, member.Span),
                startPosition),
            _ => AddUnsupportedAssignmentTarget(target)
        };
    }

    private StatementSyntax? TryParseUnsupportedAssignmentTargetStatement()
    {
        var startPosition = _position;
        var startDiagnosticCount = _diagnostics.Count;

        var target = ParsePostfixExpression();
        if (target is not null && CurrentToken is Token.Assign or Token.Colon)
        {
            return AddUnsupportedAssignmentTarget(target);
        }

        _position = startPosition;
        if (_diagnostics.Count > startDiagnosticCount)
        {
            _diagnostics.RemoveRange(startDiagnosticCount, _diagnostics.Count - startDiagnosticCount);
        }

        return null;
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

                variableName = IdentifierText(variableToken);
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

            targets.Add(new UnpackingTargetSyntax(IdentifierText(nameToken), isStarred));
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

    private bool IsUnpackingAssignmentStart()
    {
        var offset = 0;
        if (!IsNameToken(PeekToken(offset)))
        {
            return false;
        }

        offset++;
        var sawComma = false;
        while (PeekToken(offset) == Token.Comma)
        {
            sawComma = true;
            offset++;
            if (PeekToken(offset) == Token.Assign)
            {
                return true;
            }

            if (PeekToken(offset) == Token.Star)
            {
                offset++;
            }

            if (!IsNameToken(PeekToken(offset)))
            {
                return false;
            }

            offset++;
        }

        return sawComma && PeekToken(offset) == Token.Assign;
    }

    private StatementSyntax? ParseAssignmentAfterFirstTarget(AssignmentTargetSyntax firstTarget, int startToken)
    {
        var startDiagnosticCount = _diagnostics.Count;
        var expression = ParseExpressionList();
        if (expression is null)
        {
            if (_diagnostics.Count == startDiagnosticCount)
            {
                AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", startToken);
            }

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

            targets.Add(nextTarget.RequireNotNull());
            ReadToken();

            startDiagnosticCount = _diagnostics.Count;
            currentExpression = ParseExpressionList();
            if (currentExpression is null)
            {
                if (_diagnostics.Count == startDiagnosticCount)
                {
                    AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", startToken);
                }

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
            SliceAssignmentTargetSyntax slice => new SliceAssignmentStatementSyntax(
                slice.Target,
                slice.Start,
                slice.End,
                slice.Step,
                expression,
                Merge(slice.Span, expression.Span)),
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
            case SliceExpressionSyntax slice:
                target = new SliceAssignmentTargetSyntax(slice.Target, slice.Start, slice.End, slice.Step, slice.Span);
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

    private StatementSyntax? TryParseAugmentedAssignmentStatement()
    {
        var startPosition = _position;
        var startDiagnosticCount = _diagnostics.Count;

        var targetExpression = ParsePostfixExpression();
        if (targetExpression is null || !IsAugmentedAssignmentToken(CurrentToken))
        {
            _position = startPosition;
            if (_diagnostics.Count > startDiagnosticCount)
            {
                _diagnostics.RemoveRange(startDiagnosticCount, _diagnostics.Count - startDiagnosticCount);
            }

            return null;
        }

        if (!TryConvertExpressionToAssignmentTarget(targetExpression, out var target) ||
            target is UnpackingAssignmentTargetGroupSyntax)
        {
            AddUnsupportedAssignmentTarget(targetExpression);
            return null;
        }

        var operatorToken = ReadToken();
        if (!TryMapAugmentedAssignmentOperator(_tokens.Tokens[operatorToken].Token, out var op))
        {
            AddDiagnostic("LA2000", "Unsupported Python construct 'augmented assignment'.", operatorToken);
            return null;
        }

        var expression = ParseExpression();
        if (expression is null)
        {
            AddDiagnostic("LA1004", "Expected expression on the right side of assignment.", operatorToken);
            return null;
        }

        return new AugmentedAssignmentStatementSyntax(
            target.RequireNotNull(),
            op,
            expression,
            Merge(targetExpression.Span, expression.Span));
    }

}
