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

        private const int SupportedPythonFlags =
            PythonIgnoreCaseFlag |
            PythonLocaleFlag |
            PythonMultilineFlag |
            PythonDotAllFlag |
            PythonUnicodeFlag |
            PythonVerboseFlag |
            RegexDebugFlag |
            PythonAsciiFlag;

        internal readonly record struct RegexPatternRange(RePatternObject Pattern, RegexSubjectRange Range);

        internal readonly record struct RegexSubstituteInputs(
            RePatternObject Pattern,
            object Replacement,
            RegexSubjectRange Range,
            int Count);

        internal readonly record struct RegexSplitInputs(RePatternObject Pattern, RegexSubjectRange Range, int MaxSplit);

        internal static RePatternObject CreatePattern(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects a string pattern and optional flags.", span);
            }

            var options = arguments.Length == 2
                ? ParseFlags(arguments[1], signature, span)
                : PythonReCompileOptions.None;

            try
            {
                var compiled = new Utf8PythonRegex(pattern.Utf8Bytes.Span, options);
                var groupSummary = RegexPatternFacts.SummarizeGroups(pattern.AsString());
                var reportedFlags = ToPythonFlags(options) | ParseLeadingInlinePythonFlags(pattern.AsString());
                if ((reportedFlags & PythonAsciiFlag) == 0)
                {
                    reportedFlags |= PythonUnicodeFlag;
                }

                return new RePatternObject(
                    pattern,
                    options,
                    reportedFlags,
                    compiled,
                    groupSummary.CaptureSlotCount,
                    groupSummary.NamedGroups);
            }
            catch (PythonRePatternException ex)
            {
                throw new LythonRuntimeException(ModuleException("re", "PatternError"), ex.Message, span);
            }
        }

        internal static RegexPatternRange CreatePatternAndRange(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 5 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, string, optional flags, pos, and endpos.", span);
            }

            var pos = arguments.Length >= 4 ? ParseOptionalIntOrDefault(arguments[3], 0, "pos", signature, span) : 0;
            var endPos = arguments.Length >= 5 ? ParseOptionalIntOrDefault(arguments[4], text.Length, "endpos", signature, span) : text.Length;

            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                return new RegexPatternRange(compiled, CreateSubjectRange(text, pos, endPos));
            }

            return new RegexPatternRange(
                CreatePattern(arguments.Length >= 3 ? [arguments[0], arguments[2]] : [arguments[0]], signature, span),
                CreateSubjectRange(text, pos, endPos));
        }

        internal static RegexSubstituteInputs CreateSubstituteInputs(object[] arguments, string signature, LythonSourceSpan span)
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
                ? ParseOptionalIntOrDefault(arguments[3], 0, "count", signature, span)
                : 0;
            var pos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], 0, "pos", signature, span) : 0;
            var endPos = arguments.Length >= 7 ? ParseOptionalIntOrDefault(arguments[6], text.Length, "endpos", signature, span) : text.Length;

            RePatternObject pattern;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length >= 5 && !ReferenceEquals(arguments[4], PyNone.Instance))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                pattern = compiled;
            }
            else
            {
                object[] patternArguments = arguments.Length >= 5 ? [arguments[0], arguments[4]] : [arguments[0]];
                pattern = CreatePattern(patternArguments, signature, span);
            }

            return new RegexSubstituteInputs(pattern, replacement, CreateSubjectRange(text, pos, endPos), count);
        }

        internal static RegexSplitInputs CreateSplitInputs(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 6 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, string, optional maxsplit, flags, pos, and endpos.", span);
            }

            var maxSplit = 0;
            var pos = arguments.Length >= 5 ? ParseOptionalIntOrDefault(arguments[4], 0, "pos", signature, span) : 0;
            var endPos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], text.Length, "endpos", signature, span) : text.Length;

            RePatternObject pattern;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length >= 4 && !ReferenceEquals(arguments[3], PyNone.Instance))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                pattern = compiled;
                if (arguments.Length == 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span);
                }
            }
            else
            {
                object[] patternArguments = arguments.Length >= 4 ? [arguments[0], arguments[3]] : [arguments[0]];
                pattern = CreatePattern(patternArguments, signature, span);
                if (arguments.Length >= 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span);
                }
            }

            return new RegexSplitInputs(pattern, CreateSubjectRange(text, pos, endPos), maxSplit);
        }

        internal static int ParseOptionalIntOrDefault(object value, int defaultValue, string name, string signature, LythonSourceSpan span)
            => ReferenceEquals(value, PyNone.Instance)
                ? defaultValue
                : ParseOptionalInt(value, name, signature, span);

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

        private static PythonReCompileOptions ParseFlags(object value, string signature, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return PythonReCompileOptions.None;
            }

            var flags = value switch
            {
                BigInteger integer => integer,
                int integer => new BigInteger(integer),
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects flags to be an integer bitmask.", span)
            };

            if (flags < 0 || flags > int.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", "Regex flags are out of range.", span);
            }

            var flagBits = (int)flags;
            if ((flagBits & ~SupportedPythonFlags) != 0)
            {
                throw new LythonRuntimeException("ValueError", "Unsupported regular expression flags.", span);
            }

            if ((flagBits & RegexDebugFlag) != 0)
            {
                throw new LythonRuntimeException("NotImplementedError", "re.DEBUG is not supported by Lython's regex runtime.", span);
            }

            if ((flagBits & PythonLocaleFlag) != 0)
            {
                throw new LythonRuntimeException("NotImplementedError", "re.LOCALE is not supported by Lython's Unicode-only regex runtime.", span);
            }

            if ((flagBits & PythonAsciiFlag) != 0 && (flagBits & PythonUnicodeFlag) != 0)
            {
                throw new LythonRuntimeException("ValueError", "ASCII and UNICODE flags are incompatible.", span);
            }

            var options = PythonReCompileOptions.None;
            if ((flagBits & PythonIgnoreCaseFlag) != 0) options |= PythonReCompileOptions.IgnoreCase;
            if ((flagBits & PythonMultilineFlag) != 0) options |= PythonReCompileOptions.Multiline;
            if ((flagBits & PythonDotAllFlag) != 0) options |= PythonReCompileOptions.DotAll;
            if ((flagBits & PythonVerboseFlag) != 0) options |= PythonReCompileOptions.Verbose;
            if ((flagBits & PythonAsciiFlag) != 0) options |= PythonReCompileOptions.Ascii;
            return options;
        }

        private static int ParseOptionalInt(object value, string name, string signature, LythonSourceSpan span)
            => RuntimeArgumentValidation.ParseInt32(value, name, signature, span);

        private static int ToPythonFlags(PythonReCompileOptions options)
        {
            var flags = 0;
            if ((options & PythonReCompileOptions.IgnoreCase) != 0) flags |= PythonIgnoreCaseFlag;
            if ((options & PythonReCompileOptions.Multiline) != 0) flags |= PythonMultilineFlag;
            if ((options & PythonReCompileOptions.DotAll) != 0) flags |= PythonDotAllFlag;
            if ((options & PythonReCompileOptions.Verbose) != 0) flags |= PythonVerboseFlag;
            if ((options & PythonReCompileOptions.Ascii) != 0) flags |= PythonAsciiFlag;
            return flags;
        }

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
