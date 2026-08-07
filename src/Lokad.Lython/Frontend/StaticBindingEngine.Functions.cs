using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    private static bool TryBuildSimpleFunctionSummary(
        FunctionDefinitionStatementSyntax functionDefinition,
        AbstractState bindings,
        [MaybeNullWhen(false)] out AbstractFunctionSummary summary)
    {
        if (functionDefinition.Decorators.Count != 0 ||
            functionDefinition.Parameters.Any(static parameter => parameter.Kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary) ||
            ScopeDirectiveFactsCollector.ContainsScopeDirective(functionDefinition.Body) ||
            !IsStraightLineSummaryBody(functionDefinition.Body))
        {
            summary = default;
            return false;
        }

        var capturedBindings = bindings.Clone();
        capturedBindings.Remove(functionDefinition.Name);
        summary = new AbstractFunctionSummary(functionDefinition.Parameters, functionDefinition.Body, capturedBindings, functionDefinition.Span);
        return true;
    }

    private static bool IsStraightLineSummaryBody(IReadOnlyList<StatementSyntax> statements)
        => IsStraightLineSummaryBody(statements, out var hasReturn) && hasReturn;

    private static bool IsStraightLineSummaryBody(IReadOnlyList<StatementSyntax> statements, out bool hasReturn)
    {
        hasReturn = false;
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax:
                case ChainedAssignmentStatementSyntax:
                case AnnotatedAssignmentStatementSyntax:
                case ExpressionStatementSyntax:
                case PassStatementSyntax:
                    break;

                case WithStatementSyntax withStatement:
                    if (!IsStraightLineSummaryBody(withStatement.Body, out var withHasReturn))
                    {
                        return false;
                    }

                    hasReturn |= withHasReturn;
                    break;

                case IfStatementSyntax ifStatement:
                    if (!IsStraightLineSummaryBody(ifStatement.ThenStatements, out var thenHasReturn))
                    {
                        return false;
                    }

                    if (ifStatement.ElseStatements is not null)
                    {
                        if (!IsStraightLineSummaryBody(ifStatement.ElseStatements, out var elseHasReturn))
                        {
                            return false;
                        }

                        hasReturn |= thenHasReturn && elseHasReturn;
                    }
                    else
                    {
                        hasReturn |= thenHasReturn;
                    }
                    break;

                case ReturnStatementSyntax:
                    hasReturn = true;
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetParameterCallShapeFailure(
        IReadOnlyList<FunctionParameterSyntax> parameters,
        int firstParameterIndex,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        if (parameters.Any(static parameter => parameter.Kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary))
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        if (firstParameterIndex > parameters.Count)
        {
            reason = "method has no receiver parameter";
            offendingExpression = null;
            return true;
        }

        var effectiveParameters = parameters.Skip(firstParameterIndex).ToArray();
        var positionalParameters = effectiveParameters.Where(static parameter => parameter.Kind == FunctionParameterKind.Positional).ToArray();
        if (arguments.Positional.Count > positionalParameters.Length)
        {
            reason = "too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            assigned.Add(positionalParameters[i].Name);
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            var parameter = effectiveParameters.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
            if (parameter is null)
            {
                reason = $"unexpected keyword '{keyword}'";
                offendingExpression = expression;
                return true;
            }

            if (!assigned.Add(keyword))
            {
                reason = $"duplicate binding for '{keyword}'";
                offendingExpression = expression;
                return true;
            }
        }

        foreach (var parameter in effectiveParameters)
        {
            if (parameter.DefaultValue is null && !assigned.Contains(parameter.Name))
            {
                reason = $"missing required argument '{parameter.Name}'";
                offendingExpression = null;
                return true;
            }
        }

        reason = string.Empty;
        offendingExpression = null;
        return false;
    }

    private static bool TryBindFunctionArguments(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        out AbstractState functionBindings)
    {
        functionBindings = summary.CapturedBindings.Clone();
        var consumedKeywords = new HashSet<string>(StringComparer.Ordinal);
        var positionalIndex = 0;

        foreach (var parameter in summary.Parameters)
        {
            if (parameter.Kind == FunctionParameterKind.KeywordOnly)
            {
                if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
                {
                    consumedKeywords.Add(parameter.Name);
                    functionBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
                }
                else if (parameter.DefaultValue is not null)
                {
                    functionBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
                }
                else
                {
                    return false;
                }

                continue;
            }

            if (parameter.Kind != FunctionParameterKind.Positional)
            {
                return false;
            }

            if (positionalIndex < arguments.Positional.Count)
            {
                if (arguments.Keywords.ContainsKey(parameter.Name))
                {
                    return false;
                }

                functionBindings.Set(parameter.Name, arguments.ResolvePositionalValue(positionalIndex, callBindings));
                positionalIndex++;
            }
            else if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
            {
                consumedKeywords.Add(parameter.Name);
                functionBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
            }
            else if (parameter.DefaultValue is not null)
            {
                functionBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
            }
            else
            {
                return false;
            }
        }

        if (positionalIndex != arguments.Positional.Count)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords.Keys)
        {
            if (!consumedKeywords.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryBindInstanceMethodArguments(
        AbstractFunctionSummary summary,
        AbstractValue selfValue,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        out AbstractState methodBindings)
    {
        methodBindings = summary.CapturedBindings.Clone();
        if (summary.Parameters.Count == 0 || summary.Parameters[0].Kind != FunctionParameterKind.Positional)
        {
            return false;
        }

        methodBindings.Set(summary.Parameters[0].Name, selfValue.WithSpan(summary.Span));
        var consumedKeywords = new HashSet<string>(StringComparer.Ordinal);
        var positionalIndex = 0;

        for (var parameterIndex = 1; parameterIndex < summary.Parameters.Count; parameterIndex++)
        {
            var parameter = summary.Parameters[parameterIndex];
            if (parameter.Kind == FunctionParameterKind.KeywordOnly)
            {
                if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
                {
                    consumedKeywords.Add(parameter.Name);
                    methodBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
                }
                else if (parameter.DefaultValue is not null)
                {
                    methodBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
                }
                else
                {
                    return false;
                }

                continue;
            }

            if (parameter.Kind != FunctionParameterKind.Positional)
            {
                return false;
            }

            if (positionalIndex < arguments.Positional.Count)
            {
                if (arguments.Keywords.ContainsKey(parameter.Name))
                {
                    return false;
                }

                methodBindings.Set(parameter.Name, arguments.ResolvePositionalValue(positionalIndex, callBindings));
                positionalIndex++;
            }
            else if (arguments.Keywords.TryGetValue(parameter.Name, out var keywordExpression))
            {
                consumedKeywords.Add(parameter.Name);
                methodBindings.Set(parameter.Name, arguments.ResolveKeywordValue(parameter.Name, callBindings));
            }
            else if (parameter.DefaultValue is not null)
            {
                methodBindings.Set(parameter.Name, ResolveArgumentValue(parameter.DefaultValue, summary.CapturedBindings));
            }
            else
            {
                return false;
            }
        }

        if (positionalIndex != arguments.Positional.Count)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords.Keys)
        {
            if (!consumedKeywords.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }

    private static AbstractValue ResolveArgumentValue(ExpressionSyntax expression, AbstractState bindings)
        => StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);

    private static bool TryInferStraightLineReturn(
        IReadOnlyList<StatementSyntax> statements,
        AbstractState bindings,
        out AbstractValue returnValue)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax assignment:
                    UpdateBinding(assignment.Name, assignment.Expression, bindings);
                    break;

                case ChainedAssignmentStatementSyntax chained:
                    foreach (var target in chained.Targets)
                    {
                        if (target is NameAssignmentTargetSyntax nameTarget)
                        {
                            UpdateBinding(nameTarget.Name, chained.Expression, bindings);
                        }
                    }
                    break;

                case AnnotatedAssignmentStatementSyntax annotated when annotated.Expression is not null:
                    UpdateBinding(annotated.Name, annotated.Expression, bindings);
                    break;

                case ExpressionStatementSyntax expressionStatement:
                    TryApplyArgparseParserMutation(expressionStatement.Expression, bindings);
                    break;

                case PassStatementSyntax:
                    break;

                case WithStatementSyntax withStatement:
                    {
                        var withBindings = bindings.Clone();
                        if (withStatement.VariableName is not null &&
                            StaticAbstractValueResolver.TryResolve(withStatement.ContextExpression, bindings, out var contextValue) &&
                            contextValue.Kind == AbstractValueKind.TextFileHandle)
                        {
                            withBindings.Set(withStatement.VariableName, contextValue);
                        }

                        if (TryInferStraightLineReturn(withStatement.Body, withBindings, out returnValue))
                        {
                            return true;
                        }

                        bindings.ReplaceWith(withBindings);
                        if (withStatement.VariableName is not null)
                        {
                            bindings.Remove(withStatement.VariableName);
                        }

                        break;
                    }

                case IfStatementSyntax ifStatement:
                    {
                        var thenBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: true, thenBindings);
                        var thenReturned = TryInferStraightLineReturn(ifStatement.ThenStatements, thenBindings, out var thenReturnValue);
                        var elseBindings = bindings.Clone();
                        StaticConditionRefinements.Apply(ifStatement.Condition, assumedTruth: false, elseBindings);
                        AbstractValue elseReturnValue = default;
                        var elseReturned = ifStatement.ElseStatements is not null &&
                                           TryInferStraightLineReturn(ifStatement.ElseStatements, elseBindings, out elseReturnValue);

                        if (thenReturned && elseReturned)
                        {
                            returnValue = AbstractValue.Join(thenReturnValue, elseReturnValue, statement.Span);
                            return true;
                        }

                        if (thenReturned)
                        {
                            bindings.ReplaceWith(elseBindings);
                        }
                        else if (elseReturned)
                        {
                            bindings.ReplaceWith(thenBindings);
                        }
                        else
                        {
                            bindings.MergeFrom(thenBindings, elseBindings);
                        }

                        break;
                    }

                case ReturnStatementSyntax { Expression: null }:
                    returnValue = AbstractValue.None(statement.Span);
                    return true;

                case ReturnStatementSyntax { Expression: var expression }:
                    return StaticAbstractValueResolver.TryResolve(expression, bindings, out returnValue);

                default:
                    returnValue = default;
                    return false;
            }
        }

        returnValue = default;
        return false;
    }

}
