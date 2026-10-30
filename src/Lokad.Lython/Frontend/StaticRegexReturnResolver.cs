using System.Text;
using Lokad.Lython.Runtime;
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
            "__getitem__" => AbstractValue.Unknown(call.Span),
            // Group arity is pattern-dependent: an empty-tuple shape wrongly rejects
            // literal indexes into stored groups, mirroring groupdict.
            "groups" => AbstractValue.Unknown(call.Span),
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

    public static bool TryResolveMatchSubscriptReturn(AbstractValue target, ExpressionSyntax key, AbstractState bindings, LythonSourceSpan span, out AbstractValue value)
    {
        if (target.Kind is not (AbstractValueKind.RegexMatch or AbstractValueKind.MaybeRegexMatch))
        {
            value = default;
            return false;
        }

        // Group zero is always text on a successful match; other captures can
        // be absent. Retain the conservative group() fact for those keys.
        var isZero = StaticAbstractValueResolver.TryResolve(key, bindings, out var index) &&
            (TryGetInt32(index, out var integer) && integer == 0 ||
             index.Kind == AbstractValueKind.Boolean && !index.RequireBoolean());
        value = isZero ? AbstractValue.StringType(span) : AbstractValue.Unknown(span);
        return true;
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
            TryCompileRegex(patternSummary.PatternText, patternSummary.Flags ?? GetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings), bindings, out var regex))
        {
            var text = textValue.RequireText();
            using var inputStorage = bindings.AnalysisContext?.MemoryGovernor?.ReserveTemporary(32L + 3L * text.Length, span);
            var bytes = Encoding.UTF8.GetBytes(text);
            var matched = MatchConstant(regex, bytes, operation, bindings, span);

            value = matched
                ? AbstractValue.RegexMatch(CreateRegexMatchSummary(patternSummary), span)
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
        if (patternSummary.Flags is { } effectiveFlags && patternSummary.PatternText is not null &&
            TryGetArgument(arguments, textPosition, textKeyword, bindings, out _, out var textValue) &&
            textValue.Kind == AbstractValueKind.String &&
            TryCompileRegex(patternSummary.PatternText, effectiveFlags, bindings, out var regex))
        {
            var text = textValue.RequireText();
            using var inputStorage = bindings.AnalysisContext?.MemoryGovernor?.ReserveTemporary(32L + 3L * text.Length, span);
            var bytes = Encoding.UTF8.GetBytes(text);
            var matched = MatchConstant(regex, bytes, operation, bindings, span);

            value = matched
                ? AbstractValue.RegexMatch(CreateRegexMatchSummary(patternSummary), span)
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
        flagsKnown = TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out var patternFlags);
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

        summary = CreateRegexPatternSummary(patternValue.RequireText(), flagsKnown ? patternFlags : null, bindings);
        return true;
    }

    private static AbstractRegexPatternSummary CreateRegexPatternSummary(string pattern, int? flags, AbstractState bindings)
    {
        if (flags is { } known && TryCompileRegex(pattern, known, bindings, out var compiled))
            return new AbstractRegexPatternSummary(pattern, compiled.CaptureSlotCount, compiled.NamedGroups, compiled.Flags);
        return new AbstractRegexPatternSummary(pattern, null, new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static AbstractRegexMatchSummary CreateRegexMatchSummary(AbstractRegexPatternSummary pattern)
        => new(pattern.CaptureSlotCount, pattern.NamedGroups);

    private static bool TryGetRegexOptions(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings,
        out int options)
    {
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out var expression, out var flagsValue) ||
            flagsValue.Kind == AbstractValueKind.None)
        {
            options = 0;
            return true;
        }

        if (TryResolveFlagBits(expression, flagsValue, bindings, out var flags) && flags >= 0)
        {
            return TryTranslatePythonRegexFlags(flags, out options);
        }

        options = 0;
        return false;
    }

    private static bool TryResolveFlagBits(ExpressionSyntax expression, AbstractValue value, AbstractState bindings, out int flags)
    {
        if (TryGetInt32(value, out flags)) return true;
        if (expression is ParenthesizedExpressionSyntax parenthesized)
            return TryResolveFlagBits(parenthesized.Inner, StaticAbstractValueResolver.ResolveOrUnknown(parenthesized.Inner, bindings), bindings, out flags);
        if (expression is BinaryExpressionSyntax { Operator: BinaryOperatorSyntax.BitwiseOr or BinaryOperatorSyntax.BitwiseAnd or BinaryOperatorSyntax.BitwiseXor } binary &&
            TryResolveFlagBits(binary.Left, StaticAbstractValueResolver.ResolveOrUnknown(binary.Left, bindings), bindings, out var left) &&
            TryResolveFlagBits(binary.Right, StaticAbstractValueResolver.ResolveOrUnknown(binary.Right, bindings), bindings, out var right))
        {
            flags = binary.Operator switch
            {
                BinaryOperatorSyntax.BitwiseOr => left | right,
                BinaryOperatorSyntax.BitwiseAnd => left & right,
                _ => left ^ right,
            };
            return true;
        }
        flags = 0;
        return false;
    }

    private static bool TryTranslatePythonRegexFlags(int flags, out int options)
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

        options = 0;
        if ((flags & ~supported) != 0 ||
            (flags & (locale | debug)) != 0 ||
            (flags & ascii) != 0 && (flags & unicode) != 0)
        {
            return false;
        }

        options = flags;
        return true;
    }

    private static int GetRegexOptions(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings)
        => TryGetRegexOptions(arguments, flagsPosition, flagsKeyword, bindings, out var options)
            ? options
            : 0;

    private static bool TryCompileRegex(string pattern, int options, AbstractState bindings,
        [MaybeNullWhen(false)] out ILythonRegexPattern regex)
    {
        var analysis = bindings.AnalysisContext;
        if ((options & 256) == 0) options |= 32;
        if (analysis?.RegexPatterns.TryGetValue((pattern, options), out regex) == true) return true;
        var provider = analysis?.RegexProvider
            ?? throw RuntimeErrors.Capability("Python regex analysis requires a host-supplied Lython regex provider.", null);
        var governor = analysis.MemoryGovernor;
        var token = analysis.CancellationToken;
        try
        {
            // Encoded input plus cache/owned-handle capacity and growth overlap.
            var bytes = 256L + 3L * pattern.Length;
            analysis.ReserveRegexStorage(bytes);
            var encoded = Encoding.UTF8.GetBytes(pattern);
            regex = LythonRuntime.RegexCompiler.ProviderCall(() => provider.Compile(encoded, new LythonRegexOptions(options, (options & 64) != 0),
                analysis.RegexMemoryOwner, token), governor, token, null);
            try
            {
                analysis.OwnRegexPattern(regex);
                // Inputs remain funded until every analysis pattern handle ends.
                analysis.RegexPatterns[(pattern, options)] = regex;
                analysis.RegexPatterns[(pattern, regex.Flags)] = regex;
            }
            catch { regex.Dispose(); throw; }
            return true;
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "PatternError")
        {
            regex = default;
            return false;
        }
    }

    private static bool MatchConstant(ILythonRegexPattern pattern, ReadOnlyMemory<byte> input,
        RegexMatchOperation operation, AbstractState bindings, LythonSourceSpan span)
    {
        var analysis = bindings.AnalysisContext!;
        var token = analysis.CancellationToken;
        using var subject = LythonRuntime.RegexCompiler.ProviderCall(() => pattern.Prepare(input,
            analysis.MemoryGovernor?.Budget ?? new LythonMemoryBudget(), token), analysis.MemoryGovernor, token, span);
        return LythonRuntime.RegexCompiler.ProviderCall(() => subject.Match((LythonRegexMatchMode)operation, token),
            analysis.MemoryGovernor, token, span) is not null;
    }
}
