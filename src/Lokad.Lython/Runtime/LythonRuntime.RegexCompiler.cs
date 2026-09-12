using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static class RegexCompiler
    {
        internal const int PythonIgnoreCaseFlag = 2;
        internal const int PythonLocaleFlag = 4;
        internal const int PythonMultilineFlag = 8;
        internal const int PythonDotAllFlag = 16;
        internal const int PythonUnicodeFlag = 32;
        internal const int PythonVerboseFlag = 64;
        internal const int RegexDebugFlag = 128;
        internal const int PythonAsciiFlag = 256;

        // Retained dependency compilation state scales with capture slots and
        // pattern size, not just pattern count. Base allowance holds the
        // audited single-group shape (~65MiB per 1000 retained compilations of
        // 'a(b|c)*d', heap-measured 1:1); each further slot budgets 2KiB and
        // each pattern byte past 64 budgets 256B, covering the steepest
        // heap-measured shapes (50-group, 500-group, and 2KB single-group
        // patterns at 1.0-2.4x headroom). Lython-owned record and group tables
        // ride inside it; per-match scratch stays separately bounded.
        internal const long CompiledPatternBytes = 65536;
        private const long GroupSlotBytes = 2048;
        private const long PatternByteBytes = 256;
        private const int BaseGroupSlots = 2;
        private const int BasePatternBytes = 64;

        internal static long EstimatePatternBytes(int captureSlotCount, int patternLength)
            => checked(
                CompiledPatternBytes +
                (GroupSlotBytes * Math.Max(0, captureSlotCount - BaseGroupSlots)) +
                (PatternByteBytes * Math.Max(0, patternLength - BasePatternBytes)));

        internal readonly record struct RegexPatternRange(RePatternObject Pattern, RegexSubjectRange Range);

        internal readonly record struct RegexSubstituteInputs(
            RePatternObject Pattern,
            object Replacement,
            RegexSubjectRange Range,
            int Count);

        internal readonly record struct RegexSplitInputs(RePatternObject Pattern, RegexSubjectRange Range, int MaxSplit);

        internal static RePatternObject CreatePattern(object[] arguments, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects a string pattern and optional flags.", span);
            }

            CheckFlagsHashable(arguments, 1, span);
            if (arguments[0] is RePatternObject existing)
            {
                if (arguments.Length == 2 && IsTruthy(arguments[1], context, span))
                {
                    throw new LythonRuntimeException("ValueError", "cannot process flags argument with a compiled pattern", span);
                }

                return existing;
            }

            if (!PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "first argument must be string or compiled pattern", span);
            }

            var (options, rawFlags) = arguments.Length == 2
                ? ParseFlags(arguments[1], span, context)
                : (PythonReCompileOptions.None, PythonUnicodeFlag);

            // Summarizing never throws (best-effort scan); hoisting it ahead of
            // the reservation sizes both the peak scratch and the durable charge.
            var groupSummary = RegexPatternFacts.SummarizeGroups(pattern.AsString());
            var patternBytes = EstimatePatternBytes(groupSummary.CaptureSlotCount, pattern.Utf8Bytes.Length);

            // Cover peak compilation scratch as well as the retained pattern;
            // the temporary releases on invalid patterns, the durable charge stays.
            using var scratch = context.MemoryGovernor.ReserveTemporary(patternBytes, span);
            try
            {
                var compiled = new Utf8PythonRegex(pattern.Utf8Bytes.Span, options);
                var reportedFlags = rawFlags | ParseLeadingInlinePythonFlags(pattern.AsString());

                var result = new RePatternObject(
                    pattern,
                    options,
                    reportedFlags,
                    compiled,
                    groupSummary.CaptureSlotCount,
                    groupSummary.NamedGroups);
                context.MemoryGovernor.Reserve(patternBytes, span);
                context.MemoryGovernor.Commit(patternBytes);
                return result;
            }
            catch (PythonRePatternException ex)
            {
                throw new LythonRuntimeException(ModuleException("re", "PatternError"), ex.Message, span);
            }
        }

        // The cache lookup hashes flags first, so unhashable flags fail before
        // any dispatch or compiled-pattern check like CPython.
        private static void CheckFlagsHashable(object[] arguments, int flagsIndex, LythonSourceSpan span)
        {
            if (arguments.Length > flagsIndex)
            {
                try
                {
                    _ = PyValueComparer.Instance.GetHashCode(arguments[flagsIndex]);
                }
                catch (InvalidOperationException)
                {
                    throw RuntimeErrors.UnhashableType(arguments[flagsIndex], span);
                }
            }
        }

        // A compiled pattern with truthy flags fails like CPython before any dispatch.
        private static void ThrowIfCompiledFlags(object[] arguments, int flagsIndex, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > flagsIndex && IsTruthy(arguments[flagsIndex], context, span))
            {
                throw new LythonRuntimeException("ValueError", "cannot process flags argument with a compiled pattern", span);
            }
        }

        internal static RegexPatternRange CreatePatternAndRange(object[] arguments, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 5 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, string, optional flags, pos, and endpos.", span);
            }

            CheckFlagsHashable(arguments, 2, span);
            var pos = arguments.Length >= 4 ? ParseOptionalIntOrDefault(arguments[3], 0, "pos", signature, span, context) : 0;
            var endPos = arguments.Length >= 5 ? ParseOptionalIntOrDefault(arguments[4], text.Length, "endpos", signature, span, context) : text.Length;

            if (arguments[0] is RePatternObject compiled)
            {
                ThrowIfCompiledFlags(arguments, 2, span, context);
                return new RegexPatternRange(compiled, CreateSubjectRange(text, pos, endPos));
            }

            object[] patternArguments = arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance)
                ? [arguments[0], arguments[2]]
                : [arguments[0]];
            return new RegexPatternRange(
                CreatePattern(patternArguments, signature, span, context),
                CreateSubjectRange(text, pos, endPos));
        }

        internal static RegexSubstituteInputs CreateSubstituteInputs(object[] arguments, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 3 or > 7 || !PyStringOps.TryAsString(arguments[2], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, replacement, text, optional count, flags, pos, and endpos.", span);
            }

            var replacement = arguments[1];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects replacement to be a string or callable.", span);
            }

            var count = arguments.Length >= 4
                ? ParseOptionalIntOrDefault(arguments[3], 0, "count", signature, span, context)
                : 0;
            var pos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], 0, "pos", signature, span, context) : 0;
            var endPos = arguments.Length >= 7 ? ParseOptionalIntOrDefault(arguments[6], text.Length, "endpos", signature, span, context) : text.Length;

            CheckFlagsHashable(arguments, 4, span);
            RePatternObject pattern;
            if (arguments[0] is RePatternObject compiled)
            {
                ThrowIfCompiledFlags(arguments, 4, span, context);
                pattern = compiled;
            }
            else
            {
                object[] patternArguments = arguments.Length >= 5 && !ReferenceEquals(arguments[4], PyNone.Instance)
                    ? [arguments[0], arguments[4]]
                    : [arguments[0]];
                pattern = CreatePattern(patternArguments, signature, span, context);
            }

            return new RegexSubstituteInputs(pattern, replacement, CreateSubjectRange(text, pos, endPos), count);
        }

        internal static RegexSplitInputs CreateSplitInputs(object[] arguments, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 6 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, string, optional maxsplit, flags, pos, and endpos.", span);
            }

            var maxSplit = 0;
            var pos = arguments.Length >= 5 ? ParseOptionalIntOrDefault(arguments[4], 0, "pos", signature, span, context) : 0;
            var endPos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], text.Length, "endpos", signature, span, context) : text.Length;

            CheckFlagsHashable(arguments, 3, span);
            RePatternObject pattern;
            if (arguments[0] is RePatternObject compiled)
            {
                ThrowIfCompiledFlags(arguments, 3, span, context);
                pattern = compiled;
                if (arguments.Length == 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span, context);
                }
            }
            else
            {
                object[] patternArguments = arguments.Length >= 4 && !ReferenceEquals(arguments[3], PyNone.Instance)
                    ? [arguments[0], arguments[3]]
                    : [arguments[0]];
                pattern = CreatePattern(patternArguments, signature, span, context);
                if (arguments.Length >= 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span, context);
                }
            }

            return new RegexSplitInputs(pattern, CreateSubjectRange(text, pos, endPos), maxSplit);
        }

        // Regex bounds coerce through __index__ like CPython. Module-level
        // calls arrive binder-filled, where None marks an omitted optional,
        // while compiled-method calls pass raw values where explicit None is
        // rejected like any other non-integer. Out-of-ssize magnitudes report
        // the ssize_t overflow while int-range overflows keep the historical
        // out-of-range shape.
        internal static int ParseOptionalIntOrDefault(object? value, int defaultValue, string name, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is null || ReferenceEquals(value, PyNone.Instance))
            {
                return defaultValue;
            }

            return ParseBoundInteger(value, name, signature, span, context);
        }

        internal static int ParseExplicitInt(object value, string name, string signature, LythonSourceSpan span, ExecutionContext context)
            => ParseBoundInteger(value, name, signature, span, context);

        private static int ParseBoundInteger(object? value, string name, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            var coerced = value is null ? null : LythonRuntime.CoerceIndexProtocol(value, context, span);
            BigInteger integer;
            if (coerced is bool flag)
            {
                integer = flag ? BigInteger.One : BigInteger.Zero;
            }
            else if (coerced is int small)
            {
                integer = new BigInteger(small);
            }
            else if (coerced is not BigInteger big)
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
            }
            else
            {
                integer = big;
            }

            if (integer > long.MaxValue || integer < long.MinValue)
            {
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
            }

            if (integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span);
            }

            return (int)integer;
        }

        internal static RegexSubjectRange CreateSubjectRange(PyString text, int pos, int endPos)
        {
            var length = text.Length;
            var normalizedPos = Math.Clamp(pos, 0, length);
            var normalizedEnd = Math.Clamp(endPos, 0, length);
            if (normalizedEnd < normalizedPos)
            {
                return new RegexSubjectRange(text, PyString.Empty, normalizedPos, normalizedEnd, IsValid: false);
            }

            var startByte = text.GetByteIndexForRuneBoundary(normalizedPos);
            var endByte = text.GetByteIndexForRuneBoundary(normalizedEnd);
            return new RegexSubjectRange(text, text.SliceByByteRange(startByte, endByte), normalizedPos, normalizedEnd);
        }

        // Flags flow through operator dispatch like CPython (sre fix_flags plus
        // the _code/_sre combines): verbose/locale/ascii checks, the unicode
        // combine, debug checks, then the state combines; the converted C int
        // carries unknown bits through to the echo.
        private static (PythonReCompileOptions Options, int Flags) ParseFlags(object value, LythonSourceSpan span, ExecutionContext context)
        {
            // Verbose parsing follows the dispatched bit like CPython.
            var verbose = IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(PythonVerboseFlag), context, span), context, span);
            if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(PythonLocaleFlag), context, span), context, span))
            {
                throw new LythonRuntimeException("ValueError", "cannot use LOCALE flag with a str pattern", span);
            }

            object stateFlags;
            if (!IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(PythonAsciiFlag), context, span), context, span))
            {
                stateFlags = OrAssignFlags(value, span, context);
            }
            else
            {
                if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(PythonUnicodeFlag), context, span), context, span))
                {
                    throw new LythonRuntimeException("ValueError", "ASCII and UNICODE flags are incompatible", span);
                }

                stateFlags = value;
            }

            // Debug checks run through dispatch like CPython; Lython cannot print,
            // so a set DEBUG bit stays NotImplementedError on the converted value.
            _ = EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(RegexDebugFlag), context, span);
            var combined = EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseOr, stateFlags, value, context, span);
            _ = EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(RegexDebugFlag), context, span);
            var flagBits = ToRegexCInt(EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseOr, value, combined, context, span), span, context);
            _ = EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(RegexDebugFlag), context, span);

            if ((flagBits & RegexDebugFlag) != 0)
            {
                throw new LythonRuntimeException("NotImplementedError", "re.DEBUG is not supported by Lython's regex runtime.", span);
            }

            var options = PythonReCompileOptions.None;
            if (verbose) options |= PythonReCompileOptions.Verbose;
            if ((flagBits & PythonIgnoreCaseFlag) != 0) options |= PythonReCompileOptions.IgnoreCase;
            if ((flagBits & PythonMultilineFlag) != 0) options |= PythonReCompileOptions.Multiline;
            if ((flagBits & PythonDotAllFlag) != 0) options |= PythonReCompileOptions.DotAll;
            if ((flagBits & PythonAsciiFlag) != 0) options |= PythonReCompileOptions.Ascii;
            return (options, flagBits);
        }

        // `flags |= UNICODE` like CPython. Only instances carry hooks, so only
        // they need the manual __ior__/__or__/__ror__ chain with `|=` text;
        // int-like values ride the shared binary shape, which cannot fail them.
        private static object OrAssignFlags(object flags, LythonSourceSpan span, ExecutionContext context)
        {
            var uni = new BigInteger(PythonUnicodeFlag);
            if (flags is PyInstance)
            {
                object? assigned;
                if (TryHookOrAssign(flags, "__ior__", uni, context, span, out assigned))
                {
                    return assigned.RequireNotNull();
                }

                if (TryHookOrAssign(flags, "__or__", uni, context, span, out assigned))
                {
                    return assigned.RequireNotNull();
                }

                if (TryHookOrAssign(uni, "__ror__", flags, context, span, out assigned))
                {
                    return assigned.RequireNotNull();
                }

                throw RuntimeErrors.UnsupportedOperands("|=", flags, uni, span);
            }

            return EvaluateBinaryOperator(BinaryOperatorSyntax.BitwiseOr, flags, uni, context, span);
        }

        private static bool TryHookOrAssign(object target, string method, object argument, ExecutionContext context, LythonSourceSpan span, out object? result)
        {
            if (TryInvokeBinarySpecialMethod(target, method, argument, context, span, out var value) && value is not PyNotImplemented)
            {
                result = value;
                return true;
            }

            result = null;
            return false;
        }

        // The converted C int carries unknown bits through; out-of-range
        // magnitudes report the C-int overflow while other shapes name the type.
        private static int ToRegexCInt(object value, LythonSourceSpan span, ExecutionContext context)
            => value switch
            {
                bool flag => flag ? 1 : 0,
                int small => small,
                BigInteger big when big >= int.MinValue && big <= int.MaxValue => (int)big,
                BigInteger => throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span),
                _ => throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span),
            };

        private static int ParseLeadingInlinePythonFlags(string pattern)
        {
            if (!pattern.StartsWith("(?", StringComparison.Ordinal))
            {
                return 0;
            }

            var flags = 0;
            for (var index = 2; index < pattern.Length; index++)
            {
                switch (pattern[index])
                {
                    case ')': return flags;
                    case 'a': flags |= PythonAsciiFlag; break;
                    case 'i': flags |= PythonIgnoreCaseFlag; break;
                    case 'm': flags |= PythonMultilineFlag; break;
                    case 's': flags |= PythonDotAllFlag; break;
                    case 'u': flags |= PythonUnicodeFlag; break;
                    case 'x': flags |= PythonVerboseFlag; break;
                    default: return 0;
                }
            }

            return 0;
        }
    }
}
