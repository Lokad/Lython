using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal readonly record struct ReCaptureRange(int StartByte, int EndByte, int Start, int End);

    // Retained matches own immutable ranges over their already-funded subject.
    // Current group access owns any new string, independently of cursor scratch.
    internal sealed class ReMatchObject(RePatternObject pattern, PyString input, int pos, int endPos,
        ReCaptureRange[] captures, int? lastIndex, MemoryGovernor owner) : IPyContextualDynamicAttributes
    {
        public RePatternObject Pattern { get; } = pattern;
        public PyString String { get; } = input;
        public int Pos { get; } = pos;
        public int EndPos { get; } = endPos;
        public ReCaptureRange[] Captures { get; } = captures;
        public int CaptureSlotCount => Captures.Length;
        public IReadOnlyDictionary<PyString, int> NamedGroups => Pattern.NamedGroups;
        public int? LastCapturedIndex { get; } = lastIndex;
        public int Start => Captures[0].Start;
        public int End => Captures[0].End;

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => ReMatchMembers.TryGetMember(this, name, context, span, out value);

        internal ReadOnlyMemory<byte> GroupBytes(int index)
        {
            var capture = Captures[index];
            return capture.StartByte < 0 ? ReadOnlyMemory<byte>.Empty
                : String.Utf8Bytes.Slice(capture.StartByte, capture.EndByte - capture.StartByte);
        }

        internal PyString MaterializeGroup(int index, ExecutionContext? context, LythonSourceSpan? span)
        {
            context ??= PyStructuralGuard.AmbientContext;
            var governor = context?.MemoryGovernor ?? owner;
            governor.ThrowIfAdmissionFailed();
            context?.CheckExecution(span);
            var bytes = GroupBytes(index);
            if (bytes.IsEmpty) return PyString.Empty;
            if (bytes.Length == String.ByteLength) return String;
            using var storage = governor.ReserveTemporary(PyString.EstimateApproximateBytes(bytes.Length), span);
            context?.CheckExecution(span);
            var copy = new byte[bytes.Length];
            for (var offset = 0; offset < copy.Length;)
            {
                context?.CheckExecution(span);
                var count = Math.Min(4096, copy.Length - offset);
                bytes.Span.Slice(offset, count).CopyTo(copy.AsSpan(offset, count));
                offset += count;
            }
            context?.CheckExecution(span);
            return PyString.FromOwnedUtf8(copy, governor, span, storage);
        }
    }

    internal sealed record RePatternObject(
        PyString Pattern,
        int Flags,
        ILythonRegexPattern Regex,
        int CaptureSlotCount,
        IReadOnlyDictionary<PyString, int> NamedGroups) : IPyRenderableValue
    {
        public PyString RenderPython(PyRenderingContext context)
        {
            var renderedPattern = PyRendering.ToReprPyString(Pattern, context).AsString();
            var rendered = Flags == RegexCompiler.PythonUnicodeFlag
                ? $"re.compile({renderedPattern})"
                : $"re.compile({renderedPattern}, {Flags})";
            return PyString.FromString(rendered);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal readonly record struct RegexSubjectRange(
        PyString Original,
        int Pos,
        int EndPos)
    {
        public bool IsValid => Pos <= EndPos;
    }

    private sealed class ReModule : PyModule
    {
        public static readonly ReModule Instance = new();
        internal static readonly ExceptionTypeValue PatternErrorType = new(ModuleException("re", "PatternError"));

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
                "sub" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSub, Substitute, (arguments, span, context) => ExecuteSubstituteCoreAsync(arguments, span, context, RegexSubstitutionMode.TextOnly, true)),
                "subn" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSubn, SubstituteCount, (arguments, span, context) => ExecuteSubstituteCoreAsync(arguments, span, context, RegexSubstitutionMode.TextAndCount, true)),
                "split" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReSplit, Split),
                "escape" => BuiltinCallable.Create(LythonKnownCallableSignatures.ReEscape, Escape),
                "purge" => BuiltinCallable.Create(LythonKnownCallableSignatures.RePurge, Purge),
                "error" or "PatternError" => PatternErrorType,
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
            context.CheckExecution(span);
            return RegexCompiler.CreatePattern(arguments, "re.compile(pattern[, flags])", span, context);
        }

        private object Search(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.Search);

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.Match);

        private object FullMatch(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, RegexMatchMode.FullMatch);

        private object FindAll(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.findall(pattern, string[, flags][, pos][, endpos])", span, context);
            return RegexMatcher.CreateFindAllResult(inputs.Pattern, inputs.Range, span, context);
        }

        private object FindIter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.finditer(pattern, string[, flags][, pos][, endpos])", span, context);
            // Finditer shells charge per live instance like other iterator factories.
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var findIter = RegexMatcher.CreateFindIterMatches(inputs.Pattern, inputs.Range, context, span);
            return findIter;
        }

        private object Substitute(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextOnly);

        private object SubstituteCount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextAndCount);

        private object Split(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var inputs = RegexCompiler.CreateSplitInputs(arguments, "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos])", span, context);
            return RegexMatcher.Split(inputs.Pattern, inputs.Range, inputs.MaxSplit, span, context);
        }

        private object Escape(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "re.escape(string) expects one string argument.", span);
            }

            return PyStringOps.EscapeRegex(text);
        }

        private object Purge(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "re.purge() expects no arguments.", span);
            }

            // N19: drops every cached reference and releases slot charges. Retained
            // patterns and matches keep working on their own ownership, so a later
            // compile of the same pattern simply builds (and caches) anew.
            context.State.RegexCache?.Clear(context.MemoryGovernor);
            return PyNone.Instance;
        }

        private static object ExecuteMatch(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexMatchMode mode)
        {
            context.CheckExecution(span);
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
                span,
                context);
            return RegexMatcher.Match(inputs.Pattern, inputs.Range, (LythonRegexMatchMode)mode, span, context);
        }

        private static object ExecuteSubstitute(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexSubstitutionMode mode)
            => ExecuteSubstituteCoreAsync(arguments, span, context, mode, false).GetAwaiter().GetResult();

        private static ValueTask<object> ExecuteSubstituteCoreAsync(object[] arguments, LythonSourceSpan span,
            ExecutionContext context, RegexSubstitutionMode mode, bool asynchronous)
        {
            context.CheckExecution(span);
            var operationName = mode == RegexSubstitutionMode.TextAndCount ? "subn" : "sub";
            var inputs = RegexCompiler.CreateSubstituteInputs(
                arguments,
                $"re.{operationName}(pattern, replacement, string[, count][, flags][, pos][, endpos])",
                span,
                context);
            return asynchronous ? RegexMatcher.ExecuteSubstituteAsync(inputs.Pattern, inputs.Replacement,
                inputs.Range, inputs.Count, span, context, mode)
                : new ValueTask<object>(RegexMatcher.ExecuteSubstitute(inputs.Pattern, inputs.Replacement,
                    inputs.Range, inputs.Count, span, context, mode));
        }

        private enum RegexMatchMode
        {
            Search,
            Match,
            FullMatch,
        }

        internal sealed class RegexFlagFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes
        {
            private static readonly LythonCallableSignature CallSignature = LythonCallableSignature.Create(
                "re.RegexFlag",
                ["value"],
                requiredCount: 0);

            public string Name => "re.RegexFlag";

            // The flag name is fixed vocabulary shared across factory instances, so
            // reads alias stably; there is no Flag base model, so __bases__ stays
            // missing (CPython reports int/Flag there).
            private static readonly PyString RegexFlagName = PyString.FromString("RegexFlag");

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                if (name is "__name__" or "__qualname__")
                {
                    value = RegexFlagName;
                    return true;
                }

                if (name == "__module__")
                {
                    value = ExceptionTypeValue.SharedModuleLabel("re");
                    return true;
                }

                value = PyNone.Instance;
                return false;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecution(span);
                var bound = CallBinder.BindNamedArguments(arguments, span, CallSignature, PythonCallableKind.Builtin);
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
