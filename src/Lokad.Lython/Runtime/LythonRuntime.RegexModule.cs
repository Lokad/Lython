using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal readonly record struct ReCaptureRange(int StartByte, int EndByte, int Start, int End);

    private static PyString MaterializeRegexRange(PyString subject, int start, int end, MemoryGovernor governor,
        ExecutionContext? context, LythonSourceSpan? span)
    {
        governor.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        if (start < 0 || start == end) return PyString.Empty;
        var length = end - start;
        if (length == subject.ByteLength) return subject;
        return subject.CopyOrBorrowUtf8Range(start, length, governor, span, context);
    }

    // Retained matches own immutable ranges over their already-funded subject.
    // Current group access owns any new string, independently of cursor scratch.
    internal sealed class ReMatchObject(RePatternObject pattern, PyString input, int pos, int endPos,
        ReCaptureRange[] captures, int? lastIndex, MemoryGovernor owner) : IPyContextualDynamicAttributes, IPyHashableValue
    {
        private PyTuple? _registers;

        public RePatternObject Pattern { get; } = pattern;
        public PyString String { get; } = input;
        public int Pos { get; } = pos;
        public int EndPos { get; } = endPos;
        public ReCaptureRange[] Captures { get; } = captures;
        public int CaptureSlotCount => Captures.Length;
        public PyDict NamedGroups => Pattern.NamedGroups;
        public int? LastCapturedIndex { get; } = lastIndex;
        public int Start => Captures[0].Start;
        public int End => Captures[0].End;

        public int GetPyHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => ReMatchMembers.TryGetMember(this, name, context, span, out value);

        internal PyTuple ReadRegisters(ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (_registers is { } cached) return cached;
            var projection = CreateTuple(Captures.Length, i =>
            {
                var capture = Captures[i];
                return CreateTuple(2, j => new BigInteger(j == 0 ? capture.Start : capture.End), context, span);
            }, context, span);
            // The immutable tuple and its contents stay funded by execution.
            // Publish it only after every coordinate has been materialized.
            return _registers ??= projection;
        }

        internal ReadOnlySpan<byte> GroupBytes(int index)
        {
            var capture = Captures[index];
            return capture.StartByte < 0 ? default
                : String.Utf8Span.Slice(capture.StartByte, capture.EndByte - capture.StartByte);
        }

        internal PyString MaterializeGroup(int index, ExecutionContext? context, LythonSourceSpan? span)
        {
            context ??= PyStructuralGuard.AmbientContext;
            var capture = Captures[index];
            return MaterializeRegexRange(String, capture.StartByte, capture.EndByte,
                context?.MemoryGovernor ?? owner, context, span);
        }
    }

    internal sealed class RePatternObject(
        PyString pattern, int flags, ILythonRegexPattern regex, int captureSlotCount, PyDict namedGroups,
        LythonRegexOptions options)
        : IPyRenderableValue, IPyContextualDynamicAttributes, IPyHashableValue
    {
        public PyString Pattern { get; } = pattern;
        public int Flags { get; } = flags;
        public ILythonRegexPattern Regex { get; } = regex;
        public int CaptureSlotCount { get; } = captureSlotCount;
        public PyDict NamedGroups { get; } = namedGroups;
        private LythonRegexOptions Options { get; } = options;

        // Pattern creation already hashed the text under the active token.
        public int GetPyHashCode() => HashCode.Combine(Pattern.GetHashCode(), Flags);

        internal bool HasSameProgram(RePatternObject other, ExecutionContext? context, LythonSourceSpan? span)
        {
            context?.CheckExecution(span);
            if (ReferenceEquals(this, other)) return true;
            if (Flags != other.Flags || !Pattern.Equals(other.Pattern, context, span)) return false;
            // Unicode provenance only affects validation. Both handles already
            // represent successful compilations of the same original pattern.
            if (Options.Flags == other.Options.Flags && Options.Verbose == other.Options.Verbose ||
                ReferenceEquals(Regex, other.Regex)) return true;
            return RegexCompiler.ProviderCall(() => CompareProgramKeys(other, context, span),
                context?.MemoryGovernor, context?.State.CancellationToken ?? default, span, Pattern);
        }

        private bool CompareProgramKeys(RePatternObject other, ExecutionContext? context, LythonSourceSpan? span)
        {
            if (Regex.ProgramEqualityKey is not { } left || other.Regex.ProgramEqualityKey is not { } right)
                throw RuntimeErrors.Capability("Regex provider does not supply compiled program equality for these options.", span);
            if (left.Length != right.Length) return false;

            var a = left.Span;
            var b = right.Span;
            for (var i = 0; i < a.Length;)
            {
                context?.CheckExecution(span);
                var count = Math.Min(4096, a.Length - i);
                if (!a.Slice(i, count).SequenceEqual(b.Slice(i, count))) return false;
                i += count;
            }
            context?.CheckExecution(span);
            return true;
        }

        private static readonly (int Mask, string Name)[] DisplayFlags =
        [
            (RegexCompiler.PythonIgnoreCaseFlag, "re.IGNORECASE"),
            (RegexCompiler.PythonLocaleFlag, "re.LOCALE"),
            (RegexCompiler.PythonMultilineFlag, "re.MULTILINE"),
            (RegexCompiler.PythonDotAllFlag, "re.DOTALL"),
            (RegexCompiler.PythonUnicodeFlag, "re.UNICODE"),
            (RegexCompiler.PythonVerboseFlag, "re.VERBOSE"),
            (RegexCompiler.RegexDebugFlag, "re.DEBUG"),
            (RegexCompiler.PythonAsciiFlag, "re.ASCII"),
        ];

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => RePatternMembers.TryGetMember(this, name, context, span, out value);

        internal bool TryGetNamedGroup(PyString name, ExecutionContext? context, LythonSourceSpan span, out int index)
        {
            var found = context is null ? NamedGroups.TryGetValue(name, out var value)
                : NamedGroups.TryGetValue(name, out value, context, span);
            index = found ? (int)(BigInteger)value! : -1;
            return found;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var execution = context.Context;
            var span = PyStructuralGuard.AmbientSpan;
            var builder = new GovernedByteBuilder(execution.MemoryGovernor, span);
            try
            {
                builder.AppendAscii("re.compile(");
                PyRendering.AppendStringLiteral(builder, Pattern.Utf8Span, execution, span, maximumCharacters: 200);
                const int characterFlags = RegexCompiler.PythonLocaleFlag | RegexCompiler.PythonUnicodeFlag | RegexCompiler.PythonAsciiFlag;
                var flags = Flags;
                if ((flags & characterFlags) == RegexCompiler.PythonUnicodeFlag)
                    flags &= ~RegexCompiler.PythonUnicodeFlag;
                var separator = ", ";
                foreach (var (mask, name) in DisplayFlags)
                {
                    if ((flags & mask) == 0) continue;
                    builder.AppendAscii(separator);
                    builder.AppendAscii(name);
                    separator = "|";
                    flags &= ~mask;
                }
                if (flags != 0)
                {
                    builder.AppendAscii(separator);
                    builder.AppendAscii("0x");
                    Span<byte> digits = stackalloc byte[8];
                    unchecked((uint)flags).TryFormat(digits, out var written, "x", System.Globalization.CultureInfo.InvariantCulture);
                    builder.Append(digits[..written]);
                }
                builder.Append((byte)')');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
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
                "search" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReSearch, Search),
                "match" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReMatch, Match),
                "fullmatch" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReFullMatch, FullMatch),
                "findall" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReFindAll, FindAll),
                "finditer" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReFindIter, FindIter),
                "sub" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReSub, Substitute, (arguments, span, context) => ExecuteSubstituteCoreAsync(arguments, span, context, RegexSubstitutionMode.TextOnly, true)),
                "subn" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReSubn, SubstituteCount, (arguments, span, context) => ExecuteSubstituteCoreAsync(arguments, span, context, RegexSubstitutionMode.TextAndCount, true)),
                "split" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.ReSplit, Split),
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
                "Pattern" => PyType.RegexPatternType,
                "Match" => PyType.RegexMatchType,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Compile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            return RegexCompiler.CreatePattern(arguments, "re.compile(pattern[, flags])", span, context);
        }

        private object Search(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, LythonRegexMatchMode.Search);

        private object Match(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, LythonRegexMatchMode.Match);

        private object FullMatch(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteMatch(arguments, span, context, LythonRegexMatchMode.FullMatch);

        private object FindAll(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.findall(pattern, string[, flags][, pos][, endpos])", span, context);
            return RegexMatcher.CreateFindAllResult(inputs.Pattern, inputs.Range, span, context);
        }

        private object FindIter(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var inputs = RegexCompiler.CreatePatternAndRange(arguments, "re.finditer(pattern, string[, flags][, pos][, endpos])", span, context);
            return PyRegexFindIterator.Create(inputs.Pattern, inputs.Range, context, span);
        }

        private object Substitute(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextOnly);

        private object SubstituteCount(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => ExecuteSubstitute(arguments, span, context, RegexSubstitutionMode.TextAndCount);

        private object Split(BoundBuiltinArguments arguments, LythonSourceSpan span, ExecutionContext context)
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
            BoundBuiltinArguments arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            LythonRegexMatchMode mode)
        {
            context.CheckExecution(span);
            var operationName = mode switch
            {
                LythonRegexMatchMode.Search => "search",
                LythonRegexMatchMode.Match => "match",
                LythonRegexMatchMode.FullMatch => "fullmatch",
                _ => throw new InvalidOperationException(),
            };
            var inputs = RegexCompiler.CreatePatternAndRange(
                arguments,
                $"re.{operationName}(pattern, string[, flags][, pos][, endpos])",
                span,
                context);
            return RegexMatcher.Match(inputs.Pattern, inputs.Range, mode, span, context);
        }

        private static object ExecuteSubstitute(
            BoundBuiltinArguments arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexSubstitutionMode mode)
            => ExecuteSubstituteCoreAsync(arguments, span, context, mode, false).GetAwaiter().GetResult();

        private static ValueTask<object> ExecuteSubstituteCoreAsync(BoundBuiltinArguments arguments, LythonSourceSpan span,
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
