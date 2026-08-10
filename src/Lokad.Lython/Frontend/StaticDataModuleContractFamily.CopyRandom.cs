using System.Globalization;
using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static partial class StaticDataModuleContractFamily
{
    private static bool AnalyzeCopyKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;

        if (string.Equals(targetName, LythonKnownCallableSignatures.CopyCopy.Name, StringComparison.Ordinal))
        {
            return AnalyzeUnsupportedCopyProtocols(arguments, deep: false, diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CopyReplace.Name, StringComparison.Ordinal))
        {
            return AnalyzeCopyReplaceDataclassFields(arguments, diagnostics, bindings);
        }

        if (!string.Equals(targetName, LythonKnownCallableSignatures.CopyDeepCopy.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (TryGetArgument(arguments, 1, "memo", bindings, out var memoExpression, out var memoValue) &&
            !IsUnknown(memoValue) &&
            memoValue.Kind is not (AbstractValueKind.None or AbstractValueKind.Dict))
        {
            AddDiagnostic(diagnostics, "LA3158", "copy.deepcopy(..., memo=...) expects a dict or None.", memoExpression.Span);
            emitted = true;
        }

        emitted |= AnalyzeUnsupportedCopyProtocols(arguments, deep: true, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeUnsupportedCopyProtocols(
        ConcreteCallArguments arguments,
        bool deep,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 0, "x", bindings, out var expression, out var value) ||
            value.Kind != AbstractValueKind.UserInstance)
        {
            return false;
        }

        var instance = value.RequirePayload<AbstractInstanceSummary>();
        var hook = deep ? "__deepcopy__" : "__copy__";
        if (instance.Class.Methods.ContainsKey(hook) || instance.Class.FieldsByName.ContainsKey(hook))
        {
            return false;
        }

        foreach (var protocol in CopyProtocolFacts.UnsupportedReductionHooks)
        {
            if (instance.Class.Methods.ContainsKey(protocol) || instance.Class.FieldsByName.ContainsKey(protocol))
            {
                AddDiagnostic(diagnostics, "LA3158", $"copy protocol {protocol} is unsupported by Lython; define {hook} instead.", expression.Span);
                return true;
            }
        }

        return false;
    }

    private static bool AnalyzeCopyReplaceDataclassFields(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (arguments.Positional.Count == 0)
        {
            return false;
        }

        var value = arguments.ResolvePositionalValue(0, bindings);
        if (value.Kind != AbstractValueKind.UserInstance)
        {
            return false;
        }

        var instance = value.RequirePayload<AbstractInstanceSummary>();
        if (!instance.Class.IsDataclass)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords)
        {
            if (!instance.Class.FieldsByName.TryGetValue(keyword.Key, out var field))
            {
                AddDiagnostic(diagnostics, "LA3156", $"copy.replace() got an unexpected field '{keyword.Key}'.", keyword.Value.Span);
                return true;
            }

            if (!field.IncludeInInit)
            {
                AddDiagnostic(diagnostics, "LA3156", $"copy.replace() cannot override init=False field '{keyword.Key}'.", keyword.Value.Span);
                return true;
            }
        }

        return false;
    }

    private static bool AnalyzeRandomKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!targetName.StartsWith("random.", StringComparison.Ordinal))
        {
            return false;
        }

        var emitted = false;
        switch (targetName)
        {
            case "random.Random":
                return AnalyzeRandomSeedArgument(arguments, 0, "a", diagnostics, bindings);
            case "random.seed":
                emitted |= AnalyzeRandomSeedArgument(arguments, 0, "a", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "version", "random.seed(..., version=...) expects an integer version.", diagnostics, bindings);
                return emitted;
            case "random.setstate":
                return false;
            case "random.randrange":
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 0, "start", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "stop", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 2, "step", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                return emitted;
            case "random.randint":
                emitted |= AnalyzeIntegerArgument(arguments, 0, "a", "random.randint(a, b) expects integer bounds.", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "b", "random.randint(a, b) expects integer bounds.", diagnostics, bindings);
                return emitted;
            case "random.choice":
                return AnalyzeIterableArgument(arguments, 0, "seq", "random.choice(seq) expects an iterable sequence.", diagnostics, bindings);
            case "random.choices":
                AnalyzeRandomChoicesCall(arguments, diagnostics, bindings);
                return true;
            case "random.shuffle":
                return AnalyzeMutableSequenceArgument(arguments, 0, "x", "random.shuffle(x) expects a mutable sequence.", diagnostics, bindings);
            case "random.sample":
                emitted |= AnalyzeIterableArgument(arguments, 0, "population", "random.sample(population, k, *, counts=None) expects an iterable population.", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "k", "random.sample(..., k=...) expects an integer.", diagnostics, bindings);
                emitted |= AnalyzeIterableOrNoneArgument(arguments, 2, "counts", "random.sample(..., counts=...) expects an iterable of counts or None.", diagnostics, bindings);
                return emitted;
            case "random.getrandbits":
                return AnalyzeIntegerArgument(arguments, 0, "k", "random.getrandbits(k) expects an integer.", diagnostics, bindings);
            case "random.randbytes":
                return AnalyzeIntegerArgument(arguments, 0, "n", "random.randbytes(n) expects an integer.", diagnostics, bindings);
            case "random.uniform":
                emitted |= AnalyzeRealArgument(arguments, 0, "a", "random.uniform(a, b) expects real numbers.", diagnostics, bindings);
                emitted |= AnalyzeRealArgument(arguments, 1, "b", "random.uniform(a, b) expects real numbers.", diagnostics, bindings);
                return emitted;
            case "random.triangular":
                emitted |= AnalyzeRealOrNoneArgument(arguments, 0, "low", "random.triangular(..., low=...) expects a real number or None.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 1, "high", "random.triangular(..., high=...) expects a real number or None.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 2, "mode", "random.triangular(..., mode=...) expects a real number or None.", diagnostics, bindings);
                return emitted;
            case "random.expovariate":
            case "random.gauss":
            case "random.normalvariate":
                emitted |= AnalyzeRealOrNoneArgument(arguments, 0, targetName.EndsWith("expovariate", StringComparison.Ordinal) ? "lambd" : "mu", $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 1, "sigma", $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                return emitted;
            case "random.betavariate":
            case "random.gammavariate":
            case "random.lognormvariate":
            case "random.weibullvariate":
            case "random.vonmisesvariate":
                emitted |= AnalyzeRealArgument(arguments, 0, FirstRandomDistributionParameter(targetName), $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                emitted |= AnalyzeRealArgument(arguments, 1, SecondRandomDistributionParameter(targetName), $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                return emitted;
            case "random.paretovariate":
                return AnalyzeRealArgument(arguments, 0, "alpha", "random.paretovariate(alpha) expects a real argument.", diagnostics, bindings);
            default:
                return false;
        }
    }

    private static void AnalyzeRandomMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeRandomKnownCallArgumentTypes("random." + memberName, arguments, diagnostics, bindings);

    private static void AnalyzeRandomChoicesCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeIterableArgument(arguments, 0, "population", "random.choices(population, ...) expects an iterable population.", diagnostics, bindings);

        if (arguments.TryGetValue(1, "weights", out var weightsExpression) &&
            weightsExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(weightsExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3098", "random.choices(..., weights=...) expects an iterable of numeric weights.", weightsExpression.Span);
        }

        if (arguments.TryGetValue(2, "cum_weights", out var cumulativeExpression) &&
            cumulativeExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(cumulativeExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3099", "random.choices(..., cum_weights=...) expects an iterable of numeric weights.", cumulativeExpression.Span);
        }

        if (arguments.TryGetValue(1, "weights", out var weightsArgument) &&
            arguments.TryGetValue(2, "cum_weights", out var cumulativeArgument) &&
            weightsArgument is not NoneLiteralExpressionSyntax &&
            cumulativeArgument is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(weightsArgument, bindings) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(cumulativeArgument, bindings))
        {
            AddDiagnostic(diagnostics, "LA3100", "random.choices(...) does not accept both weights and cum_weights.", cumulativeArgument.Span);
        }

        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            3,
            "k",
            "LA3101",
            "random.choices(..., k=...) expects k to be a non-negative integer.",
            diagnostics,
            bindings);
    }

    private static bool AnalyzeRandomSeedArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, "random seed expects None, bool, int, float, str, or bytes.", diagnostics, bindings, static value =>
            value.Kind is AbstractValueKind.None or
                AbstractValueKind.Boolean or
                AbstractValueKind.BooleanType or
                AbstractValueKind.Integer or
                AbstractValueKind.IntegerType or
                AbstractValueKind.Float or
                AbstractValueKind.FloatType or
                AbstractValueKind.String or
                AbstractValueKind.StringType or
                AbstractValueKind.Bytes or
                AbstractValueKind.BytesType);

    private static bool AnalyzeIterableArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeIterableOrNoneArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeMutableSequenceArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value =>
            value.Kind is AbstractValueKind.List or AbstractValueKind.ListType || StaticKnownCallArgumentChecks.IsUnknown(value));

    private static bool AnalyzeRealArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, StaticAbstractFacts.IsNumericLike);

    private static bool AnalyzeRealOrNoneArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsNumericLike(value));

    private static string FirstRandomDistributionParameter(string targetName)
        => targetName switch
        {
            "random.betavariate" or "random.gammavariate" or "random.paretovariate" or "random.weibullvariate" => "alpha",
            "random.vonmisesvariate" or "random.lognormvariate" => "mu",
            _ => "a"
        };

    private static string SecondRandomDistributionParameter(string targetName)
        => targetName switch
        {
            "random.betavariate" or "random.gammavariate" or "random.weibullvariate" => "beta",
            "random.vonmisesvariate" => "kappa",
            "random.lognormvariate" => "sigma",
            _ => "b"
        };

    private static void AnalyzeFunctoolsUpdateWrapperCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "wrapper", out var wrapperExpression))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(wrapperExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3106", "functools.update_wrapper(wrapper, wrapped) expects a mutable callable wrapper and a wrapped object.", wrapperExpression.Span);
        }
    }

}
