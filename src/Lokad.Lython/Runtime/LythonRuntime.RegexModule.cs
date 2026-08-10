using System.Numerics;
using Lokad.Lython.Frontend;
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
                "compile" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReCompile, Compile),
                "search" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSearch, Search),
                "match" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReMatch, Match),
                "fullmatch" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReFullMatch, FullMatch),
                "findall" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReFindAll, FindAll),
                "finditer" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReFindIter, FindIter),
                "sub" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSub, Substitute),
                "subn" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSubn, SubstituteCount),
                "split" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSplit, Split),
                "escape" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReEscape, Escape),
                "purge" => BuiltinCallable.Create(LythonKnownCallableSignatures.RePurge, Purge),
                "error" => new ExceptionTypeValue("error"),
                "PatternError" => new ExceptionTypeValue("PatternError"),
                "RegexFlag" => new RegexFlagFactory(),
                "NOFLAG" => BigInteger.Zero,
                "IGNORECASE" or "I" => new BigInteger(RegexCompiler.PythonIgnoreCaseFlag),
                "UNICODE" or "U" => new BigInteger(RegexCompiler.PythonUnicodeFlag),
                "MULTILINE" or "M" => new BigInteger(RegexCompiler.PythonMultilineFlag),
                "DOTALL" or "S" => new BigInteger(RegexCompiler.PythonDotAllFlag),
                "VERBOSE" or "X" => new BigInteger(RegexCompiler.PythonVerboseFlag),
                "ASCII" or "A" => new BigInteger(RegexCompiler.PythonAsciiFlag),
                "LOCALE" or "L" => new BigInteger(RegexCompiler.PythonLocaleFlag),
                "DEBUG" => new BigInteger(RegexCompiler.RegexDebugFlag),
                "Pattern" => PyString.FromString("re.Pattern"),
                "Match" => PyString.FromString("re.Match"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Compile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return RegexCompiler.CreatePattern(arguments, "re.compile(pattern[, flags])", span);
        }

        private object Search(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.Search);

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.Match);

        private object FullMatch(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.FullMatch);

        private object FindAll(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.findall(pattern, string[, flags][, pos][, endpos])", span);
            return RegexMatcher.CreateFindAllResult(inputs.Pattern, inputs.Range, span, context);
        }

        private object FindIter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.finditer(pattern, string[, flags][, pos][, endpos])", span);
            return RegexMatcher.CreateFindIterMatches(inputs.Pattern, inputs.Range, context, span);
        }

        private object Substitute(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextOnly);

        private object SubstituteCount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextAndCount);

        private object Split(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var inputs = RegexCompiler.CreateSplitInputs(arguments, "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos])", span);
            return RegexMatcher.ProjectSplitResult(inputs.Pattern.Regex.SplitDetailed(inputs.Range.Segment.Utf8Bytes.Span, inputs.MaxSplit), span, context);
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

        private static object ExecuteMatch(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexMatchMode mode)
        {
            context.CheckExecutionBudget(span);
            var operationName = mode switch
            {
                RegexMatchMode.Search => "search",
                RegexMatchMode.Match => "match",
                RegexMatchMode.FullMatch => "fullmatch",
                _ => throw new InvalidOperationException(),
            };
            var inputs = RegexCompiler.CreatePatternAndRange(
                arguments,
                $"re.{operationName}(pattern, string[, flags][, pos][, endpos])",
                span);
            if (!inputs.Range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = mode switch
            {
                RegexMatchMode.Search => inputs.Pattern.Regex.SearchDetailedData(inputs.Range.Segment.Utf8Bytes.Span),
                RegexMatchMode.Match => inputs.Pattern.Regex.MatchDetailedData(inputs.Range.Segment.Utf8Bytes.Span),
                RegexMatchMode.FullMatch => inputs.Pattern.Regex.FullMatchDetailedData(inputs.Range.Segment.Utf8Bytes.Span),
                _ => throw new InvalidOperationException(),
            };
            return match.Success
                ? RegexMatcher.CreateMatchObject(inputs.Pattern, inputs.Range, match, context, span)
                : PyNone.Instance;
        }

        private static object ExecuteSubstitute(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexSubstitutionMode mode)
        {
            context.CheckExecutionBudget(span);
            var operationName = mode == RegexSubstitutionMode.TextAndCount ? "subn" : "sub";
            var inputs = RegexCompiler.CreateSubstituteInputs(
                arguments,
                $"re.{operationName}(pattern, replacement, string[, count][, flags][, pos][, endpos])",
                span);
            return RegexMatcher.ExecuteSubstitute(
                inputs.Pattern,
                inputs.Replacement,
                inputs.Range,
                inputs.Count,
                span,
                context,
                mode);
        }

        private enum RegexMatchMode
        {
            Search,
            Match,
            FullMatch,
        }

        private sealed class RegexFlagFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
        {
            public string Name => "re.RegexFlag";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var signature = LythonCallableSignature.Create("re.RegexFlag", ["value"], RequiredCount: 0);
                var bound = CallBinder.BindNamedArguments(arguments, span, signature, PythonCallableKind.Builtin);
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
    }
}
