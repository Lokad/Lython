namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    public static bool TryResolveFunctionCallReturn(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (!TryBindFunctionArguments(summary, arguments, callBindings, out var functionBindings) ||
            !TryInferStraightLineReturn(summary.Body, functionBindings, out var returnValue))
        {
            value = default;
            return false;
        }

        value = returnValue.WithSpan(span);
        return true;
    }

    public static bool TryInstantiateUserClass(
        AbstractClassSummary summary,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (!summary.IsDataclass)
        {
            value = default;
            return false;
        }

        var initFields = summary.InitFields;
        var positionalFields = summary.PositionalInitFields;
        if (arguments.Positional.Count > positionalFields.Length)
        {
            value = default;
            return false;
        }

        var fields = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);
        var consumedFields = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < positionalFields.Length; i++)
        {
            if (i < arguments.Positional.Count)
            {
                var field = positionalFields[i];
                if (field.StoreOnInstance)
                {
                    fields[field.Name] = arguments.ResolvePositionalValue(i, bindings).WithSpan(span);
                }

                consumedFields.Add(field.Name);
            }
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            if (!summary.FieldsByName.TryGetValue(keyword, out var field) ||
                !field.IncludeInInit ||
                !consumedFields.Add(keyword))
            {
                value = default;
                return false;
            }

            if (field.StoreOnInstance)
            {
                fields[keyword] = arguments.ResolveKeywordValue(keyword, bindings).WithSpan(span);
            }
        }

        foreach (var field in summary.Fields)
        {
            if (fields.ContainsKey(field.Name))
            {
                continue;
            }

            if (field.StoreOnInstance)
            {
                fields[field.Name] = field.HasDefault
                    ? field.DefaultValue.WithSpan(span)
                    : AbstractValue.Unknown(span);
            }
        }

        value = AbstractValue.UserInstance(new AbstractInstanceSummary(summary, fields, span), span);
        return true;
    }

    public static bool TryGetUserInstanceMemberValue(AbstractValue instanceValue, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        var instance = instanceValue.RequireInstanceSummary();
        if (instance.Fields.TryGetValue(memberName, out value))
        {
            value = value.WithSpan(span);
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryResolveUserInstanceMethodReturn(
        AbstractValue instanceValue,
        string methodName,
        ConcreteCallArguments arguments,
        AbstractState callBindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        var instance = instanceValue.RequireInstanceSummary();
        if (!instance.Class.Methods.TryGetValue(methodName, out var methodSummary) ||
            !TryBindInstanceMethodArguments(methodSummary, instanceValue, arguments, callBindings, out var methodBindings) ||
            !TryInferStraightLineReturn(methodSummary.Body, methodBindings, out value))
        {
            value = default;
            return false;
        }

        value = value.WithSpan(span);
        return true;
    }

    public static bool TryGetFunctionCallShapeFailure(
        AbstractFunctionSummary summary,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
        => TryGetParameterCallShapeFailure(
            summary.Parameters,
            firstParameterIndex: 0,
            arguments,
            out reason,
            out offendingExpression);

    public static bool TryGetUserInstanceMethodCallShapeFailure(
        AbstractValue instanceValue,
        string methodName,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        var instance = instanceValue.RequireInstanceSummary();
        if (!instance.Class.Methods.TryGetValue(methodName, out var methodSummary))
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        return TryGetParameterCallShapeFailure(
            methodSummary.Parameters,
            firstParameterIndex: 1,
            arguments,
            out reason,
            out offendingExpression);
    }

    public static bool TryGetDataclassConstructorShapeFailure(
        AbstractClassSummary summary,
        ConcreteCallArguments arguments,
        out string reason,
        out ExpressionSyntax? offendingExpression)
    {
        if (!summary.IsDataclass)
        {
            reason = string.Empty;
            offendingExpression = null;
            return false;
        }

        var initFields = summary.InitFields;
        var positionalFields = summary.PositionalInitFields;
        if (arguments.Positional.Count > positionalFields.Length)
        {
            reason = "too many positional arguments";
            offendingExpression = null;
            return true;
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            assigned.Add(positionalFields[i].Name);
        }

        foreach (var (keyword, expression) in arguments.Keywords)
        {
            if (!summary.FieldsByName.TryGetValue(keyword, out var field) || !field.IncludeInInit)
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

        foreach (var field in initFields)
        {
            if (!field.HasDefault && !assigned.Contains(field.Name))
            {
                reason = $"missing required argument '{field.Name}'";
                offendingExpression = null;
                return true;
            }
        }

        reason = string.Empty;
        offendingExpression = null;
        return false;
    }
}
