using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Utf8Regex.PythonRe;
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
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "flags", "re.compile(pattern[, flags]) expects flags to be an integer or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSearch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFullMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindAll.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindIter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, string[, flags]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", $"{targetName}(pattern, string[, flags]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 2, "flags", $"{targetName}(pattern, string[, flags]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSub.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReSubn.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, repl, string[, count][, flags]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringOrCallableArgument(arguments, 1, "repl", $"{targetName}(pattern, repl, string[, count][, flags]) expects repl to be a string or callable.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 2, "string", $"{targetName}(pattern, repl, string[, count][, flags]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "count", $"{targetName}(pattern, repl, string[, count][, flags]) expects count to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 4, "flags", $"{targetName}(pattern, repl, string[, count][, flags]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSplit.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", "re.split(pattern, string[, maxsplit][, flags]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", "re.split(pattern, string[, maxsplit][, flags]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "maxsplit", "re.split(pattern, string[, maxsplit][, flags]) expects maxsplit to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 3, "flags", "re.split(pattern, string[, maxsplit][, flags]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReEscape.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "string", "re.escape(string) expects a string argument.", diagnostics, bindings);
        }

        return false;
    }

    public static bool AnalyzeCallableSemanticContract(
        CallExpressionSyntax call,
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
            emitted |= AnalyzeRegexMatchGroupContract(arguments, diagnostics, bindings, receiver);
        }

        if (receiver.Kind == AbstractValueKind.RegexPattern)
        {
            emitted |= AnalyzeRegexPatternMemberArgumentTypes(memberName, arguments, diagnostics, bindings);
        }

        return emitted;
    }

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

        var summary = (AbstractRegexPatternSummary)receiver.Value;
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
            "start" or "end" => AbstractValue.IntegerType(call.Span),
            "span" => new AbstractValue(
                AbstractValueKind.Tuple,
                new[] { AbstractValue.IntegerType(call.Span), AbstractValue.IntegerType(call.Span) },
                call.Span),
            _ => default
        };

        return value.Kind != default;
    }

    private static bool AnalyzeRegexFlagsArgument(
        ConcreteCallArguments arguments,
        int patternPosition,
        string patternKeyword,
        int flagsPosition,
        string flagsKeyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out var flagsExpression, out var flagsValue))
        {
            return false;
        }

        if (TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out _, out var patternValue) &&
            patternValue.Kind == AbstractValueKind.RegexPattern)
        {
            AddDiagnostic(diagnostics, "LA3158", message, flagsExpression.Span);
            return true;
        }

        return AnalyzeKnownArgumentValue(flagsExpression, flagsValue, message, diagnostics, static value => value.Kind == AbstractValueKind.None || IsRuntimeIntegerLike(value));
    }

    private static bool AnalyzeRegexPatternMemberArgumentTypes(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (memberName is "search" or "match" or "fullmatch" or "findall" or "finditer")
        {
            return AnalyzeStringArgument(arguments, 0, "string", $"pattern.{memberName}(string) expects a string argument.", diagnostics, bindings);
        }

        if (memberName is "sub" or "subn")
        {
            emitted |= AnalyzeStringOrCallableArgument(arguments, 0, "repl", $"pattern.{memberName}(repl, string[, count]) expects repl to be a string or callable.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", $"pattern.{memberName}(repl, string[, count]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "count", $"pattern.{memberName}(repl, string[, count]) expects count to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (memberName == "split")
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "string", "pattern.split(string[, maxsplit]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 1, "maxsplit", "pattern.split(string[, maxsplit]) expects maxsplit to be an integer.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool AnalyzeRegexMatchGroupContract(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver)
    {
        var summary = (AbstractRegexMatchSummary)receiver.Value;
        var emitted = false;
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            var expression = arguments.Positional[i];
            var value = arguments.ResolvePositionalValue(i, bindings);
            if (IsUnknown(value))
            {
                continue;
            }

            if (TryGetInt32(value, out var index))
            {
                if (summary.CaptureSlotCount.HasValue &&
                    (index < 0 || index >= summary.CaptureSlotCount.Value))
                {
                    AddDiagnostic(diagnostics, "LA3159", "Regex group index is out of range.", DiagnosticSpan(expression, value));
                    emitted = true;
                }

                continue;
            }

            if (value.Kind == AbstractValueKind.IntegerType)
            {
                continue;
            }

            if (value.Kind == AbstractValueKind.String)
            {
                var name = (string)value.Value;
                if (summary.CaptureSlotCount.HasValue &&
                    !summary.NamedGroups.ContainsKey(name))
                {
                    AddDiagnostic(diagnostics, "LA3159", $"Regex group '{name}' is not defined.", DiagnosticSpan(expression, value));
                    emitted = true;
                }

                continue;
            }

            if (value.Kind == AbstractValueKind.StringType)
            {
                continue;
            }

            AddDiagnostic(diagnostics, "LA3159", "match.group(index) expects an integer or group name.", DiagnosticSpan(expression, value));
            emitted = true;
        }

        return emitted;
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
            var text = (string)textValue.Value;
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
            var text = (string)textValue.Value;
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
        out AbstractRegexPatternSummary summary,
        out bool flagsKnown)
    {
        flagsKnown = TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out _);
        if (!TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out _, out var patternValue))
        {
            summary = default!;
            return false;
        }

        if (allowCompiledPattern && patternValue.Kind == AbstractValueKind.RegexPattern)
        {
            summary = (AbstractRegexPatternSummary)patternValue.Value;
            return true;
        }

        if (patternValue.Kind != AbstractValueKind.String)
        {
            summary = default!;
            return false;
        }

        summary = CreateRegexPatternSummary((string)patternValue.Value);
        return true;
    }

    private static AbstractRegexPatternSummary CreateRegexPatternSummary(string pattern)
    {
        return TrySummarizeRegexGroups(pattern, out var captureSlotCount, out var namedGroups)
            ? new AbstractRegexPatternSummary(pattern, captureSlotCount, namedGroups)
            : new AbstractRegexPatternSummary(pattern, null, new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static AbstractRegexMatchSummary CreateRegexMatchSummary(AbstractRegexPatternSummary pattern)
        => new(pattern.CaptureSlotCount, pattern.NamedGroups);

    private static AbstractRegexMatchSummary CreateRegexMatchSummary<TNameEntry>(
        int captureSlotCount,
        IEnumerable<TNameEntry> nameEntries)
    {
        var namedGroups = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in nameEntries)
        {
            dynamic dynamicEntry = entry!;
            namedGroups[(string)dynamicEntry.Name] = (int)dynamicEntry.Number;
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
            options = (PythonReCompileOptions)flags;
            return true;
        }

        options = PythonReCompileOptions.None;
        return false;
    }

    private static PythonReCompileOptions GetRegexOptions(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings)
        => TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out var options)
            ? options
            : PythonReCompileOptions.None;

    private static bool TryCompileRegex(string pattern, PythonReCompileOptions options, out Utf8PythonRegex regex)
    {
        try
        {
            regex = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException || ex.GetType().Name.Contains("Regex", StringComparison.Ordinal))
        {
            regex = default!;
            return false;
        }
    }

    private static bool TrySummarizeRegexGroups(
        string pattern,
        out int captureSlotCount,
        out IReadOnlyDictionary<string, int> namedGroups)
    {
        var count = 1;
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var inClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '\\')
            {
                i++;
                continue;
            }

            if (ch == '[')
            {
                inClass = true;
                continue;
            }

            if (ch == ']' && inClass)
            {
                inClass = false;
                continue;
            }

            if (inClass || ch != '(')
            {
                continue;
            }

            if (i + 1 >= pattern.Length || pattern[i + 1] != '?')
            {
                count++;
                continue;
            }

            if (i + 3 < pattern.Length && pattern[i + 2] == 'P' && pattern[i + 3] == '<')
            {
                var end = pattern.IndexOf('>', i + 4);
                if (end < 0)
                {
                    captureSlotCount = 0;
                    namedGroups = names;
                    return false;
                }

                var name = pattern[(i + 4)..end];
                names[name] = count;
                count++;
                i = end;
                continue;
            }

            if (i + 2 < pattern.Length && pattern[i + 2] is ':' or '=' or '!')
            {
                continue;
            }

            if (i + 3 < pattern.Length && pattern[i + 2] == '<' && pattern[i + 3] is '=' or '!')
            {
                continue;
            }

            if (TrySkipInlineFlags(pattern, i + 2, out var endIndex))
            {
                i = endIndex;
                continue;
            }

            captureSlotCount = 0;
            namedGroups = names;
            return false;
        }

        captureSlotCount = count;
        namedGroups = names;
        return true;
    }

    private static bool TrySkipInlineFlags(string pattern, int start, out int endIndex)
    {
        var i = start;
        while (i < pattern.Length && (char.IsLetter(pattern[i]) || pattern[i] == '-'))
        {
            i++;
        }

        if (i == start || i >= pattern.Length)
        {
            endIndex = start;
            return false;
        }

        if (pattern[i] is ')' or ':')
        {
            endIndex = i;
            return true;
        }

        endIndex = start;
        return false;
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
