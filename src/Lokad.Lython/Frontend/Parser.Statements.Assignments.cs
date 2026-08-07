using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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
