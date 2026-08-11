using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticRegexContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.ReCompile.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "pattern", "re.compile(pattern[, flags]) expects pattern to be a string.", diagnostics, bindings);
            if (TryGetArgument(arguments, 1, "flags", bindings, out var flagsExpression, out _) &&
                StaticRegexArgumentContracts.ContainsRegexDebugFlag(flagsExpression))
            {
                StaticDiagnosticSink.AddError(
                    diagnostics,
                    "LA3045",
                    "re.DEBUG is unsupported.",
                    flagsExpression.Span);
                emitted = true;
            }

            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "flags", "re.compile(pattern[, flags]) expects flags to be an integer or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSearch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFullMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindAll.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindIter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 2, "flags", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "pos", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 4, "endpos", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSub.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReSubn.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringOrCallableArgument(arguments, 1, "repl", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects repl to be a string or callable.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 2, "string", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "count", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects count to be an integer.", diagnostics, bindings);
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 4, "flags", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 5, "pos", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 6, "endpos", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSplit.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "maxsplit", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects maxsplit to be an integer.", diagnostics, bindings);
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 3, "flags", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 4, "pos", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 5, "endpos", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReEscape.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "string", "re.escape(string) expects a string argument.", diagnostics, bindings);
        }

        return false;
    }

    public static bool AnalyzeCallableSemanticContract(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver,
        string memberName)
    {
        var emitted = false;
        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            string.Equals(memberName, "group", StringComparison.Ordinal))
        {
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexMatchGroupContract(arguments, diagnostics, bindings, receiver);
        }

        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            memberName is "start" or "end" or "span")
        {
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexMatchSingleGroupContract(memberName, arguments, diagnostics, bindings, receiver);
        }

        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            string.Equals(memberName, "expand", StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "template", "match.expand(template) expects template to be a string.", diagnostics, bindings);
        }

        if (receiver.Kind == AbstractValueKind.RegexPattern)
        {
            emitted |= StaticRegexArgumentContracts.AnalyzeRegexPatternMemberArgumentTypes(memberName, arguments, diagnostics, bindings);
        }

        return emitted;
    }

    public static bool TryResolveKnownCallReturn(
        string targetName,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
        => StaticRegexReturnResolver.TryResolveKnownCallReturn(targetName, arguments, bindings, span, out value);

    public static bool TryResolvePatternMemberCallReturn(
        CallExpressionSyntax call,
        AbstractState bindings,
        out AbstractValue value)
        => StaticRegexReturnResolver.TryResolvePatternMemberCallReturn(call, bindings, out value);

    public static bool TryResolveMatchMemberCallReturn(
        CallExpressionSyntax call,
        AbstractState bindings,
        out AbstractValue value)
        => StaticRegexReturnResolver.TryResolveMatchMemberCallReturn(call, bindings, out value);
}
