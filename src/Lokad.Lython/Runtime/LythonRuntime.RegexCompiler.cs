using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class RegexCompiler
    {
        internal const int PythonIgnoreCaseFlag = 2;
        internal const int PythonLocaleFlag = 4;
        internal const int PythonMultilineFlag = 8;
        internal const int PythonDotAllFlag = 16;
        internal const int PythonUnicodeFlag = 32;
        internal const int PythonVerboseFlag = 64;
        internal const int RegexDebugFlag = 128;
        internal const int PythonAsciiFlag = 256;

        internal static T ProviderCall<T>(Func<T> operation, MemoryGovernor? governor,
            CancellationToken cancellationToken, LythonSourceSpan? span)
        {
            governor?.ThrowIfAdmissionFailed();
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            try
            {
                var result = operation();
                try
                {
                    governor?.ThrowIfAdmissionFailed();
                    if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                    return result;
                }
                catch
                {
                    // Ownership has not reached the caller's using/field yet.
                    // A token changing as preparation returns must still close
                    // the acquired working resource before its owner can end.
                    if (result is ILythonRegexSubject subject) subject.Dispose();
                    else if (result is ILythonRegexCursor cursor) cursor.Dispose();
                    throw;
                }
            }
            catch (OperationCanceledException) { throw RuntimeErrors.Cancelled(span); }
            catch (LythonRegexException error)
            {
                throw error.Failure switch
                {
                    LythonRegexFailure.Capability => RuntimeErrors.Capability(error.Message, span),
                    LythonRegexFailure.MemoryAdmission => governor?.RefuseAdmission(error.Message, error.RequestedBytes, span)
                        ?? RuntimeErrors.Attributed("MemoryError", error.Message, span,
                            LythonFailureReason.SharedMemoryAllowance, requested: error.RequestedBytes),
                    _ => new LythonRuntimeException(ModuleException("re", "PatternError"), error.Message, span),
                };
            }
            catch (Exception error) when (error is not LythonRuntimeException and not OutOfMemoryException)
            { throw RuntimeErrors.Host("regex provider", error, span); }
        }

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

            var options = arguments.Length == 2
                ? ParseFlags(arguments[1], span, context)
                : new LythonRegexOptions(PythonUnicodeFlag, false);
            var rawFlags = options.Flags;

            var patternText = pattern.AsString();
            var cache = context.Services.State.RegexCache ??= new RegexPatternCache();
            if (cache.TryGet(patternText, rawFlags, out var cached) && cached is not null)
            {
                return cached;
            }

            var provider = context.State.RegexProvider
                ?? throw RuntimeErrors.Capability("Python regex functionality requires a host-supplied Lython regex provider.", span);
            var compiled = ProviderCall(() => provider.Compile(pattern.Utf8Bytes, options,
                context.MemoryGovernor.Budget, context.Limits.CancellationToken), context.MemoryGovernor,
                context.Limits.CancellationToken, span);
            var wrapperBytes = checked(128L + 64L * compiled.CaptureSlotCount);
            using var storage = context.MemoryGovernor.ReserveTemporary(wrapperBytes, span);
            var result = new RePatternObject(pattern, compiled.Flags, compiled,
                compiled.CaptureSlotCount, compiled.NamedGroups);
            storage.Commit(wrapperBytes);
            cache.Add(patternText, rawFlags, result, context.MemoryGovernor, span);
            return result;
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
                var compiledRange = CreateSubjectRange(text, pos, endPos);
                return new RegexPatternRange(compiled, compiledRange);
            }

            object[] patternArguments = arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance)
                ? [arguments[0], arguments[2]]
                : [arguments[0]];
            var matchRange = CreateSubjectRange(text, pos, endPos);
            return new RegexPatternRange(
                CreatePattern(patternArguments, signature, span, context),
                matchRange);
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

            var substituteInputs = new RegexSubstituteInputs(pattern, replacement, CreateSubjectRange(text, pos, endPos), count);
            return substituteInputs;
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

            var splitRange = CreateSubjectRange(text, pos, endPos);
            return new RegexSplitInputs(pattern, splitRange, maxSplit);
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
        private static LythonRegexOptions ParseFlags(object value, LythonSourceSpan span, ExecutionContext context)
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

            return new(flagBits, verbose);
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

    }
}
