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
        if (IsDataclassesFieldCall(call))
        {
            AnalyzeDataclassesFieldCall(arguments, diagnostics, bindings);
            return true;
        }

        if (IsDataclassesAsDictCall(call))
        {
            AnalyzeDataclassesAsDictCall(arguments, diagnostics, bindings);
            return true;
        }

        if (IsDataclassesAsTupleCall(call))
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
        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesIsDataclass.Name, StringComparison.Ordinal))
        {
            value = TryResolveDataclassTarget(arguments, bindings, out var target)
                ? AbstractValue.Boolean(IsDataclassAbstractValue(target), span)
                : AbstractValue.BooleanType(span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesFields.Name, StringComparison.Ordinal) &&
            TryResolveDataclassClass(arguments, bindings, out var classSummary))
        {
            value = new AbstractValue(
                AbstractValueKind.Tuple,
                GetVisibleDataclassFields(classSummary)
                    .Select(field => AbstractValue.DataclassField(field.Name, span))
                    .ToArray(),
                span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesAsDict.Name, StringComparison.Ordinal) &&
            TryResolveDataclassInstance(arguments, bindings, out var instance))
        {
            var pairs = new List<KeyValuePair<AbstractValue, AbstractValue>>();
            foreach (var field in GetVisibleDataclassFields(instance.Class))
            {
                if (instance.Fields.TryGetValue(field.Name, out var fieldValue))
                {
                    pairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                        AbstractValue.String(field.Name, span),
                        fieldValue.WithSpan(span)));
                }
            }

            value = AbstractValue.Dict(pairs, span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesAsTuple.Name, StringComparison.Ordinal) &&
            TryResolveDataclassInstance(arguments, bindings, out instance))
        {
            value = new AbstractValue(
                AbstractValueKind.Tuple,
                GetVisibleDataclassFields(instance.Class)
                    .Select(field => instance.Fields.TryGetValue(field.Name, out var fieldValue) ? fieldValue.WithSpan(span) : AbstractValue.Unknown(span))
                    .ToArray(),
                span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DataclassesReplace.Name, StringComparison.Ordinal) &&
            TryResolveDataclassInstance(arguments, bindings, out instance))
        {
            var fields = new Dictionary<string, AbstractValue>(instance.Fields, StringComparer.Ordinal);
            foreach (var (keyword, expression) in arguments.Keywords)
            {
                if (string.Equals(keyword, "obj", StringComparison.Ordinal))
                {
                    continue;
                }

                var field = instance.Class.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
                if (field.Name is not null && field.StoreOnInstance && field.IncludeInInit)
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

        var instance = (AbstractInstanceSummary)objValue.Value;
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

            var field = instance.Class.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, keyword, StringComparison.Ordinal));
            if (field.Name is null)
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

    private static bool TryResolveDataclassClass(ConcreteCallArguments arguments, AbstractState bindings, [MaybeNullWhen(false)] out AbstractClassSummary summary)
    {
        if (TryResolveDataclassTarget(arguments, bindings, out var target))
        {
            if (target.Kind == AbstractValueKind.UserClass)
            {
                summary = (AbstractClassSummary)target.Value;
                return summary.IsDataclass;
            }

            if (target.Kind == AbstractValueKind.UserInstance)
            {
                summary = ((AbstractInstanceSummary)target.Value).Class;
                return summary.IsDataclass;
            }
        }

        summary = default;
        return false;
    }

    private static bool TryResolveDataclassInstance(ConcreteCallArguments arguments, AbstractState bindings, [MaybeNullWhen(false)] out AbstractInstanceSummary instance)
    {
        if (TryResolveDataclassTarget(arguments, bindings, out var target) &&
            target.Kind == AbstractValueKind.UserInstance)
        {
            instance = (AbstractInstanceSummary)target.Value;
            return instance.Class.IsDataclass;
        }

        instance = default;
        return false;
    }

    private static bool IsDataclassAbstractValue(AbstractValue value)
        => value.Kind switch
        {
            AbstractValueKind.UserClass => ((AbstractClassSummary)value.Value).IsDataclass,
            AbstractValueKind.UserInstance => ((AbstractInstanceSummary)value.Value).Class.IsDataclass,
            _ => false
        };

    private static IEnumerable<AbstractClassFieldSummary> GetVisibleDataclassFields(AbstractClassSummary summary)
        => summary.Fields.Where(static field => field.StoreOnInstance);

    private static bool IsDataclassesFieldCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "field" } or
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "field" };

    private static bool IsDataclassesAsDictCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "asdict" } or
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "asdict" };

    private static bool IsDataclassesAsTupleCall(CallExpressionSyntax call)
        => call.Target is IdentifierExpressionSyntax { Name: "astuple" } or
            MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "dataclasses" }, MemberName: "astuple" };

    private static void AnalyzeDataclassesFieldCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "default", out _) &&
            arguments.TryGetValue(1, "default_factory", out var defaultFactoryExpression))
        {
            AddDiagnostic(diagnostics, "LA3037", "dataclasses.field() cannot specify both default and default_factory.", defaultFactoryExpression.Span);
        }

        if (arguments.TryGetValue(1, "default_factory", out defaultFactoryExpression) &&
            defaultFactoryExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractValueResolver.IsDefinitelyKnownNonCallableLiteral(defaultFactoryExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3038", "dataclasses.field(default_factory=...) expects a callable.", defaultFactoryExpression.Span);
        }
    }

    private static void AnalyzeDataclassesAsDictCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 1, "dict_factory", "LA3039", "asdict(..., dict_factory=...) expects a callable or None.", diagnostics, bindings);
    }

    private static void AnalyzeDataclassesAsTupleCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeCallableOrNoneArgument(arguments, 1, "tuple_factory", "LA3042", "astuple(..., tuple_factory=...) expects a callable or None.", diagnostics, bindings);
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
