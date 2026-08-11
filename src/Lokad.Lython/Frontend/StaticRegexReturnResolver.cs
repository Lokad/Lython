using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Utf8Regex.PythonRe;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticRegexReturnResolver
{
    public static bool TryResolveKnownCallReturn(
        string targetName,
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span,
        out AbstractValue value)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.ReCompile.Name, StringComparison.Ordinal) &&
            TryGetRegexPatternSummary(arguments, 0, "pattern", 1, "flags", bindings, allowCompiledPattern: false, out var patternSummary, out _))
        {
            value = AbstractValue.RegexPattern(patternSummary, span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSearch.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, RegexMatchOperation.Search, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReMatch.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, RegexMatchOperation.Match, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReFullMatch.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, RegexMatchOperation.FullMatch, out value);
        }

        value = default;
        return false;
    }

    public static bool TryResolvePatternMemberCallReturn(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is not MemberExpressionSyntax { Target: var receiverExpression, MemberName: var memberName } ||
            !StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver) ||
            receiver.Kind != AbstractValueKind.RegexPattern ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !StaticContracts.TryGetCallableContract(receiver, memberName, out var contract) ||
            !contract.AcceptsArgumentShape(arguments))
        {
            value = default;
            return false;
        }

        var summary = receiver.RequireRegexPatternSummary();
        if (memberName is "search" or "match" or "fullmatch")
        {
            var operation = memberName switch
            {
                "match" => RegexMatchOperation.Match,
                "fullmatch" => RegexMatchOperation.FullMatch,
                _ => RegexMatchOperation.Search
            };

            return TryResolveRegexMatchLikeReturn(
                summary,
                arguments,
                0,
                "string",
                bindings,
                call.Span,
                operation,
                out value);
        }

        value = memberName switch
        {
            "findall" => AbstractValue.ListOf(AbstractValue.Unknown(call.Span), call.Span),
            "sub" => AbstractValue.StringType(call.Span),
            "split" => AbstractValue.ListOf(AbstractValue.StringType(call.Span), call.Span),
            _ => default
        };

        return value.Kind != default;
    }

    public static bool TryResolveMatchMemberCallReturn(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (call.Target is not MemberExpressionSyntax { Target: var receiverExpression, MemberName: var memberName } ||
            !StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver) ||
            receiver.Kind != AbstractValueKind.RegexMatch ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !StaticContracts.TryGetCallableContract(receiver, memberName, out var contract) ||
            !contract.AcceptsArgumentShape(arguments))
        {
            value = default;
            return false;
        }

        value = memberName switch
        {
            "group" when arguments.Positional.Count == 0 && arguments.Keywords.Count == 0 => AbstractValue.StringType(call.Span),
            "group" => AbstractValue.Unknown(call.Span),
            "groups" => AbstractValue.Tuple([], call.Span),
            "groupdict" => AbstractValue.Unknown(call.Span),
            "expand" => AbstractValue.StringType(call.Span),
            "start" or "end" => AbstractValue.IntegerType(call.Span),
            "span" => AbstractValue.Tuple(
                new[] { AbstractValue.IntegerType(call.Span), AbstractValue.IntegerType(call.Span) },
                call.Span),
            _ => default
        };

        return value.Kind != default;
    }

    private enum RegexMatchOperation
    {
        Search,
        Match,
        FullMatch,
    }

    private static bool TryResolveRegexMatchLikeReturn(
        ConcreteCallArguments arguments,
        int patternPosition,
        string patternKeyword,
        int textPosition,
        string textKeyword,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings,
        LythonSourceSpan span,
        RegexMatchOperation operation,
        out AbstractValue value)
    {
        if (!TryGetRegexPatternSummary(arguments, patternPosition, patternKeyword, flagsPosition, flagsKeyword, bindings, allowCompiledPattern: true, out var patternSummary, out var flagsKnown))
        {
            value = default;
            return false;
        }

        if (flagsKnown &&
            patternSummary.PatternText is not null &&
            TryGetArgument(arguments, textPosition, textKeyword, bindings, out _, out var textValue) &&
            textValue.Kind == AbstractValueKind.String &&
            TryCompileRegex(patternSummary.PatternText, GetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings), out var regex))
        {
            var text = textValue.RequireText();
            var bytes = Encoding.UTF8.GetBytes(text);
            var match = operation switch
            {
                RegexMatchOperation.Match => regex.MatchDetailedData(bytes),
                RegexMatchOperation.FullMatch => regex.FullMatchDetailedData(bytes),
                _ => regex.SearchDetailedData(bytes)
            };

            value = match.Success
                ? AbstractValue.RegexMatch(CreateRegexMatchSummary(match.CaptureSlotCount, match.NameEntries), span)
                : AbstractValue.None(span);
            return true;
        }

        value = AbstractValue.MaybeRegexMatch(CreateRegexMatchSummary(patternSummary), span);
        return true;
    }

    private static bool TryResolveRegexMatchLikeReturn(
        AbstractRegexPatternSummary patternSummary,
        ConcreteCallArguments arguments,
        int textPosition,
        string textKeyword,
        AbstractState bindings,
        LythonSourceSpan span,
        RegexMatchOperation operation,
        out AbstractValue value)
    {
        if (patternSummary.PatternText is not null &&
            TryGetArgument(arguments, textPosition, textKeyword, bindings, out _, out var textValue) &&
            textValue.Kind == AbstractValueKind.String &&
            TryCompileRegex(patternSummary.PatternText, PythonReCompileOptions.None, out var regex))
        {
            var text = textValue.RequireText();
            var bytes = Encoding.UTF8.GetBytes(text);
            var match = operation switch
            {
                RegexMatchOperation.Match => regex.MatchDetailedData(bytes),
                RegexMatchOperation.FullMatch => regex.FullMatchDetailedData(bytes),
                _ => regex.SearchDetailedData(bytes)
            };

            value = match.Success
                ? AbstractValue.RegexMatch(CreateRegexMatchSummary(match.CaptureSlotCount, match.NameEntries), span)
                : AbstractValue.None(span);
            return true;
        }

        value = AbstractValue.MaybeRegexMatch(CreateRegexMatchSummary(patternSummary), span);
        return true;
    }

    private static bool TryGetRegexPatternSummary(
        ConcreteCallArguments arguments,
        int patternPosition,
        string patternKeyword,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings,
        bool allowCompiledPattern,
        [MaybeNullWhen(false)] out AbstractRegexPatternSummary summary,
        out bool flagsKnown)
    {
        flagsKnown = TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out _);
        if (!TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out _, out var patternValue))
        {
            summary = default;
            return false;
        }

        if (allowCompiledPattern && patternValue.Kind == AbstractValueKind.RegexPattern)
        {
            summary = patternValue.RequireRegexPatternSummary();
            return true;
        }

        if (patternValue.Kind != AbstractValueKind.String)
        {
            summary = default;
            return false;
        }

        summary = CreateRegexPatternSummary(patternValue.RequireText());
        return true;
    }

    private static AbstractRegexPatternSummary CreateRegexPatternSummary(string pattern)
    {
        var summary = RegexPatternFacts.SummarizeGroups(pattern);
        return summary.IsComplete
            ? new AbstractRegexPatternSummary(pattern, summary.CaptureSlotCount, summary.NamedGroups)
            : new AbstractRegexPatternSummary(pattern, null, new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static AbstractRegexMatchSummary CreateRegexMatchSummary(AbstractRegexPatternSummary pattern)
        => new(pattern.CaptureSlotCount, pattern.NamedGroups);

    private static AbstractRegexMatchSummary CreateRegexMatchSummary(
        int captureSlotCount,
        IEnumerable<PythonReNameEntry> nameEntries)
    {
        var namedGroups = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in nameEntries)
        {
            namedGroups[entry.Name] = entry.Number;
        }

        return new AbstractRegexMatchSummary(captureSlotCount, namedGroups);
    }

    private static bool TryGetRegexOptions(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings,
        out PythonReCompileOptions options)
    {
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out _, out var flagsValue) ||
            flagsValue.Kind == AbstractValueKind.None)
        {
            options = PythonReCompileOptions.None;
            return true;
        }

        if (TryGetInt32(flagsValue, out var flags) && flags >= 0)
        {
            return TryTranslatePythonRegexFlags(flags, out options);
        }

        options = PythonReCompileOptions.None;
        return false;
    }

    private static bool TryTranslatePythonRegexFlags(int flags, out PythonReCompileOptions options)
    {
        const int ignoreCase = 2;
        const int locale = 4;
        const int multiline = 8;
        const int dotAll = 16;
        const int unicode = 32;
        const int verbose = 64;
        const int debug = 128;
        const int ascii = 256;
        const int supported = ignoreCase | locale | multiline | dotAll | unicode | verbose | debug | ascii;

        options = PythonReCompileOptions.None;
        if ((flags & ~supported) != 0 ||
            (flags & (locale | debug)) != 0 ||
            (flags & ascii) != 0 && (flags & unicode) != 0)
        {
            return false;
        }

        if ((flags & ignoreCase) != 0) options |= PythonReCompileOptions.IgnoreCase;
        if ((flags & multiline) != 0) options |= PythonReCompileOptions.Multiline;
        if ((flags & dotAll) != 0) options |= PythonReCompileOptions.DotAll;
        if ((flags & verbose) != 0) options |= PythonReCompileOptions.Verbose;
        if ((flags & ascii) != 0) options |= PythonReCompileOptions.Ascii;
        return true;
    }

    private static PythonReCompileOptions GetRegexOptions(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings)
        => TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out var options)
            ? options
            : PythonReCompileOptions.None;

    private static bool TryCompileRegex(string pattern, PythonReCompileOptions options, [MaybeNullWhen(false)] out Utf8PythonRegex regex)
    {
        try
        {
            regex = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException || ex.GetType().Name.Contains("Regex", StringComparison.Ordinal))
        {
            regex = default;
            return false;
        }
    }

}
