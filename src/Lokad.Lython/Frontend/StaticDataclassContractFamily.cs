using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static class StaticDataclassContractFamily
{
    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!StaticContractEngine.TryResolveKnownCallableTarget(call.Target, bindings, out var targetName))
        {
            return false;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesAsDict.Name, StringComparison.Ordinal))
        {
            AnalyzeDataclassesAsDictCall(arguments, diagnostics, bindings);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesAsTuple.Name, StringComparison.Ordinal))
        {
            AnalyzeDataclassesAsTupleCall(arguments, diagnostics, bindings);
            return true;
        }

        return false;
    }

    public static bool AnalyzeKnownCallSemanticContract(
        string targetName,
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesReplace.Name, StringComparison.Ordinal))
        {
            return AnalyzeDataclassesReplaceContract(call, arguments, diagnostics, bindings);
        }

        return false;
    }

    public static bool TryResolveKnownCallReturn(
        string targetName,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (targetName == LythonKnownCallableSignatures.DataclassesIsDataclass.Name)
        { value = AbstractValue.BooleanType(span); return true; }
        if (targetName == LythonKnownCallableSignatures.DataclassesFields.Name ||
            targetName == LythonKnownCallableSignatures.DataclassesAsDict.Name ||
            targetName == LythonKnownCallableSignatures.DataclassesAsTuple.Name)
        { value = AbstractValue.Unknown(span); return true; }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesReplace.Name, StringComparison.Ordinal) &&
            TryResolveDataclassInstance(arguments, bindings, out var instance))
        {
            var fields = new Dictionary<string, AbstractValue>(instance.Fields, StringComparer.Ordinal);
            foreach (var (keyword, expression) in arguments.Keywords)
            {
                if (string.Equals(keyword, "obj", StringComparison.Ordinal))
                {
                    continue;
                }

                if (instance.Class.FieldsByName.TryGetValue(keyword, out var field) &&
                    field.StoreOnInstance &&
                    field.IncludeInInit)
                {
                    fields[keyword] = arguments.ResolveKeywordValue(keyword, bindings).WithSpan(span);
                }
            }

            value = AbstractValue.UserInstance(new AbstractInstanceSummary(instance.Class, fields, span), span);
            return true;
        }

        value = default;
        return false;
    }

    private static bool AnalyzeDataclassesReplaceContract(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "obj", out var objExpression) ||
            !StaticAbstractValueResolver.TryResolve(objExpression, bindings, out var objValue) ||
            objValue.Kind != AbstractValueKind.UserInstance)
        {
            return false;
        }

        var instance = objValue.RequireInstanceSummary();
        if (!instance.Class.IsDataclass)
        {
            return false;
        }

        var emitted = false;
        foreach (var (keyword, expression) in arguments.Keywords)
        {
            if (string.Equals(keyword, "obj", StringComparison.Ordinal))
            {
                continue;
            }

            if (!instance.Class.FieldsByName.TryGetValue(keyword, out var field))
            {
                AddDiagnostic(diagnostics, "LA3156", $"dataclasses.replace() got an unexpected field '{keyword}'.", expression.Span);
                emitted = true;
                continue;
            }

            if (!field.IncludeInInit)
            {
                // dataclasses.replace deliberately reports init=False overrides as a
                // catchable runtime ValueError.
                continue;
            }
        }

        return emitted;
    }

    private static bool TryResolveDataclassTarget(ConcreteCallArguments arguments, AbstractState bindings, out AbstractValue target)
    {
        if (arguments.TryGetValue(0, "value", out var expression) ||
            arguments.TryGetValue(0, "class_or_instance", out expression) ||
            arguments.TryGetValue(0, "obj", out expression))
        {
            return StaticAbstractValueResolver.TryResolve(expression, bindings, out target);
        }

        target = default;
        return false;
    }

    private static bool TryResolveDataclassInstance(ConcreteCallArguments arguments, AbstractState bindings, [MaybeNullWhen(false)] out AbstractInstanceSummary instance)
    {
        if (TryResolveDataclassTarget(arguments, bindings, out var target) &&
            target.Kind == AbstractValueKind.UserInstance)
        {
            instance = target.RequireInstanceSummary();
            return instance.Class.IsDataclass;
        }

        instance = default;
        return false;
    }

    private static void AnalyzeDataclassesAsDictCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 1, "dict_factory", "LA3039", "asdict(..., dict_factory=...) expects a callable or None.", diagnostics, bindings);
    }

    private static void AnalyzeDataclassesAsTupleCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 1, "tuple_factory", "LA3042", "astuple(..., tuple_factory=...) expects a callable or None.", diagnostics, bindings);
    }

}
