using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
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
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, LythonRegexMatchMode.Search, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReMatch.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, LythonRegexMatchMode.Match, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReFullMatch.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, LythonRegexMatchMode.FullMatch, out value);
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
                "match" => LythonRegexMatchMode.Match,
                "fullmatch" => LythonRegexMatchMode.FullMatch,
                _ => LythonRegexMatchMode.Search
            };

            value = ResolveRegexMatchLikeReturn(summary, arguments, 0, "string", 1, bindings, call.Span, operation);
            return true;
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
            (index.TryGetInt32(out var integer) && integer == 0 ||
             index.Kind == AbstractValueKind.Boolean && !index.RequireBoolean());
        value = isZero ? AbstractValue.StringType(span) : AbstractValue.Unknown(span);
        return true;
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
        LythonRegexMatchMode operation,
        out AbstractValue value)
    {
        if (!TryGetRegexPatternSummary(arguments, patternPosition, patternKeyword, flagsPosition, flagsKeyword, bindings, allowCompiledPattern: true, out var patternSummary, out var flagsKnown))
        {
            value = default;
            return false;
        }

        value = ResolveRegexMatchLikeReturn(patternSummary, arguments, textPosition, textKeyword,
            flagsPosition + 1, bindings, span, operation, flagsKnown);
        return true;
    }

    private static AbstractValue ResolveRegexMatchLikeReturn(
        AbstractRegexPatternSummary patternSummary,
        ConcreteCallArguments arguments,
        int textPosition,
        string textKeyword,
        int boundsPosition,
        AbstractState bindings,
        LythonSourceSpan span,
        LythonRegexMatchMode operation,
        bool evaluateConstant = true)
    {
        if (evaluateConstant && patternSummary.Program is { } program && patternSummary.CaptureSlotCount is { } captureSlotCount &&
            TryGetArgument(arguments, textPosition, textKeyword, bindings, out _, out var textValue) &&
            textValue.Kind == AbstractValueKind.String &&
            TryGetBounds(arguments, boundsPosition, bindings, out var pos, out var endPos))
        {
            return MatchConstant(program, captureSlotCount, textValue.RequireText(), pos, endPos, operation, bindings, span)
                ? AbstractValue.RegexMatch(CreateRegexMatchSummary(patternSummary), span)
                : AbstractValue.None(span);
        }

        return AbstractValue.MaybeRegexMatch(CreateRegexMatchSummary(patternSummary), span);
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
        if (!TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out var patternExpression, out var patternValue))
        {
            summary = default;
            return false;
        }

        if (allowCompiledPattern && patternValue.Kind == AbstractValueKind.RegexPattern)
        {
            flagsKnown &= patternFlags == 0;
            summary = patternValue.RequireRegexPatternSummary();
            return true;
        }

        if (patternValue.Kind != AbstractValueKind.String)
        {
            summary = default;
            return false;
        }

        summary = flagsKnown && TryCompileRegex(patternValue.RequireText(), patternFlags, bindings,
            DiagnosticSpan(patternExpression, patternValue), out var compiled)
            ? compiled : AbstractRegexPatternSummary.Unknown;
        return true;
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
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out _, out var flagsValue) ||
            flagsValue.Kind == AbstractValueKind.None)
        {
            options = 0;
            return true;
        }

        if (flagsValue.TryGetInt32(out var flags) && flags >= 0)
        {
            return TryTranslatePythonRegexFlags(flags, out options);
        }

        options = 0;
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

    private static bool TryCompileRegex(string pattern, int options, AbstractState bindings, LythonSourceSpan span,
        [MaybeNullWhen(false)] out AbstractRegexPatternSummary regex)
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
            var bytes = 512L + 3L * pattern.Length;
            analysis.ReserveRegexStorage(bytes, span);
            var encoded = Encoding.UTF8.GetBytes(pattern);
            var compileOptions = new LythonRegexOptions(options, (options & 64) != 0);
            var program = LythonRuntime.RegexCompiler.ProviderCall(() => provider.Compile(encoded, compileOptions,
                analysis.RegexMemoryOwner, token), governor, token, span, operationName: "Regex pattern compilation");
            try
            {
                var captureSlotCount = LythonRuntime.RegexCompiler.ReadCaptureSlotCount(program, governor, token, span);
                regex = new(program, captureSlotCount,
                    LythonRuntime.RegexCompiler.CopyNamedGroups(program, captureSlotCount, governor, token, span));
                analysis.RetainRegexProgram(encoded, compileOptions, program, captureSlotCount, span);
            }
            catch { program.Dispose(); throw; }
            // The compiled lookup now owns the program, even if analysis
            // exits before publishing the new fact.
            analysis.RegexPatterns[(pattern, options)] = regex;
            return true;
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "PatternError")
        {
            regex = default;
            return false;
        }
    }

    private static bool TryGetBounds(ConcreteCallArguments arguments, int posPosition, AbstractState bindings,
        out int pos, out int endPos)
    {
        endPos = int.MaxValue;
        return TryGetBound(posPosition, "pos", 0, out pos) &&
            TryGetBound(posPosition + 1, "endpos", int.MaxValue, out endPos);

        bool TryGetBound(int position, string keyword, int fallback, out int bound)
        {
            if (!TryGetArgument(arguments, position, keyword, bindings, out _, out var value))
            { bound = fallback; return true; }
            if (value.Kind == AbstractValueKind.Boolean)
            { bound = value.RequireBoolean() ? 1 : 0; return true; }
            return value.TryGetInt32(out bound);
        }
    }

    private static bool MatchConstant(ILythonRegexPattern pattern, int captureSlotCount, string text, int pos, int endPos,
        LythonRegexMatchMode operation, AbstractState bindings, LythonSourceSpan span)
    {
        var analysis = bindings.AnalysisContext!;
        var token = analysis.CancellationToken;
        using var inputStorage = analysis.MemoryGovernor?.ReserveTemporary(32L + 3L * text.Length, span);
        token.ThrowIfCancellationRequested();
        var input = Encoding.UTF8.GetBytes(text);
        var length = PyString.CountUtf8Units(input, false, token, span);
        var window = LythonRuntime.RegexCompiler.CreateWindow(input, length, pos, endPos, token, span);
        if (LythonRuntime.RegexCompiler.IsImpossibleWindow(operation, window.Pos, window.EndPos)) return false;
        using var subject = LythonRuntime.RegexCompiler.ProviderCall(() => pattern.Prepare(input, window,
            analysis.MemoryGovernor?.Budget ?? new LythonMemoryBudget(), token), analysis.MemoryGovernor, token, span,
            operationName: "Regex constant-subject preparation");
        using var storage = analysis.MemoryGovernor?.ReserveTemporary(checked(32L + 16L * captureSlotCount), span);
        token.ThrowIfCancellationRequested();
        var captures = new LythonRegexCapture?[captureSlotCount];
        if (!LythonRuntime.RegexCompiler.TryMatch(subject, operation, captures, out var lastIndex,
            analysis.MemoryGovernor, token, span, operationName: "Regex constant matching")) return false;

        var error = LythonRuntime.RegexCompiler.MatchResultError(input, window, operation, captures, lastIndex,
            analysis.MemoryGovernor, token, span);
        if (error is not null) throw RuntimeErrors.Capability(error, span);
        return true;
    }
}
