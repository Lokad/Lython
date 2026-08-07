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
}
