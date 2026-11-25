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
        if (string.Equals(targetName, LythonKnownCallableSignatures.Regex.Compile.Name, StringComparison.Ordinal) &&
            TryGetRegexPatternSummary(arguments, 0, "pattern", 1, "flags", bindings, allowCompiledPattern: false, out var patternSummary, out _))
        {
            value = AbstractValue.RegexPattern(patternSummary, span);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Regex.Search.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, LythonRegexMatchMode.Search, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Regex.Match.Name, StringComparison.Ordinal))
        {
            return TryResolveRegexMatchLikeReturn(arguments, 0, "pattern", 1, "string", 2, "flags", bindings, span, LythonRegexMatchMode.Match, out value);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.Regex.FullMatch.Name, StringComparison.Ordinal))
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
        flagsKnown = false;
        if (!TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out var patternExpression, out var patternValue))
        {
            summary = default;
            return false;
        }

        var compiledPattern = allowCompiledPattern && patternValue.Kind == AbstractValueKind.RegexPattern;
        flagsKnown = TryGetRegexFlags(arguments, flagsPosition, flagsKeyword, bindings, compiledPattern, out var patternFlags);
        if (compiledPattern)
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

        var options = default(LythonRegexOptions);
        flagsKnown = flagsKnown && LythonRuntime.RegexCompiler.TryNormalizeExactFlags(patternFlags, out options);
        summary = flagsKnown && TryCompileRegex(patternValue.RequireText(), options, bindings,
            DiagnosticSpan(patternExpression, patternValue), out var compiled)
            ? compiled : AbstractRegexPatternSummary.Unknown;
        return true;
    }

    private static AbstractRegexMatchSummary CreateRegexMatchSummary(AbstractRegexPatternSummary pattern)
        => new(pattern.CaptureSlotCount, pattern.NamedGroups);

    private static bool TryGetRegexFlags(
        ConcreteCallArguments arguments,
        int flagsPosition,
        string flagsKeyword,
        AbstractState bindings,
        bool allowNone,
        out int flags)
    {
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out _, out var flagsValue))
        {
            flags = 0;
            return true;
        }

        if (flagsValue.Kind == AbstractValueKind.None)
        {
            flags = 0;
            return allowNone;
        }

        if (flagsValue.Kind == AbstractValueKind.Boolean)
        {
            flags = flagsValue.RequireBoolean() ? 1 : 0;
            return true;
        }
        return flagsValue.TryGetInt32(out flags);
    }

    private static bool TryCompileRegex(string pattern, LythonRegexOptions options, AbstractState bindings, LythonSourceSpan span,
        [MaybeNullWhen(false)] out AbstractRegexPatternSummary regex)
    {
        var analysis = bindings.AnalysisContext;
        if (analysis?.RegexPatterns.TryGetValue((pattern, options), out regex) == true) return true;
        var provider = analysis?.RegexProvider
            ?? throw RuntimeErrors.Capability("Python regex analysis requires a host-supplied Lython regex provider.", null);
        var governor = analysis.MemoryGovernor;
        var token = analysis.CancellationToken;
        ILythonMemoryReservation? storage = null;
        try
        {
            // Encoded input plus cache/owned-handle capacity and growth overlap.
            var bytes = 512L + 3L * pattern.Length;
            storage = analysis.ReserveRegexStorage(bytes, span);
            var encoded = EncodeConstantText(pattern, token, span);
            var program = LythonRuntime.RegexCompiler.ProviderCall((provider, encoded, options, analysis, token),
                static state => state.provider.Compile(state.encoded, state.options, state.analysis.RegexMemoryOwner, state.token),
                governor, token, span, operationName: "Regex pattern compilation", materializePatternError: false);
            try
            {
                var captureSlotCount = LythonRuntime.RegexCompiler.ReadCaptureSlotCount(program, governor, token, span);
                regex = new(program, captureSlotCount,
                    analysis.ReadRegexGroupFacts(program, captureSlotCount, span));
                analysis.RetainRegexProgram(encoded, options, program, captureSlotCount, span);
            }
            catch { program.Dispose(); throw; }
            // The compiled lookup now owns the program, even if analysis
            // exits before publishing the new fact. Its scope keeps the
            // reservation for the encoded key and table.
            storage = null;
            analysis.RegexPatterns[(pattern, options)] = regex;
            return true;
        }
        catch (LythonRuntimeException error) when (error.ExceptionType is "PatternError" or "ValueError")
        {
            regex = default;
            return false;
        }
        finally { storage?.Dispose(); }
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
        var input = EncodeConstantText(text, token, span);
        var length = PyString.CountUtf8Units(input, false, token, span);
        var window = LythonRuntime.RegexCompiler.CreateWindow(input, length, pos, endPos, token, span);
        if (LythonRuntime.RegexCompiler.IsImpossibleWindow(operation, window.Pos, window.EndPos)) return false;
        using var subject = LythonRuntime.RegexCompiler.ProviderCall((pattern, input, window, analysis, token),
            static state => state.pattern.Prepare(state.input, state.window,
                state.analysis.MemoryGovernor?.Budget ?? new LythonMemoryBudget(), state.token),
            analysis.MemoryGovernor, token, span, operationName: "Regex constant-subject preparation");
        var useInlineCaptures = captureSlotCount <= LythonRuntime.RegexCompiler.InlineCaptureCapacity;
        using var storage = useInlineCaptures ? null
            : analysis.MemoryGovernor?.ReserveTemporary(LythonRuntime.RegexCompiler.CaptureBufferBytes(captureSlotCount), span);
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        LythonRuntime.RegexCompiler.InlineCaptureBuffer buffer = default;
        Span<LythonRegexCapture?> captures = buffer;
        captures = useInlineCaptures ? captures[..captureSlotCount] : new LythonRegexCapture?[captureSlotCount];
        if (!LythonRuntime.RegexCompiler.TryMatch(subject, operation, captures, out var lastIndex,
            analysis.MemoryGovernor, token, span, operationName: "Regex constant matching")) return false;

        var error = LythonRuntime.RegexCompiler.MatchResultError(input, window, operation, captures, lastIndex,
            analysis.MemoryGovernor, token, span);
        if (error is not null) throw RuntimeErrors.Capability(error, span);
        return true;
    }

    // Both callers admit the UTF-8 capacity before entering. For large
    // cancellable inputs, bound each count and encode operation while keeping
    // the same exact-sized backing and UTF-8 replacement behavior.
    private static byte[] EncodeConstantText(string text, CancellationToken token, LythonSourceSpan span)
    {
        const int chunkSize = 4096;
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        var encoding = Encoding.UTF8;
        if (!token.CanBeCanceled || text.Length <= chunkSize) return encoding.GetBytes(text);

        var count = 0;
        for (var remaining = text.AsSpan(); !remaining.IsEmpty;)
        {
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            var length = ChunkLength(remaining);
            count = checked(count + encoding.GetByteCount(remaining[..length]));
            remaining = remaining[length..];
        }

        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        var encoded = new byte[count];
        var output = encoded.AsSpan();
        for (var remaining = text.AsSpan(); !remaining.IsEmpty;)
        {
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            var length = ChunkLength(remaining);
            var written = encoding.GetBytes(remaining[..length], output);
            remaining = remaining[length..];
            output = output[written..];
        }
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        return encoded;

        static int ChunkLength(ReadOnlySpan<char> remaining)
            => remaining.Length <= chunkSize ? remaining.Length
                : char.IsSurrogatePair(remaining[chunkSize - 1], remaining[chunkSize]) ? chunkSize - 1 : chunkSize;
    }
}
