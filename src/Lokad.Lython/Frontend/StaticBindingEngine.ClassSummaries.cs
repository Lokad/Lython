using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    private static bool TryBuildSimpleClassSummary(
        ClassDefinitionStatementSyntax classDefinition,
        AbstractState bindings,
        [MaybeNullWhen(false)] out AbstractClassSummary summary)
    {
        if (classDefinition.DataclassDecorator is null ||
            classDefinition.Bases.Count != 0 ||
            classDefinition.KeywordArguments.Count != 0)
        {
            summary = default;
            return false;
        }

        var fields = new List<AbstractClassFieldSummary>();
        var methods = new Dictionary<string, AbstractFunctionSummary>(StringComparer.Ordinal);
        foreach (var statement in classDefinition.Body)
        {
            switch (statement)
            {
                case PassStatementSyntax:
                    break;

                case FunctionDefinitionStatementSyntax methodDefinition:
                    if (TryBuildSimpleFunctionSummary(methodDefinition, bindings, out var methodSummary))
                    {
                        methods[methodDefinition.Name] = methodSummary;
                    }
                    break;

                case AnnotatedAssignmentStatementSyntax annotated:
                    if (IsClassOnlyDataclassField(annotated.Annotation))
                    {
                        break;
                    }

                    var initOnly = IsInitOnlyDataclassField(annotated.Annotation);
                    var hasDefault = TryGetDataclassFieldDefault(annotated.Expression, bindings, out var defaultValue);
                    var includeInInit = TryGetDataclassFieldInit(annotated.Expression, out var init) ? init : true;
                    var keywordOnly = classDefinition.DataclassDecorator.RequireNotNull().KwOnly ||
                        (TryGetDataclassFieldKeywordOnly(annotated.Expression, out var kwOnly) && kwOnly);
                    fields.Add(new AbstractClassFieldSummary(
                        annotated.Name,
                        hasDefault ? defaultValue : AbstractValue.Unknown(annotated.Span),
                        hasDefault,
                        includeInInit,
                        keywordOnly,
                        StoreOnInstance: !initOnly,
                        annotated.Span));
                    break;

                default:
                    summary = default;
                    return false;
            }
        }

        if (fields.Count == 0)
        {
            summary = default;
            return false;
        }

        summary = new AbstractClassSummary(classDefinition.Name, fields, methods, IsDataclass: true, classDefinition.Span);
        return true;
    }

    private static bool TryGetDataclassFieldDefault(ExpressionSyntax? expression, AbstractState bindings, out AbstractValue value)
    {
        if (expression is null)
        {
            value = default;
            return false;
        }

        if (TryGetDataclassFieldArgument(expression, 0, "default", out var defaultExpression))
        {
            value = StaticAbstractValueResolver.ResolveOrUnknown(defaultExpression, bindings);
            return true;
        }

        if (TryGetDataclassFieldArgument(expression, 1, "default_factory", out var defaultFactoryExpression))
        {
            return TryGetDataclassDefaultFactoryValue(defaultFactoryExpression, expression.Span, out value);
        }

        if (StaticDataclassFacts.IsFieldCall(expression))
        {
            value = default;
            return false;
        }

        value = StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);
        return true;
    }

    private static bool TryGetDataclassFieldInit(ExpressionSyntax? expression, out bool init)
    {
        init = true;
        if (!TryGetDataclassFieldArgument(expression, 2, "init", out var initExpression))
        {
            return false;
        }

        return TryGetBooleanLiteral(initExpression, out init);
    }

    private static bool TryGetDataclassFieldKeywordOnly(ExpressionSyntax? expression, out bool keywordOnly)
    {
        keywordOnly = false;
        if (!TryGetDataclassFieldArgument(expression, 7, "kw_only", out var keywordOnlyExpression))
        {
            return false;
        }

        return TryGetBooleanLiteral(keywordOnlyExpression, out keywordOnly);
    }

    private static bool TryGetDataclassFieldArgument(ExpressionSyntax? expression, int position, string keyword, [MaybeNullWhen(false)] out ExpressionSyntax argument)
    {
        if (expression is CallExpressionSyntax call &&
            StaticDataclassFacts.IsFieldCall(call) &&
            StaticCallArguments.TryGetConcreteArguments(call, out var arguments) &&
            arguments.TryGetValue(position, keyword, out argument))
        {
            return true;
        }

        argument = default;
        return false;
    }

    private static bool TryGetDataclassDefaultFactoryValue(ExpressionSyntax expression, LythonSourceSpan span, out AbstractValue value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        switch (expression)
        {
            case IdentifierExpressionSyntax { Name: "list" }:
                value = AbstractValue.List([], span);
                return true;
            case IdentifierExpressionSyntax { Name: "tuple" }:
                value = AbstractValue.Tuple([], span);
                return true;
            case IdentifierExpressionSyntax { Name: "dict" }:
                value = AbstractValue.Dict(Array.Empty<KeyValuePair<AbstractValue, AbstractValue>>(), span);
                return true;
            case IdentifierExpressionSyntax { Name: "set" }:
                value = AbstractValue.Set([], span);
                return true;
            case IdentifierExpressionSyntax { Name: "str" }:
                value = AbstractValue.StringType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "bytes" }:
                value = AbstractValue.BytesType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "int" }:
                value = AbstractValue.IntegerType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "float" }:
                value = AbstractValue.FloatType(span);
                return true;
            case IdentifierExpressionSyntax { Name: "bool" }:
                value = AbstractValue.BooleanType(span);
                return true;
            default:
                value = AbstractValue.Unknown(span);
                return true;
        }
    }

    private static bool TryGetBooleanLiteral(ExpressionSyntax expression, out bool value)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (expression is BooleanLiteralExpressionSyntax boolean)
        {
            value = boolean.Value;
            return true;
        }

        value = false;
        return false;
    }

    private static bool IsClassOnlyDataclassField(ExpressionSyntax annotation)
    {
        while (annotation is ParenthesizedExpressionSyntax parenthesized)
        {
            annotation = parenthesized.Inner;
        }

        if (annotation is SubscriptExpressionSyntax subscript)
        {
            annotation = subscript.Target;
        }

        return annotation switch
        {
            IdentifierExpressionSyntax { Name: "ClassVar" or "KW_ONLY" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "typing" }, MemberName: "ClassVar" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "KW_ONLY" } => true,
            _ => false
        };
    }

    private static bool IsInitOnlyDataclassField(ExpressionSyntax annotation)
    {
        while (annotation is ParenthesizedExpressionSyntax parenthesized)
        {
            annotation = parenthesized.Inner;
        }

        if (annotation is SubscriptExpressionSyntax subscript)
        {
            annotation = subscript.Target;
        }

        return annotation switch
        {
            IdentifierExpressionSyntax { Name: "InitVar" } => true,
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "InitVar" } => true,
            _ => false
        };
    }

}
