using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed record ReCapture(PyString Value, BigInteger Start, BigInteger End);

    internal sealed record ReMatchObject(
        PyString Value,
        BigInteger Start,
        BigInteger End,
        RePatternObject Pattern,
        PyString String,
        BigInteger Pos,
        BigInteger EndPos,
        int CaptureSlotCount,
        IReadOnlyList<ReCapture?> Captures,
        IReadOnlyDictionary<string, int> NamedGroups);

    internal sealed record ReFindAllResult(PyList Items)
        : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
    {
        public int Count => Items.Count;

        public int Length => Items.Length;

        public object this[int index] => Items[index];

        public object GetItem(int index) => Items.GetItem(index);

        public object CreateSlice(IEnumerable<object> items) => Items.CreateSlice(items);

        public object GetIndex(int index) => Items.GetIndex(index);

        public object GetSlice(IEnumerable<int> indices) => Items.GetSlice(indices);

        public bool IsTruthy() => Items.IsTruthy();

        public IEnumerable<object> Iterate() => Items;

        public PyString RenderPython(PyRenderingContext context) => Items.RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => Items.RenderInterpolated(context);

        public IEnumerator<object> GetEnumerator() => Items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed record RePatternObject(
        PyString Pattern,
        PythonReCompileOptions Options,
        int Flags,
        Utf8PythonRegex Regex,
        int CaptureSlotCount,
        IReadOnlyDictionary<string, int> NamedGroups);

    internal readonly record struct RegexSubjectRange(
        PyString Original,
        PyString Segment,
        int Pos,
        int EndPos,
        bool IsValid)
    {
        public RegexSubjectRange(PyString Original, PyString Segment, int Pos, int EndPos)
            : this(Original, Segment, Pos, EndPos, true)
        {
        }
    }

    private sealed class ReModule : PyModule
    {
        public static readonly ReModule Instance = new();

        private ReModule() : base("re")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "compile" => new BuiltinCallable(LythonKnownCallableSignatures.ReCompile, Compile),
                "search" => new BuiltinCallable(LythonKnownCallableSignatures.ReSearch, Search),
                "match" => new BuiltinCallable(LythonKnownCallableSignatures.ReMatch, Match),
                "fullmatch" => new BuiltinCallable(LythonKnownCallableSignatures.ReFullMatch, FullMatch),
                "findall" => new BuiltinCallable(LythonKnownCallableSignatures.ReFindAll, FindAll),
                "finditer" => new BuiltinCallable(LythonKnownCallableSignatures.ReFindIter, FindIter),
                "sub" => new BuiltinCallable(LythonKnownCallableSignatures.ReSub, Substitute),
                "subn" => new BuiltinCallable(LythonKnownCallableSignatures.ReSubn, SubstituteCount),
                "split" => new BuiltinCallable(LythonKnownCallableSignatures.ReSplit, Split),
                "escape" => new BuiltinCallable(LythonKnownCallableSignatures.ReEscape, Escape),
                "purge" => new BuiltinCallable(LythonKnownCallableSignatures.RePurge, Purge),
                "error" => new ExceptionTypeValue("error"),
                "PatternError" => new ExceptionTypeValue("PatternError"),
                "RegexFlag" => new RegexFlagFactory(),
                "NOFLAG" => BigInteger.Zero,
                "IGNORECASE" => new BigInteger(PythonIgnoreCaseFlag),
                "I" => new BigInteger(PythonIgnoreCaseFlag),
                "UNICODE" => new BigInteger(PythonUnicodeFlag),
                "U" => new BigInteger(PythonUnicodeFlag),
                "MULTILINE" => new BigInteger(PythonMultilineFlag),
                "M" => new BigInteger(PythonMultilineFlag),
                "DOTALL" => new BigInteger(PythonDotAllFlag),
                "S" => new BigInteger(PythonDotAllFlag),
                "VERBOSE" => new BigInteger(PythonVerboseFlag),
                "X" => new BigInteger(PythonVerboseFlag),
                "ASCII" => new BigInteger(PythonAsciiFlag),
                "A" => new BigInteger(PythonAsciiFlag),
                "LOCALE" => new BigInteger(PythonLocaleFlag),
                "L" => new BigInteger(PythonLocaleFlag),
                "DEBUG" => new BigInteger(RegexDebugFlag),
                "Pattern" => PyString.FromString("re.Pattern"),
                "Match" => PyString.FromString("re.Match"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private const int PythonIgnoreCaseFlag = 2;
        private const int PythonLocaleFlag = 4;
        private const int PythonMultilineFlag = 8;
        private const int PythonDotAllFlag = 16;
        private const int PythonUnicodeFlag = 32;
        private const int PythonVerboseFlag = 64;
        private const int RegexDebugFlag = 128;
        private const int PythonAsciiFlag = 256;
        private const int SupportedPythonFlags =
            PythonIgnoreCaseFlag |
            PythonLocaleFlag |
            PythonMultilineFlag |
            PythonDotAllFlag |
            PythonUnicodeFlag |
            PythonVerboseFlag |
            RegexDebugFlag |
            PythonAsciiFlag;

        private sealed class RegexFlagFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
        {
            public string Name => "re.RegexFlag";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var bound = CallBinder.BindNamedArguments(arguments, span, new LythonCallableSignature("re.RegexFlag", ["value"], RequiredCount: 0), PythonCallableKind.Builtin);
                if (bound.Length == 0 || ReferenceEquals(bound[0], PyNone.Instance))
                {
                    return BigInteger.Zero;
                }

                return bound[0] switch
                {
                    BigInteger integer => integer,
                    int integer => new BigInteger(integer),
                    _ => throw new LythonRuntimeException("TypeError", "re.RegexFlag(value=0) expects an integer value.", span)
                };
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString(Name);
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private object Compile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return CreatePattern(arguments, "re.compile(pattern[, flags])", span);
        }

        private object Search(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range) = CreatePatternAndRange(arguments, "re.search(pattern, string[, flags][, pos][, endpos])", span);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = pattern.Regex.SearchDetailedData(range.Segment.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(pattern, range, match, context, span) : PyNone.Instance;
        }

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range) = CreatePatternAndRange(arguments, "re.match(pattern, string[, flags][, pos][, endpos])", span);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = pattern.Regex.MatchDetailedData(range.Segment.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(pattern, range, match, context, span) : PyNone.Instance;
        }

        private object FullMatch(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range) = CreatePatternAndRange(arguments, "re.fullmatch(pattern, string[, flags][, pos][, endpos])", span);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = pattern.Regex.FullMatchDetailedData(range.Segment.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(pattern, range, match, context, span) : PyNone.Instance;
        }

        private object FindAll(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range) = CreatePatternAndRange(arguments, "re.findall(pattern, string[, flags][, pos][, endpos])", span);
            return CreateFindAllResult(pattern, range, span, context);
        }

        private object FindIter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range) = CreatePatternAndRange(arguments, "re.finditer(pattern, string[, flags][, pos][, endpos])", span);
            return CreateFindIterMatches(pattern, range, context, span);
        }

        private object Substitute(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, replacement, range, count) = CreateSubstituteInputs(arguments, "re.sub(pattern, replacement, string[, count][, flags][, pos][, endpos])", span);
            return ExecuteSubstitute(pattern, replacement, range, count, span, context, includeCount: false);
        }

        private object SubstituteCount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, replacement, range, count) = CreateSubstituteInputs(arguments, "re.subn(pattern, replacement, string[, count][, flags][, pos][, endpos])", span);
            return ExecuteSubstitute(pattern, replacement, range, count, span, context, includeCount: true);
        }

        private object Split(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, range, maxSplit) = CreateSplitInputs(arguments, "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos])", span);
            return ProjectSplitResult(pattern.Regex.SplitDetailed(range.Segment.Utf8Bytes.Span, maxSplit), span, context);
        }

        private object Escape(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "re.escape(string) expects one string argument.", span);
            }

            return PyStringOps.EscapeRegex(text);
        }

        private object Purge(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "re.purge() expects no arguments.", span);
            }

            return PyNone.Instance;
        }

        private static RePatternObject CreatePattern(object[] arguments, string signature, LythonSourceSpan span)
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
                throw new LythonRuntimeException("error", ex.Message, span);
            }
        }

        private static (RePatternObject Pattern, RegexSubjectRange Range) CreatePatternAndRange(object[] arguments, string signature, LythonSourceSpan span)
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

                return (compiled, CreateSubjectRange(text, pos, endPos));
            }

            return (CreatePattern(arguments.Length >= 3 ? [arguments[0], arguments[2]] : [arguments[0]], signature, span), CreateSubjectRange(text, pos, endPos));
        }

        private static (RePatternObject Pattern, object Replacement, RegexSubjectRange Range, int Count) CreateSubstituteInputs(object[] arguments, string signature, LythonSourceSpan span)
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

            var count = 0;
            var pos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], 0, "pos", signature, span) : 0;
            var endPos = arguments.Length >= 7 ? ParseOptionalIntOrDefault(arguments[6], text.Length, "endpos", signature, span) : text.Length;
            object[] patternArguments;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length >= 5 && !ReferenceEquals(arguments[4], PyNone.Instance))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                patternArguments = [compiled];
                if (arguments.Length >= 4)
                {
                    count = ParseOptionalIntOrDefault(arguments[3], 0, "count", signature, span);
                }
            }
            else
            {
                patternArguments = arguments.Length >= 5 ? [arguments[0], arguments[4]] : [arguments[0]];
                if (arguments.Length >= 4)
                {
                    count = ParseOptionalIntOrDefault(arguments[3], 0, "count", signature, span);
                }
            }

            var pattern = patternArguments[0] is RePatternObject existing
                ? existing
                : CreatePattern(patternArguments, signature, span);
            return (pattern, replacement, CreateSubjectRange(text, pos, endPos), count);
        }

        internal static object ExecuteSubstitute(RePatternObject pattern, object replacement, RegexSubjectRange range, int count, LythonSourceSpan span, ExecutionContext context, bool includeCount)
        {
            if (UsesDotStarLazyProgression(pattern))
            {
                return ExecuteDotStarLazySubstitute(pattern, replacement, range, count, span, context, includeCount);
            }

            if (PyStringOps.TryAsString(replacement, out var replacementText))
            {
                if (!includeCount)
                {
                    var replacedText = CreateUtf8String(pattern.Regex.Replace(range.Segment.Utf8Bytes.Span, replacementText.AsString(), count), context, span);
                    return SpliceRangeResult(range, replacedText);
                }

                var result = pattern.Regex.Subn(range.Segment.Utf8Bytes.Span, replacementText.AsString(), count);
                var replacedTextWithCount = CreateUtf8String(result.ResultBytes, context, span);
                return new PyTuple([SpliceRangeResult(range, replacedTextWithCount), new BigInteger(result.ReplacementCount)], context.MemoryGovernor, span);
            }

            if (replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "re.subn(...) replacement must be a string or callable."
                    : "re.sub(...) replacement must be a string or callable.", span);
            }

            var (resultText, replacementCount) = ExecuteCallableSubstitute(pattern, replacement, range, count, span, context);
            return includeCount
                ? new PyTuple([resultText, new BigInteger(replacementCount)], context.MemoryGovernor, span)
                : resultText;
        }

        private static (RePatternObject Pattern, RegexSubjectRange Range, int MaxSplit) CreateSplitInputs(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 6 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, string, optional maxsplit, flags, pos, and endpos.", span);
            }

            var maxSplit = 0;
            var pos = arguments.Length >= 5 ? ParseOptionalIntOrDefault(arguments[4], 0, "pos", signature, span) : 0;
            var endPos = arguments.Length >= 6 ? ParseOptionalIntOrDefault(arguments[5], text.Length, "endpos", signature, span) : text.Length;
            object[] patternArguments;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length >= 4 && !ReferenceEquals(arguments[3], PyNone.Instance))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                patternArguments = [compiled];
                if (arguments.Length == 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span);
                }
            }
            else
            {
                patternArguments = arguments.Length >= 4 ? [arguments[0], arguments[3]] : [arguments[0]];
                if (arguments.Length >= 3)
                {
                    maxSplit = ParseOptionalIntOrDefault(arguments[2], 0, "maxsplit", signature, span);
                }
            }

            var pattern = patternArguments[0] is RePatternObject existing
                ? existing
                : CreatePattern(patternArguments, signature, span);
            return (pattern, CreateSubjectRange(text, pos, endPos), maxSplit);
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

        internal static int ParseOptionalInt(object value, string name, string signature, LythonSourceSpan span)
        {
            return value switch
            {
                BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                    ? throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span)
                    : (int)integer,
                int integer => integer,
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects {name} to be an integer.", span)
            };
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

        private static PyString SpliceRangeResult(RegexSubjectRange range, PyString segmentReplacement)
        {
            if (range.Pos == 0 && range.EndPos == range.Original.Length)
            {
                return segmentReplacement;
            }

            var startByte = range.Original.GetByteIndexForRuneBoundary(range.Pos);
            var endByte = range.Original.GetByteIndexForRuneBoundary(range.EndPos);
            var prefix = range.Original.SliceByByteRange(0, startByte);
            var suffix = range.Original.SliceByByteRange(endByte, range.Original.Utf8Bytes.Length);
            return prefix.Concat(segmentReplacement).Concat(suffix);
        }

        internal static PyRegexFindIterator CreateFindIterMatches(RePatternObject pattern, RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
            => new(pattern, range, context, span);

        internal static ReFindAllResult CreateFindAllResult(RePatternObject pattern, RegexSubjectRange range, LythonSourceSpan span, ExecutionContext context)
        {
            if (!UsesDotStarLazyProgression(pattern))
            {
                return new ReFindAllResult(RePatternMembers.ProjectFindAllResult(pattern.Regex.FindAllToUtf8(range.Segment.Utf8Bytes.Span), span, context));
            }

            var values = CreateDotStarLazyMatches(pattern, range)
                .Select(match => (object)CreateString(match.Value.ValueText, context, span));
            return new ReFindAllResult(new PyList(values, context.MemoryGovernor, span));
        }

        internal static IReadOnlyList<Utf8PythonDetailedMatchData> CreateDetailedFindMatches(RePatternObject pattern, RegexSubjectRange range)
            => UsesDotStarLazyProgression(pattern)
                ? CreateDotStarLazyMatches(pattern, range)
                : range.IsValid ? pattern.Regex.FindIterDetailed(range.Segment.Utf8Bytes.Span) : [];

        private static bool UsesDotStarLazyProgression(RePatternObject pattern)
            => pattern.Pattern.AsString() == ".*?" && pattern.CaptureSlotCount == 1;

        private static Utf8PythonDetailedMatchData[] CreateDotStarLazyMatches(RePatternObject pattern, RegexSubjectRange range)
        {
            if (!range.IsValid)
            {
                return [];
            }

            var matches = new List<Utf8PythonDetailedMatchData>(range.Segment.Length * 2 + 1);
            var dotAll = (pattern.Options & PythonReCompileOptions.DotAll) != 0;
            for (var runeIndex = 0; runeIndex <= range.Segment.Length; runeIndex++)
            {
                var startByte = range.Segment.GetByteIndexForRuneBoundary(runeIndex);
                matches.Add(CreateSyntheticDetailedMatch(startByte, startByte, runeIndex, runeIndex, string.Empty));
                if (runeIndex == range.Segment.Length)
                {
                    continue;
                }

                var endByte = range.Segment.GetByteIndexForRuneBoundary(runeIndex + 1);
                var value = range.Segment.SliceByByteRange(startByte, endByte).AsString();
                if (dotAll || value != "\n")
                {
                    matches.Add(CreateSyntheticDetailedMatch(startByte, endByte, runeIndex, runeIndex + value.Length, value));
                }
            }

            return [.. matches];
        }

        private static Utf8PythonDetailedMatchData CreateSyntheticDetailedMatch(int startByte, int endByte, int startUtf16, int endUtf16, string value)
            => new()
            {
                Groups =
                [
                    new Utf8PythonGroupMatchData
                    {
                        Number = 0,
                        Success = true,
                        StartOffsetInBytes = startByte,
                        EndOffsetInBytes = endByte,
                        StartOffsetInUtf16 = startUtf16,
                        EndOffsetInUtf16 = endUtf16,
                        HasContiguousByteRange = true,
                        ValueText = value,
                    }
                ],
                NameEntries = [],
            };

        private static object ExecuteDotStarLazySubstitute(
            RePatternObject pattern,
            object replacement,
            RegexSubjectRange range,
            int count,
            LythonSourceSpan span,
            ExecutionContext context,
            bool includeCount)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var sourceBytes = range.Segment.Utf8Bytes.Span;
            var lastByte = 0;
            var replaced = 0;
            foreach (var match in CreateDotStarLazyMatches(pattern, range))
            {
                if (count != 0 && replaced >= count)
                {
                    break;
                }

                var whole = match.Value;
                builder.Append(sourceBytes[lastByte..whole.StartOffsetInBytes]);
                var matchObject = CreateMatchObject(pattern, range, match, context, span);
                if (PyStringOps.TryAsString(replacement, out var template))
                {
                    builder.Append(ReMatchMembers.ExpandReplacementTemplate(matchObject, template, context, span));
                }
                else
                {
                    var replacementValue = InvokeCallableTarget(
                        replacement,
                        span,
                        span,
                        context,
                        () => [CallArgumentValue.Positional(matchObject)]);
                    if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
                    {
                        throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", span);
                    }

                    builder.Append(replacementText);
                }

                lastByte = whole.EndOffsetInBytes;
                replaced++;
            }

            builder.Append(sourceBytes[lastByte..]);
            var result = SpliceRangeResult(range, builder.ToPyStringAndRelease());
            return includeCount
                ? new PyTuple([result, new BigInteger(replaced)], context.MemoryGovernor, span)
                : result;
        }

        internal static (PyString Result, int ReplacementCount) ExecuteCallableSubstitute(
            RePatternObject pattern,
            object replacement,
            RegexSubjectRange range,
            int count,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var state = new RegexReplacementState(pattern, range, replacement, span, context);
            var result = pattern.Regex.Subn(
                range.Segment.Utf8Bytes.Span,
                state,
                static (replacementState, match) => EvaluateRegexReplacement(replacementState, match),
                count);
            return (SpliceRangeResult(range, CreateUtf8String(result.ResultBytes, context, span)), result.ReplacementCount);
        }

        private sealed record RegexReplacementState(
            RePatternObject Pattern,
            RegexSubjectRange Range,
            object Replacement,
            LythonSourceSpan Span,
            ExecutionContext Context);

        private static string EvaluateRegexReplacement(RegexReplacementState state, Utf8PythonDetailedMatchData match)
        {
            state.Context.CheckExecutionBudget(state.Span);
            var matchObject = CreateMatchObject(state.Pattern, state.Range, match, state.Context, state.Span);
            var replacementValue = InvokeCallableTarget(
                state.Replacement,
                state.Span,
                state.Span,
                state.Context,
                () => [CallArgumentValue.Positional(matchObject)]);

            if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
            {
                throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", state.Span);
            }

            return replacementText.AsString();
        }

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range, Utf8PythonDetailedMatchData match)
            => CreateMatchObject(pattern, range, match, null, null);

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range, Utf8PythonDetailedMatchData match, ExecutionContext? context)
            => CreateMatchObject(pattern, range, match, context, null);

        internal static ReMatchObject CreateMatchObject(
            RePatternObject pattern,
            RegexSubjectRange range,
            Utf8PythonDetailedMatchData match,
            ExecutionContext? context,
            LythonSourceSpan? span)
        {
            if (!match.TryGetGroup(0, out var wholeGroup) || !wholeGroup.Success)
            {
                throw new InvalidOperationException("Detailed regex match is missing the whole-match capture.");
            }

            var wholeStart = wholeGroup.HasContiguousByteRange
                ? range.Pos + range.Segment.ByteIndexToRuneIndex(wholeGroup.StartOffsetInBytes)
                : wholeGroup.StartOffsetInUtf16;
            var wholeEnd = wholeGroup.HasContiguousByteRange
                ? range.Pos + range.Segment.ByteIndexToRuneIndex(wholeGroup.EndOffsetInBytes)
                : wholeGroup.EndOffsetInUtf16;
            var wholeValue = context is null ? PyString.FromString(wholeGroup.ValueText) : CreateString(wholeGroup.ValueText, context, span);

            ReCapture?[] captures;
            if (match.CaptureSlotCount <= 1)
            {
                captures = [];
            }
            else
            {
                captures = new ReCapture?[match.CaptureSlotCount - 1];
            }

            for (var i = 1; i < match.CaptureSlotCount; i++)
            {
                if (!match.TryGetGroup(i, out var group) || !group.Success)
                {
                    continue;
                }

                var start = group.HasContiguousByteRange
                    ? range.Pos + range.Segment.ByteIndexToRuneIndex(group.StartOffsetInBytes)
                    : group.StartOffsetInUtf16;
                var end = group.HasContiguousByteRange
                    ? range.Pos + range.Segment.ByteIndexToRuneIndex(group.EndOffsetInBytes)
                    : group.EndOffsetInUtf16;

                captures[i - 1] = new ReCapture(
                    context is null ? PyString.FromString(group.ValueText) : CreateString(group.ValueText, context, span),
                    new BigInteger(start),
                    new BigInteger(end));
            }

            Dictionary<string, int>? namedGroups = null;
            foreach (var entry in match.NameEntries)
            {
                namedGroups ??= new Dictionary<string, int>(StringComparer.Ordinal);
                namedGroups[entry.Name] = entry.Number;
            }

            return new ReMatchObject(
                wholeValue,
                new BigInteger(wholeStart),
                new BigInteger(wholeEnd),
                pattern,
                range.Original,
                new BigInteger(range.Pos),
                new BigInteger(range.EndPos),
                match.CaptureSlotCount,
                captures,
                namedGroups ?? pattern.NamedGroups);
        }

        internal static PyList ProjectSplitResult(Utf8PythonSplitItem[] parts, LythonSourceSpan span, ExecutionContext context)
        {
            var items = new object[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                context.CheckExecutionBudget(span);
                var part = parts[i];
                items[i] = part.ValueText is null ? PyNone.Instance : CreateString(part.ValueText, context, span);
            }

            return new PyList(items, context.MemoryGovernor, span);
        }

    }

    internal sealed class PyRegexFindIterator : PyIteratorBase
    {
        private readonly RePatternObject _pattern;
        private readonly RegexSubjectRange _range;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly System.Collections.IEnumerator? _matches;

        public PyRegexFindIterator(RePatternObject pattern, RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            _pattern = pattern;
            _range = range;
            _context = context;
            _span = span;
            _matches = ReModule.CreateDetailedFindMatches(pattern, range).GetEnumerator();
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (_matches is null || !_matches.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _context.CheckExecutionBudget(_span);
            value = ReModule.CreateMatchObject(_pattern, _range, (Utf8PythonDetailedMatchData)_matches.Current.RequireNotNull(), _context, _span);
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<callable_iterator object>");
        }
    }

    internal static class ReMatchMembers
    {
        public static bool TryGetMember(ReMatchObject match, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "re" => match.Pattern,
                "string" => match.String,
                "pos" => match.Pos,
                "endpos" => match.EndPos,
                "lastindex" => LastIndex(match),
                "lastgroup" => LastGroup(match),
                "group" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return match.Value;
                    }

                    if (arguments.Length == 1)
                    {
                        return arguments[0] switch
                        {
                            BigInteger integer => ResolveIndexedGroup(match, (int)integer, span),
                            int integer => ResolveIndexedGroup(match, integer, span),
                            _ when PyStringOps.TryAsString(arguments[0], out var nameText) => ResolveNamedGroup(match, nameText.AsString(), span),
                            _ => throw new LythonRuntimeException("TypeError", "match.group(index) expects an integer or group name.", span)
                        };
                    }

                    var groups = new object[arguments.Length];
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        var argument = arguments[i];
                        groups[i] = argument switch
                        {
                            BigInteger integer => ResolveIndexedGroup(match, (int)integer, span),
                            int integer => ResolveIndexedGroup(match, integer, span),
                            _ when PyStringOps.TryAsString(argument, out var nameText) => ResolveNamedGroup(match, nameText.AsString(), span),
                            _ => throw new LythonRuntimeException("TypeError", "match.group(index) expects an integer or group name.", span)
                        };
                    }

                    return new PyTuple(groups, context.MemoryGovernor, span);
                }),
                "groups" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.groups(default=None) expects zero or one argument.", span);
                    }

                    var defaultValue = arguments.Length == 1 ? arguments[0] : PyNone.Instance;
                    var groups = new object[Math.Max(0, match.CaptureSlotCount - 1)];
                    for (var i = 1; i < match.CaptureSlotCount; i++)
                    {
                        groups[i - 1] = ResolveIndexedGroup(match, i, span, defaultValue);
                    }

                    return new PyTuple(groups, context.MemoryGovernor, span);
                }, "match.groups", ["default"], 0),
                "groupdict" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.groupdict(default=None) expects zero or one argument.", span);
                    }

                    var defaultValue = arguments.Length == 1 ? arguments[0] : PyNone.Instance;
                    var dict = new PyDict(context.MemoryGovernor, span);
                    foreach (var entry in match.NamedGroups.OrderBy(entry => entry.Value))
                    {
                        dict.SetItem(PyString.FromString(entry.Key, context.MemoryGovernor, span), ResolveIndexedGroup(match, entry.Value, span, defaultValue));
                    }

                    return dict;
                }, "match.groupdict", ["default"], 0),
                "expand" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var template))
                    {
                        throw new LythonRuntimeException("TypeError", "match.expand(template) expects one string argument.", span);
                    }

                    return ExpandReplacementTemplate(match, template, context, span);
                }, "match.expand", ["template"]),
                "start" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.start(group=0) expects zero or one group identifier.", span);
                    }

                    return ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span).Start;
                }, "match.start", ["group"], 0),
                "end" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.end(group=0) expects zero or one group identifier.", span);
                    }

                    return ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span).End;
                }, "match.end", ["group"], 0),
                "span" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.span(group=0) expects zero or one group identifier.", span);
                    }

                    var bounds = ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span);
                    return CreateTuple(2, i => i == 0 ? bounds.Start : bounds.End, context, span);
                }, "match.span", ["group"], 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span)
            => ResolveIndexedGroup(match, index, span, PyNone.Instance);

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span, object defaultValue)
        {
            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            if (index == 0)
            {
                return match.Value;
            }

            return (object?)match.Captures[index - 1]?.Value ?? defaultValue;
        }

        private static object ResolveNamedGroup(ReMatchObject match, string groupName, LythonSourceSpan span)
        {
            if (!match.NamedGroups.TryGetValue(groupName, out var number))
            {
                throw new LythonRuntimeException("IndexError", $"Regex group '{groupName}' is not defined.", span);
            }

            return ResolveIndexedGroup(match, number, span);
        }

        private static (BigInteger Start, BigInteger End) ResolveGroupBounds(ReMatchObject match, object group, LythonSourceSpan span)
        {
            if (TryResolveGroupIndex(match, group, span, out var index))
            {
                if (index == 0)
                {
                    return (match.Start, match.End);
                }

                var capture = match.Captures[index - 1];
                return capture is null
                    ? (new BigInteger(-1), new BigInteger(-1))
                    : (capture.Start, capture.End);
            }

            throw new LythonRuntimeException("TypeError", "Regex group identifier must be an integer or group name.", span);
        }

        private static bool TryResolveGroupIndex(ReMatchObject match, object group, LythonSourceSpan span, out int index)
        {
            index = group switch
            {
                BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                    ? throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span)
                    : (int)integer,
                int integer => integer,
                _ when PyStringOps.TryAsString(group, out var nameText) => match.NamedGroups.TryGetValue(nameText.AsString(), out var namedIndex)
                    ? namedIndex
                    : throw new LythonRuntimeException("IndexError", $"Regex group '{nameText.AsString()}' is not defined.", span),
                _ => int.MinValue
            };

            if (index == int.MinValue)
            {
                return false;
            }

            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            return true;
        }

        private static object LastIndex(ReMatchObject match)
        {
            for (var i = match.Captures.Count - 1; i >= 0; i--)
            {
                if (match.Captures[i] is not null)
                {
                    return new BigInteger(i + 1);
                }
            }

            return PyNone.Instance;
        }

        private static object LastGroup(ReMatchObject match)
        {
            var lastIndex = LastIndex(match);
            if (lastIndex is not BigInteger index)
            {
                return PyNone.Instance;
            }

            foreach (var entry in match.NamedGroups)
            {
                if (entry.Value == (int)index)
                {
                    return PyString.FromString(entry.Key);
                }
            }

            return PyNone.Instance;
        }

        internal static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template, ExecutionContext context, LythonSourceSpan span)
        {
            var text = template.AsString();
            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (++i >= text.Length)
                {
                    builder.Append('\\');
                    break;
                }

                var escaped = text[i];
                if (char.IsDigit(escaped))
                {
                    var start = i;
                    while (i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        i++;
                    }

                    AppendExpandedGroup(match, text[start..(i + 1)], builder, span);
                    continue;
                }

                if (escaped == 'g' && i + 1 < text.Length && text[i + 1] == '<')
                {
                    var end = text.IndexOf('>', i + 2);
                    if (end < 0)
                    {
                        throw new LythonRuntimeException("error", "missing > in regex replacement group reference.", span);
                    }

                    AppendExpandedGroup(match, text[(i + 2)..end], builder, span);
                    i = end;
                    continue;
                }

                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'f' => '\f',
                    'v' => '\v',
                    'a' => '\a',
                    'b' => '\b',
                    _ => escaped
                });
            }

            return CreateString(builder.ToString(), context, span);
        }

        private static void AppendExpandedGroup(ReMatchObject match, string reference, StringBuilder builder, LythonSourceSpan span)
        {
            object group = int.TryParse(reference, out var index)
                ? new BigInteger(index)
                : PyString.FromString(reference);

            if (!TryResolveGroupIndex(match, group, span, out var groupIndex))
            {
                throw new LythonRuntimeException("error", "invalid regex replacement group reference.", span);
            }

            var value = ResolveIndexedGroup(match, groupIndex, span, PyString.Empty);
            if (PyStringOps.TryAsString(value, out var groupText))
            {
                builder.Append(groupText.AsString());
            }
        }
    }

    internal static class RePatternMembers
    {
        public static bool TryGetMember(RePatternObject pattern, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "pattern" => pattern.Pattern,
                "flags" => new BigInteger(pattern.Flags),
                "groups" => new BigInteger(Math.Max(0, pattern.CaptureSlotCount - 1)),
                "groupindex" => CreateGroupIndex(pattern),
                "search" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.SearchDetailedData(input)), "pattern.search", ["string", "pos", "endpos"], 1),
                "match" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.MatchDetailedData(input)), "pattern.match", ["string", "pos", "endpos"], 1),
                "fullmatch" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.FullMatchDetailedData(input)), "pattern.fullmatch", ["string", "pos", "endpos"], 1),
                "findall" => new BoundCallable((arguments, span, context) => ExecuteFindAll(pattern, arguments, span, context), "pattern.findall", ["string", "pos", "endpos"], 1),
                "finditer" => new BoundCallable((arguments, span, context) => ExecuteFindIter(pattern, arguments, span, context), "pattern.finditer", ["string", "pos", "endpos"], 1),
                "sub" => new BoundCallable((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, includeCount: false), "pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2),
                "subn" => new BoundCallable((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, includeCount: true), "pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2),
                "split" => new BoundCallable((arguments, span, context) => ExecuteSplit(pattern, arguments, span, context), "pattern.split", ["string", "maxsplit", "pos", "endpos"], 1),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyDict CreateGroupIndex(RePatternObject pattern)
        {
            var dict = new PyDict();
            foreach (var entry in pattern.NamedGroups.OrderBy(entry => entry.Value))
            {
                dict.SetItem(PyString.FromString(entry.Key), new BigInteger(entry.Value));
            }

            return dict;
        }

        private static object ExecuteMatch(
            RePatternObject pattern,
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            Func<Utf8PythonRegex, ReadOnlySpan<byte>, Utf8PythonDetailedMatchData> operation)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Compiled regex method expects string, optional pos, and optional endpos.", span);
            }

            var range = ReModule.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? ReModule.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "compiled regex method", span) : 0,
                arguments.Length >= 3 ? ReModule.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "compiled regex method", span) : text.Length);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = operation(pattern.Regex, range.Segment.Utf8Bytes.Span);
            return match.Success ? ReModule.CreateMatchObject(pattern, range, match, context, span) : PyNone.Instance;
        }

        private static object ExecuteFindAll(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.findall(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = ReModule.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? ReModule.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "pattern.findall", span) : 0,
                arguments.Length >= 3 ? ReModule.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "pattern.findall", span) : text.Length);
            return ReModule.CreateFindAllResult(pattern, range, span, context);
        }

        internal static PyList ProjectFindAllResult(Utf8PythonFindAllUtf8Result result, LythonSourceSpan span, ExecutionContext context)
        {
            switch (result.Shape)
            {
                case Utf8PythonFindAllShape.FullMatch:
                case Utf8PythonFindAllShape.SingleGroup:
                    var scalars = new object[result.ScalarValues.Length];
                    for (var i = 0; i < scalars.Length; i++)
                    {
                        context.CheckExecutionBudget(span);
                        scalars[i] = CreateUtf8String(result.ScalarValues[i], context, span);
                    }
                    return new PyList(scalars, context.MemoryGovernor, span);

                case Utf8PythonFindAllShape.GroupTuple:
                    var tuples = new object[result.TupleValues.Length];
                    for (var tupleIndex = 0; tupleIndex < tuples.Length; tupleIndex++)
                    {
                        context.CheckExecutionBudget(span);
                        var tuple = result.TupleValues[tupleIndex];
                        var items = new object[tuple.Length];
                        for (var i = 0; i < tuple.Length; i++)
                        {
                            items[i] = CreateUtf8String(tuple[i], context, span);
                        }

                        tuples[tupleIndex] = new PyTuple(items, context.MemoryGovernor, span);
                    }
                    return new PyList(tuples, context.MemoryGovernor, span);

                default:
                    throw new LythonRuntimeException("RuntimeError", "Unsupported regex findall result shape.", span);
            }
        }

        private static object ExecuteFindIter(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.finditer(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = ReModule.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? ReModule.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "pattern.finditer", span) : 0,
                arguments.Length >= 3 ? ReModule.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "pattern.finditer", span) : text.Length);
            return ReModule.CreateFindIterMatches(pattern, range, context, span);
        }

        private static object ExecuteSub(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context, bool includeCount)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 2 or > 5 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "pattern.subn(replacement, string[, count[, pos[, endpos]]]) expects replacement, text string, and optional count/pos/endpos."
                    : "pattern.sub(replacement, string[, count[, pos[, endpos]]]) expects replacement, text string, and optional count/pos/endpos.", span);
            }

            var replacement = arguments[0];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "pattern.subn(...) replacement must be a string or callable."
                    : "pattern.sub(...) replacement must be a string or callable.", span);
            }

            var count = arguments.Length >= 3 ? ReModule.ParseOptionalIntOrDefault(arguments[2], 0, "count", includeCount ? "pattern.subn" : "pattern.sub", span) : 0;
            var range = ReModule.CreateSubjectRange(
                text,
                arguments.Length >= 4 ? ReModule.ParseOptionalIntOrDefault(arguments[3], 0, "pos", includeCount ? "pattern.subn" : "pattern.sub", span) : 0,
                arguments.Length >= 5 ? ReModule.ParseOptionalIntOrDefault(arguments[4], text.Length, "endpos", includeCount ? "pattern.subn" : "pattern.sub", span) : text.Length);
            return ReModule.ExecuteSubstitute(pattern, replacement, range, count, span, context, includeCount);
        }

        private static object ExecuteSplit(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects a string and optional maxsplit/pos/endpos.", span);
            }

            var maxSplit = arguments.Length >= 2 ? ReModule.ParseOptionalIntOrDefault(arguments[1], 0, "maxsplit", "pattern.split", span) : 0;
            var range = ReModule.CreateSubjectRange(
                text,
                arguments.Length >= 3 ? ReModule.ParseOptionalIntOrDefault(arguments[2], 0, "pos", "pattern.split", span) : 0,
                arguments.Length >= 4 ? ReModule.ParseOptionalIntOrDefault(arguments[3], text.Length, "endpos", "pattern.split", span) : text.Length);
            return ReModule.ProjectSplitResult(pattern.Regex.SplitDetailed(range.Segment.Utf8Bytes.Span, maxSplit), span, context);
        }
    }

}
