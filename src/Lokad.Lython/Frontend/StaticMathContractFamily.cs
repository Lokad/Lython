using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticMathContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (IsSingleRealCall(targetName))
        {
            return AnalyzeMathRealArgument(arguments, 0, "x", $"{targetName}(x) expects a real number.", diagnostics, bindings);
        }

        if (IsBinaryRealCall(targetName))
        {
            var isAtan2 = string.Equals(targetName, LythonKnownCallableSignatures.MathAtan2.Name, StringComparison.Ordinal);
            emitted |= AnalyzeMathRealArgument(arguments, 0, isAtan2 ? "y" : "x", $"{targetName}(...) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 1, isAtan2 ? "x" : "y", $"{targetName}(...) expects real numbers.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathLog.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathRealArgument(arguments, 0, "x", "math.log(x[, base]) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealOrNoneArgument(arguments, 1, "base", "math.log(x[, base]) expects base to be a real number or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathIsClose.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathRealArgument(arguments, 0, "a", "math.isclose(a, b[, rel_tol][, abs_tol]) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 1, "b", "math.isclose(a, b[, rel_tol][, abs_tol]) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealOrNoneArgument(arguments, 2, "rel_tol", "math.isclose(..., rel_tol=...) expects a real number or None.", diagnostics, bindings);
            emitted |= AnalyzeMathRealOrNoneArgument(arguments, 3, "abs_tol", "math.isclose(..., abs_tol=...) expects a real number or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathProd.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "iterable", "math.prod(iterable, *, start=1) expects an iterable.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 1, "start", "math.prod(..., start=...) expects a real number.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathFsum.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableArgument(arguments, 0, "iterable", "math.fsum(iterable) expects an iterable.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathHypot.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.MathGcd.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.MathLcm.Name, StringComparison.Ordinal))
        {
            return AnalyzeVariadicMathArguments(targetName, arguments, diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathFactorial.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.MathIsqrt.Name, StringComparison.Ordinal))
        {
            return AnalyzeMathIntegerArgument(arguments, 0, "n", $"{targetName}(n) expects an integer argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathComb.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathIntegerArgument(arguments, 0, "n", "math.comb(n, k) expects integer arguments.", diagnostics, bindings);
            emitted |= AnalyzeMathIntegerArgument(arguments, 1, "k", "math.comb(n, k) expects integer arguments.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathPerm.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathIntegerArgument(arguments, 0, "n", "math.perm(n[, k]) expects integer arguments.", diagnostics, bindings);
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "k", "math.perm(n[, k]) expects k to be an integer or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathDist.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "p", "math.dist(p, q) expects iterable points.", diagnostics, bindings);
            emitted |= AnalyzeIterableArgument(arguments, 1, "q", "math.dist(p, q) expects iterable points.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathLdexp.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathRealArgument(arguments, 0, "x", "math.ldexp(x, i) expects x to be a real number.", diagnostics, bindings);
            emitted |= AnalyzeMathIntegerArgument(arguments, 1, "i", "math.ldexp(x, i) expects i to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathNextAfter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathRealArgument(arguments, 0, "x", "math.nextafter(x, y, *, steps=None) expects real endpoints.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 1, "y", "math.nextafter(x, y, *, steps=None) expects real endpoints.", diagnostics, bindings);
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 2, "steps", "math.nextafter(..., steps=...) expects an integer or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathFma.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeMathRealArgument(arguments, 0, "x", "math.fma(x, y, z) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 1, "y", "math.fma(x, y, z) expects real numbers.", diagnostics, bindings);
            emitted |= AnalyzeMathRealArgument(arguments, 2, "z", "math.fma(x, y, z) expects real numbers.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.MathSumProd.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeIterableArgument(arguments, 0, "p", "math.sumprod(p, q) expects iterable inputs.", diagnostics, bindings);
            emitted |= AnalyzeIterableArgument(arguments, 1, "q", "math.sumprod(p, q) expects iterable inputs.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool AnalyzeVariadicMathArguments(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        var integerOnly = string.Equals(targetName, LythonKnownCallableSignatures.MathGcd.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.MathLcm.Name, StringComparison.Ordinal);
        var message = integerOnly
            ? $"{targetName}(*integers) expects integer arguments."
            : $"{targetName}(*coordinates) expects real-number arguments.";

        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            emitted |= AnalyzeArgument(
                arguments,
                i,
                string.Empty,
                message,
                diagnostics,
                bindings,
                integerOnly ? StaticAbstractFacts.IsIntegerLike : IsMathRealLike);
        }

        return emitted;
    }

    private static bool AnalyzeMathRealArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, IsMathRealLike);

    private static bool AnalyzeMathIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, StaticAbstractFacts.IsIntegerLike);

    private static bool AnalyzeMathRealOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || IsMathRealLike(value));

    private static bool AnalyzeIntegerOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsIntegerLike(value));

    private static bool AnalyzeIterableArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value) ||
            IsUnknown(value) ||
            !StaticAbstractFacts.IsDefinitelyNonIterable(value))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(diagnostics, "LA3158", message, expression.Span);
        return true;
    }

    private static bool IsSingleRealCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.MathSqrt.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathExp.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathLog10.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathLog2.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathSin.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathCos.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathTan.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathAsin.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathAcos.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathAtan.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathSinh.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathCosh.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathTanh.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathFloor.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathCeil.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathFabs.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathTrunc.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathDegrees.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathRadians.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathIsFinite.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathIsInf.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathIsNaN.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathFrexp.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathModf.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathUlp.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathExp2.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathExpm1.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathLog1p.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathCbrt.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathErf.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathErfc.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathGamma.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathLgamma.Name, StringComparison.Ordinal);

    private static bool IsBinaryRealCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.MathAtan2.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathPow.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathFmod.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathCopySign.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.MathRemainder.Name, StringComparison.Ordinal);

    private static bool IsMathRealLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType;

}
