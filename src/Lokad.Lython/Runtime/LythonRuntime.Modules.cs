using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly IReadOnlyDictionary<string, int> EmptyRegexNamedGroups = new Dictionary<string, int>(StringComparer.Ordinal);

    internal sealed record ReCapture(PyString Value, BigInteger Start, BigInteger End);

    internal sealed record ReMatchObject(
        PyString Value,
        BigInteger Start,
        BigInteger End,
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
        Utf8PythonRegex Regex);

    private sealed class MathModule : PyModule
    {
        public static readonly MathModule Instance = new();

        private MathModule() : base("math")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                "tau" => Math.Tau,
                "inf" => double.PositiveInfinity,
                "nan" => double.NaN,
                "sqrt" => new BuiltinCallable("math.sqrt", Sqrt, ["x"]),
                "exp" => new BuiltinCallable("math.exp", Exp, ["x"]),
                "log" => new BuiltinCallable("math.log", Log, ["x", "base"], requiredCount: 1),
                "log10" => new BuiltinCallable("math.log10", Log10, ["x"]),
                "log2" => new BuiltinCallable("math.log2", Log2, ["x"]),
                "sin" => new BuiltinCallable("math.sin", Sin, ["x"]),
                "cos" => new BuiltinCallable("math.cos", Cos, ["x"]),
                "tan" => new BuiltinCallable("math.tan", Tan, ["x"]),
                "asin" => new BuiltinCallable("math.asin", Asin, ["x"]),
                "acos" => new BuiltinCallable("math.acos", Acos, ["x"]),
                "atan" => new BuiltinCallable("math.atan", Atan, ["x"]),
                "atan2" => new BuiltinCallable("math.atan2", Atan2, ["y", "x"]),
                "sinh" => new BuiltinCallable("math.sinh", Sinh, ["x"]),
                "cosh" => new BuiltinCallable("math.cosh", Cosh, ["x"]),
                "tanh" => new BuiltinCallable("math.tanh", Tanh, ["x"]),
                "floor" => new BuiltinCallable("math.floor", Floor, ["x"]),
                "ceil" => new BuiltinCallable("math.ceil", Ceil, ["x"]),
                "fabs" => new BuiltinCallable("math.fabs", Fabs, ["x"]),
                "trunc" => new BuiltinCallable("math.trunc", Trunc, ["x"]),
                "degrees" => new BuiltinCallable("math.degrees", Degrees, ["x"]),
                "radians" => new BuiltinCallable("math.radians", Radians, ["x"]),
                "isfinite" => new BuiltinCallable("math.isfinite", IsFinite, ["x"]),
                "isinf" => new BuiltinCallable("math.isinf", IsInf, ["x"]),
                "isnan" => new BuiltinCallable("math.isnan", IsNaN, ["x"]),
                "pow" => new BuiltinCallable("math.pow", Pow, ["x", "y"]),
                "hypot" => new BuiltinCallable("math.hypot", Hypot, ["x", "y"]),
                "fmod" => new BuiltinCallable("math.fmod", Fmod, ["x", "y"]),
                "copysign" => new BuiltinCallable("math.copysign", CopySign, ["x", "y"]),
                "isclose" => new BuiltinCallable("math.isclose", IsClose, ["a", "b", "rel_tol", "abs_tol"], requiredCount: 2),
                "prod" => new BuiltinCallable("math.prod", Prod, ["iterable", "start"], requiredCount: 1),
                "fsum" => new BuiltinCallable("math.fsum", Fsum, ["iterable"]),
                _ => null!,
            };

            return value is not null;
        }

        private static object Sqrt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.sqrt", span, context);
            if (value < 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Sqrt(value);
        }

        private static object Exp(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.exp", span, context, Math.Exp);

        private static object Log(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "math.log(x[, base]) expects one or two numeric arguments.", span);
            }

            var x = ExpectReal(arguments[0], "math.log", span);
            if (x <= 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            if (arguments.Length == 1 || ReferenceEquals(arguments[1], PyNone.Instance))
            {
                return Math.Log(x);
            }

            var @base = ExpectReal(arguments[1], "math.log", span);
            if (@base <= 0.0 || @base == 1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Log(x, @base);
        }

        private static object Log10(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.log10", span, context);
            if (value <= 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Log10(value);
        }

        private static object Log2(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.log2", span, context);
            if (value <= 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Log2(value);
        }

        private static object Sin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.sin", span, context, Math.Sin);

        private static object Cos(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.cos", span, context, Math.Cos);

        private static object Tan(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.tan", span, context, Math.Tan);

        private static object Asin(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.asin", span, context);
            if (value is < -1.0 or > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Asin(value);
        }

        private static object Acos(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.acos", span, context);
            if (value is < -1.0 or > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Acos(value);
        }

        private static object Atan(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.atan", span, context, Math.Atan);

        private static object Atan2(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.atan2", span, context, Math.Atan2);

        private static object Sinh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.sinh", span, context, Math.Sinh);

        private static object Cosh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.cosh", span, context, Math.Cosh);

        private static object Tanh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.tanh", span, context, Math.Tanh);

        private static object Floor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.floor(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.floor", span, static x => Math.Floor(x));
        }

        private static object Ceil(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.ceil(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.ceil", span, static x => Math.Ceiling(x));
        }

        private static object Fabs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.fabs", span, context, Math.Abs);

        private static object Trunc(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.trunc(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.trunc", span, static x => Math.Truncate(x));
        }

        private static object Degrees(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.degrees", span, context, static x => x * (180.0 / Math.PI));

        private static object Radians(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.radians", span, context, static x => x * (Math.PI / 180.0));

        private static object IsFinite(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isfinite", span, context, static x => double.IsFinite(x));

        private static object IsInf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isinf", span, context, static x => double.IsInfinity(x));

        private static object IsNaN(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isnan", span, context, static x => double.IsNaN(x));

        private static object Pow(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.pow", span, context, Math.Pow);

        private static object Hypot(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.hypot", span, context, static (x, y) => Math.Sqrt(x * x + y * y));

        private static object Fmod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.fmod", span, context);
            if (y == 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return x % y;
        }

        private static object CopySign(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.copysign", span, context, static (x, y) => Math.Abs(x) * (Math.CopySign(1.0, y)));

        private static object IsClose(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 2 or > 4)
            {
                throw new LythonRuntimeException("TypeError", "math.isclose(a, b[, rel_tol][, abs_tol]) expects two to four numeric arguments.", span);
            }

            var a = ExpectReal(arguments[0], "math.isclose", span);
            var b = ExpectReal(arguments[1], "math.isclose", span);
            var relTol = arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance)
                ? ExpectReal(arguments[2], "math.isclose", span)
                : 1e-09;
            var absTol = arguments.Length >= 4 && !ReferenceEquals(arguments[3], PyNone.Instance)
                ? ExpectReal(arguments[3], "math.isclose", span)
                : 0.0;

            if (relTol < 0.0 || absTol < 0.0)
            {
                throw new LythonRuntimeException("ValueError", "tolerances must be non-negative", span);
            }

            if (a == b)
            {
                return true;
            }

            if (double.IsInfinity(a) || double.IsInfinity(b))
            {
                return false;
            }

            var diff = Math.Abs(b - a);
            return diff <= Math.Max(relTol * Math.Max(Math.Abs(a), Math.Abs(b)), absTol);
        }

        private static object Prod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "math.prod(iterable[, start]) expects one iterable and an optional numeric start.", span);
            }

            object total = arguments.Length == 2 ? ExpectNumericObject(arguments[1], "math.prod", span) : BigInteger.One;
            foreach (var item in ToSequence(arguments[0], span))
            {
                total = MultiplyNumeric(total, item, span);
            }

            return RuntimeValue(total);
        }

        private static object Fsum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.fsum(iterable) expects one iterable.", span);
            }

            decimal finiteTotal = 0m;
            bool sawFinite = false;
            double nonFiniteTotal = 0.0;
            foreach (var item in ToSequence(arguments[0], span))
            {
                var value = ExpectReal(item, "math.fsum", span);
                if (!double.IsFinite(value))
                {
                    nonFiniteTotal += value;
                    continue;
                }

                finiteTotal += (decimal)value;
                sawFinite = true;
            }

            if (!double.IsFinite(nonFiniteTotal))
            {
                return nonFiniteTotal;
            }

            return sawFinite ? (double)finiteTotal : 0.0;
        }

        private static double ExpectUnaryReal(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x) expects one numeric argument.", span);
            }

            return ExpectReal(arguments[0], owner, span);
        }

        private static (double X, double Y) ExpectBinaryReal(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two numeric arguments.", span);
            }

            return (ExpectReal(arguments[0], owner, span), ExpectReal(arguments[1], owner, span));
        }

        private static object UnaryFloat(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double> func)
        {
            var value = ExpectUnaryReal(arguments, owner, span, context);
            return func(value);
        }

        private static object UnaryBool(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, bool> func)
        {
            var value = ExpectUnaryReal(arguments, owner, span, context);
            return func(value);
        }

        private static object BinaryFloat(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double, double> func)
        {
            var (x, y) = ExpectBinaryReal(arguments, owner, span, context);
            return func(x, y);
        }

        private static double ExpectReal(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects real numbers.", span);
            }

            return number.ToDouble();
        }

        private static object ExpectNumericObject(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects real numbers.", span);
            }

            return number.IsFloat ? number.Floating : number.Integer;
        }

        private static object MultiplyNumeric(object lhs, object rhs, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(lhs, out var left) || !PyNumberOps.TryAsNumber(rhs, out var right))
            {
                throw new LythonRuntimeException("TypeError", "math.prod(...) expects an iterable of real numbers.", span);
            }

            return PyNumberOps.Multiply(left, right);
        }

        private static object ExpectFloorLike(object value, string owner, LythonSourceSpan span, Func<double, double> func)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            if (!number.IsFloat)
            {
                return number.Integer;
            }

            return new BigInteger(func(number.Floating));
        }
    }

    private sealed class DatetimeModule : PyModule
    {
        public static readonly DatetimeModule Instance = new();

        private DatetimeModule() : base("datetime")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "MINYEAR" => BigInteger.One,
                "MAXYEAR" => new BigInteger(9999),
                "timedelta" => PyDateTimeOps.TimedeltaType,
                "date" => PyDateTimeOps.DateType,
                "time" => PyDateTimeOps.TimeType,
                "datetime" => PyDateTimeOps.DateTimeType,
                "timezone" => PyDateTimeOps.TimezoneType,
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed class ReModule : PyModule
    {
        public static readonly ReModule Instance = new();

        private ReModule() : base("re")
        {
        }

        public override bool TryGetMember(string name, out object value)
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
                "IGNORECASE" => new BigInteger((int)PythonReCompileOptions.IgnoreCase),
                "I" => new BigInteger((int)PythonReCompileOptions.IgnoreCase),
                "UNICODE" => BigInteger.Zero,
                "U" => BigInteger.Zero,
                "MULTILINE" => new BigInteger((int)PythonReCompileOptions.Multiline),
                "M" => new BigInteger((int)PythonReCompileOptions.Multiline),
                "DOTALL" => new BigInteger((int)PythonReCompileOptions.DotAll),
                "S" => new BigInteger((int)PythonReCompileOptions.DotAll),
                "VERBOSE" => new BigInteger((int)PythonReCompileOptions.Verbose),
                "X" => new BigInteger((int)PythonReCompileOptions.Verbose),
                _ => null!,
            };

            return value is not null;
        }

        private object Compile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return CreatePattern(arguments, "re.compile(pattern[, flags])", span);
        }

        private object Search(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text) = CreatePatternAndText(arguments, "re.search(pattern, string[, flags])", span);
            var match = pattern.Regex.SearchDetailedData(text.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(text, match, context, span) : PyNone.Instance;
        }

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text) = CreatePatternAndText(arguments, "re.match(pattern, string[, flags])", span);
            var match = pattern.Regex.MatchDetailedData(text.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(text, match, context, span) : PyNone.Instance;
        }

        private object FullMatch(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text) = CreatePatternAndText(arguments, "re.fullmatch(pattern, string[, flags])", span);
            var match = pattern.Regex.FullMatchDetailedData(text.Utf8Bytes.Span);
            return match.Success ? CreateMatchObject(text, match, context, span) : PyNone.Instance;
        }

        private object FindAll(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text) = CreatePatternAndText(arguments, "re.findall(pattern, string[, flags])", span);
            return new ReFindAllResult(RePatternMembers.ProjectFindAllResult(pattern.Regex.FindAllToUtf8(text.Utf8Bytes.Span), span, context));
        }

        private object FindIter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text) = CreatePatternAndText(arguments, "re.finditer(pattern, string[, flags])", span);
            return CreateFindIterMatches(pattern.Regex, text, context, span);
        }

        private object Substitute(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, replacement, text, count) = CreateSubstituteInputs(arguments, "re.sub(pattern, replacement, string[, count][, flags])", span);
            return ExecuteSubstitute(pattern, replacement, text, count, span, context, includeCount: false);
        }

        private object SubstituteCount(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, replacement, text, count) = CreateSubstituteInputs(arguments, "re.subn(pattern, replacement, string[, count][, flags])", span);
            return ExecuteSubstitute(pattern, replacement, text, count, span, context, includeCount: true);
        }

        private object Split(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var (pattern, text, maxSplit) = CreateSplitInputs(arguments, "re.split(pattern, string[, maxsplit][, flags])", span);
            return ProjectSplitResult(pattern.Regex.SplitDetailed(text.Utf8Bytes.Span, maxSplit), span, context);
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

        private static RePatternObject CreatePattern(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects two string arguments when used in module form, with optional flags.", span);
            }

            var options = arguments.Length == 2
                ? ParseFlags(arguments[1], signature, span)
                : PythonReCompileOptions.None;

            try
            {
                return new RePatternObject(pattern, options, new Utf8PythonRegex(pattern.Utf8Bytes.Span, options));
            }
            catch (PythonRePatternException ex)
            {
                throw new LythonRuntimeException("ValueError", ex.Message, span);
            }
        }

        private static (RePatternObject Pattern, PyString Text) CreatePatternAndText(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 3 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects two string arguments (pattern and string), plus optional flags.", span);
            }

            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length == 3)
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                return (compiled, text);
            }

            return (CreatePattern(arguments.Length == 3 ? [arguments[0], arguments[2]] : [arguments[0]], signature, span), text);
        }

        private static (RePatternObject Pattern, object Replacement, PyString Text, int Count) CreateSubstituteInputs(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 3 or > 5 || !PyStringOps.TryAsString(arguments[2], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects pattern, replacement, text, plus optional count/flags.", span);
            }

            var replacement = arguments[1];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects replacement to be a string or callable.", span);
            }

            var count = 0;
            object[] patternArguments;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length == 5)
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                patternArguments = [compiled];
                if (arguments.Length >= 4)
                {
                    count = ParseOptionalInt(arguments[3], "count", signature, span);
                }
            }
            else
            {
                patternArguments = arguments.Length == 5 ? [arguments[0], arguments[4]] : [arguments[0]];
                if (arguments.Length >= 4)
                {
                    count = ParseOptionalInt(arguments[3], "count", signature, span);
                }
            }

            var pattern = patternArguments[0] is RePatternObject existing
                ? existing
                : CreatePattern(patternArguments, signature, span);
            return (pattern, replacement, text, count);
        }

        internal static object ExecuteSubstitute(RePatternObject pattern, object replacement, PyString text, int count, LythonSourceSpan span, ExecutionContext context, bool includeCount)
        {
            if (PyStringOps.TryAsString(replacement, out var replacementText))
            {
                if (!includeCount)
                {
                    return CreateUtf8String(pattern.Regex.Replace(text.Utf8Bytes.Span, replacementText.AsString(), count), context, span);
                }

                var result = pattern.Regex.Subn(text.Utf8Bytes.Span, replacementText.AsString(), count);
                return new PyTuple([CreateUtf8String(result.ResultBytes, context, span), new BigInteger(result.ReplacementCount)], context.MemoryGovernor, span);
            }

            if (replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "re.subn(...) replacement must be a string or callable."
                    : "re.sub(...) replacement must be a string or callable.", span);
            }

            var (resultText, replacementCount) = ExecuteCallableSubstitute(pattern, replacement, text, count, span, context);
            return includeCount
                ? new PyTuple([resultText, new BigInteger(replacementCount)], context.MemoryGovernor, span)
                : resultText;
        }

        private static (RePatternObject Pattern, PyString Text, int MaxSplit) CreateSplitInputs(object[] arguments, string signature, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 4 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects two string arguments (pattern and string), plus optional maxsplit/flags.", span);
            }

            var maxSplit = 0;
            object[] patternArguments;
            if (arguments[0] is RePatternObject compiled)
            {
                if (arguments.Length == 4)
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} does not accept flags when passed a compiled pattern.", span);
                }

                patternArguments = [compiled];
                if (arguments.Length == 3)
                {
                    maxSplit = ParseOptionalInt(arguments[2], "maxsplit", signature, span);
                }
            }
            else
            {
                patternArguments = arguments.Length == 4 ? [arguments[0], arguments[3]] : [arguments[0]];
                if (arguments.Length >= 3)
                {
                    maxSplit = ParseOptionalInt(arguments[2], "maxsplit", signature, span);
                }
            }

            var pattern = patternArguments[0] is RePatternObject existing
                ? existing
                : CreatePattern(patternArguments, signature, span);
            return (pattern, text, maxSplit);
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

            return (PythonReCompileOptions)(int)flags;
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

        internal static PyList CreateFindIterMatches(Utf8PythonRegex regex, PyString text, ExecutionContext context, LythonSourceSpan span)
        {
            var results = new PyList([], context.MemoryGovernor, span);
            foreach (var match in regex.FindIterDetailed(text.Utf8Bytes.Span))
            {
                context.CheckExecutionBudget(span);
                results.Add(CreateMatchObject(text, match, context, span));
            }

            return results;
        }

        internal static (PyString Result, int ReplacementCount) ExecuteCallableSubstitute(
            RePatternObject pattern,
            object replacement,
            PyString text,
            int count,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var state = new RegexReplacementState(text, replacement, span, context);
            var result = pattern.Regex.Subn(
                text.Utf8Bytes.Span,
                state,
                static (replacementState, match) => EvaluateRegexReplacement(replacementState, match),
                count);
            return (CreateUtf8String(result.ResultBytes, context, span), result.ReplacementCount);
        }

        private sealed record RegexReplacementState(
            PyString Text,
            object Replacement,
            LythonSourceSpan Span,
            ExecutionContext Context);

        private static string EvaluateRegexReplacement(RegexReplacementState state, Utf8PythonDetailedMatchData match)
        {
            state.Context.CheckExecutionBudget(state.Span);
            var matchObject = CreateMatchObject(state.Text, match, state.Context, state.Span);
            var replacementValue = InvokeCallableTarget(
                state.Replacement,
                state.Span,
                state.Span,
                state.Context,
                () => [new CallArgumentValue(null, matchObject)]);

            if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
            {
                throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", state.Span);
            }

            return replacementText.AsString();
        }

        internal static ReMatchObject CreateMatchObject(PyString text, Utf8PythonDetailedMatchData match, ExecutionContext? context = null, LythonSourceSpan? span = null)
        {
            if (!match.TryGetGroup(0, out var wholeGroup) || !wholeGroup.Success)
            {
                throw new InvalidOperationException("Detailed regex match is missing the whole-match capture.");
            }

            var wholeStart = wholeGroup.HasContiguousByteRange
                ? text.ByteIndexToRuneIndex(wholeGroup.StartOffsetInBytes)
                : wholeGroup.StartOffsetInUtf16;
            var wholeEnd = wholeGroup.HasContiguousByteRange
                ? text.ByteIndexToRuneIndex(wholeGroup.EndOffsetInBytes)
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
                    ? text.ByteIndexToRuneIndex(group.StartOffsetInBytes)
                    : group.StartOffsetInUtf16;
                var end = group.HasContiguousByteRange
                    ? text.ByteIndexToRuneIndex(group.EndOffsetInBytes)
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

            return new ReMatchObject(wholeValue, new BigInteger(wholeStart), new BigInteger(wholeEnd), match.CaptureSlotCount, captures, namedGroups ?? EmptyRegexNamedGroups);
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

    internal static class ReMatchMembers
    {
        public static bool TryGetMember(ReMatchObject match, string name, out object value)
        {
            value = name switch
                {
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
                "start" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "match.start() expects no arguments.", span);
                    }

                    return match.Start;
                }),
                "end" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "match.end() expects no arguments.", span);
                    }

                    return match.End;
                }),
                "span" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "match.span() expects no arguments.", span);
                    }

                    return CreateTuple(2, i => i == 0 ? match.Start : match.End, context, span);
                }),
                _ => null!,
            };

            return value is not null;
        }

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span)
        {
            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            if (index == 0)
            {
                return match.Value;
            }

            return (object?)match.Captures[index - 1]?.Value ?? PyNone.Instance;
        }

        private static object ResolveNamedGroup(ReMatchObject match, string groupName, LythonSourceSpan span)
        {
            if (!match.NamedGroups.TryGetValue(groupName, out var number))
            {
                throw new LythonRuntimeException("IndexError", $"Regex group '{groupName}' is not defined.", span);
            }

            return ResolveIndexedGroup(match, number, span);
        }
    }

    private sealed class SysModule : PyModule
    {
        private readonly PyList _argv;
        private readonly HostTextInputHandle _stdin;
        private readonly HostTextOutputHandle _stdout;
        private readonly HostTextOutputHandle _stderr;

        public SysModule(ExecutionState state) : base("sys")
        {
            var args = new object[state.Args.Count];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = state.Args[i];
            }

            _argv = new PyList(args, state.MemoryGovernor, null);
            _stdin = state.Stdin;
            _stdout = state.Stdout;
            _stderr = state.Stderr;
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "argv" => _argv,
                "stdin" => _stdin,
                "stdout" => _stdout,
                "stderr" => _stderr,
                "exit" => new BuiltinCallable(LythonKnownCallableSignatures.SysExit, Exit),
                _ => null!
            };

            return value is not null;
        }

        private static object Exit(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "sys.exit([code]) expects zero or one argument.", span);
            }

            var value = arguments.Length == 0 ? PyNone.Instance : arguments[0];
            throw new LythonRuntimeException("SystemExit", FormatSystemExitMessage(value), span, payload: value);
        }
    }

    private sealed class DataclassesModule : PyModule
    {
        public static readonly DataclassesModule Instance = new();

        private DataclassesModule()
            : base("dataclasses")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            if (name == "dataclass")
            {
                value = new BuiltinCallable(
                    "dataclasses.dataclass",
                    static (_, span, _) => throw new LythonRuntimeException(
                        "TypeError",
                        "@dataclass in Lython is compile-time only. Apply it directly as a class decorator.",
                        span),
                    ["cls"],
                    requiredCount: 1);
                return true;
            }

            if (name == "field")
            {
                value = PyDataclass.FieldCallable;
                return true;
            }

            if (name == "is_dataclass")
            {
                value = new BuiltinCallable(
                    LythonKnownCallableSignatures.DataclassesIsDataclass,
                    PyDataclass.IsDataclass);
                return true;
            }

            if (name == "fields")
            {
                value = new BuiltinCallable(
                    LythonKnownCallableSignatures.DataclassesFields,
                    PyDataclass.Fields);
                return true;
            }

            if (name == "asdict")
            {
                value = new BuiltinCallable(
                    LythonKnownCallableSignatures.DataclassesAsDict,
                    PyDataclass.AsDict);
                return true;
            }

            if (name == "astuple")
            {
                value = new BuiltinCallable(
                    LythonKnownCallableSignatures.DataclassesAsTuple,
                    PyDataclass.AsTuple);
                return true;
            }

            if (name == "replace")
            {
                value = PyDataclass.ReplaceCallable;
                return true;
            }

            if (name == "MISSING")
            {
                value = PyDataclassMissing.Instance;
                return true;
            }

            if (name == "KW_ONLY")
            {
                value = PyDataclassKwOnlyMarker.Instance;
                return true;
            }

            if (name == "InitVar")
            {
                value = PyDataclassInitVarMarker.Instance;
                return true;
            }

            if (name == "FrozenInstanceError")
            {
                value = new ExceptionTypeValue("FrozenInstanceError");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }
    }

    private sealed class TypingModule : PyModule
    {
        public static readonly TypingModule Instance = new();

        private TypingModule()
            : base("typing")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            if (name == "ClassVar")
            {
                value = PyString.FromString("ClassVar");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }
    }

    internal static class RePatternMembers
    {
        public static bool TryGetMember(RePatternObject pattern, string name, out object value)
        {
            value = name switch
            {
                "search" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.SearchDetailedData(input))),
                "match" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.MatchDetailedData(input))),
                "fullmatch" => new BoundCallable((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.FullMatchDetailedData(input))),
                "findall" => new BoundCallable((arguments, span, context) => ExecuteFindAll(pattern, arguments, span, context)),
                "finditer" => new BoundCallable((arguments, span, context) => ExecuteFindIter(pattern, arguments, span, context)),
                "sub" => new BoundCallable((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, includeCount: false), "pattern.sub", ["repl", "string", "count"], 2),
                "subn" => new BoundCallable((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, includeCount: true), "pattern.subn", ["repl", "string", "count"], 2),
                "split" => new BoundCallable((arguments, span, context) => ExecuteSplit(pattern, arguments, span, context), "pattern.split", ["string", "maxsplit"], 1),
                _ => null!,
            };

            return value is not null;
        }

        private static object ExecuteMatch(
            RePatternObject pattern,
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            Func<Utf8PythonRegex, ReadOnlySpan<byte>, Utf8PythonDetailedMatchData> operation)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Compiled regex method expects one string argument.", span);
            }

            var match = operation(pattern.Regex, text.Utf8Bytes.Span);
            return match.Success ? ReModule.CreateMatchObject(text, match, context, span) : PyNone.Instance;
        }

        private static object ExecuteFindAll(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.findall(string) expects one string argument.", span);
            }

            return new ReFindAllResult(ProjectFindAllResult(pattern.Regex.FindAllToUtf8(text.Utf8Bytes.Span), span, context));
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
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.finditer(string) expects one string argument.", span);
            }

            context.CheckExecutionBudget(span);
            return ReModule.CreateFindIterMatches(pattern.Regex, text, context, span);
        }

        private static object ExecuteSub(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context, bool includeCount)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 2 or > 3 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "pattern.subn(replacement, string[, count]) expects replacement, text string, and optional count."
                    : "pattern.sub(replacement, string[, count]) expects replacement, text string, and optional count.", span);
            }

            var replacement = arguments[0];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", includeCount
                    ? "pattern.subn(...) replacement must be a string or callable."
                    : "pattern.sub(...) replacement must be a string or callable.", span);
            }

            var count = arguments.Length == 3 ? ReModule.ParseOptionalInt(arguments[2], "count", includeCount ? "pattern.subn" : "pattern.sub", span) : 0;
            return ReModule.ExecuteSubstitute(pattern, replacement, text, count, span, context, includeCount);
        }

        private static object ExecuteSplit(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.split(string[, maxsplit]) expects a string and optional integer maxsplit.", span);
            }

            var maxSplit = arguments.Length == 2 ? ReModule.ParseOptionalInt(arguments[1], "maxsplit", "pattern.split", span) : 0;
            return ReModule.ProjectSplitResult(pattern.Regex.SplitDetailed(text.Utf8Bytes.Span, maxSplit), span, context);
        }
    }

    private sealed class ArgparseModule : PyModule
    {
        public static readonly ArgparseModule Instance = new();

        private ArgparseModule() : base("argparse")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ArgumentParser" => new BuiltinCallable(LythonKnownCallableSignatures.ArgparseArgumentParser, CreateParser),
                "SUPPRESS" => PyString.FromString("SUPPRESS"),
                _ => null!
            };

            return value is not null;
        }

        private object CreateParser(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser([description]) expects zero or one argument.", span);
            }

            PyString? description = null;
            if (arguments.Length == 1)
            {
                if (!PyStringOps.TryAsString(arguments[0], out description))
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser(description) expects description to be a string.", span);
                }
            }

            return new ArgumentParserObject(description);
        }
    }

    internal sealed class ArgumentParserObject
    {
        private readonly List<ArgumentSpec> _arguments = [];
        private readonly List<ArgparseMutuallyExclusiveGroupObject> _groups = [];
        private int _nextGroupId;

        public ArgumentParserObject(PyString? description)
        {
            Description = description;
        }

        public PyString? Description { get; }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable("argparse.ArgumentParser.add_argument", AddArgument),
                "add_mutually_exclusive_group" => new CustomMethodCallable("argparse.ArgumentParser.add_mutually_exclusive_group", AddMutuallyExclusiveGroup),
                "parse_args" => new CustomMethodCallable("argparse.ArgumentParser.parse_args", ParseArgs),
                _ => null!
            };

            return value is not null;
        }

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _arguments.Add(CreateArgumentSpec(arguments, span, groupId: null, context));
            return PyNone.Instance;
        }

        private object AddMutuallyExclusiveGroup(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            ValidateSupportedKeywords(arguments, ["required"], "argparse.ArgumentParser.add_mutually_exclusive_group", span);

            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_mutually_exclusive_group([required]) expects zero or one argument.",
                    span);
            }

            var required = arguments.Length == 1 && IsTruthy(arguments[0].Value);
            var group = new ArgparseMutuallyExclusiveGroupObject(this, _nextGroupId++, required);
            _groups.Add(group);
            return group;
        }

        internal void AddArgumentToGroup(CallArgumentValue[] arguments, LythonSourceSpan span, int groupId, ExecutionContext context)
        {
            _arguments.Add(CreateArgumentSpec(arguments, span, groupId, context));
        }

        private object ParseArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            ValidateSupportedKeywords(arguments, ["args"], "argparse.ArgumentParser.parse_args", span);

            List<string> argv;
            switch (arguments.Length)
            {
                case 0:
                {
                    argv = new List<string>(context.State.Args.Count);
                    foreach (var item in context.State.Args)
                    {
                        argv.Add(item.AsString());
                    }

                    break;
                }
                case 1:
                {
                    argv = [];
                    foreach (var item in ToSequence(arguments[0].Value, span))
                    {
                        if (!PyStringOps.TryAsString(item, out var text))
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.parse_args(args) expects an iterable of strings.", span);
                        }

                        argv.Add(text.AsString());
                    }

                    break;
                }
                default:
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.parse_args([args]) expects zero or one argument.", span);
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var seenSpecs = new HashSet<ArgumentSpec>();
            foreach (var spec in _arguments)
            {
                values[spec.Destination] = CloneDefault(spec.DefaultValue, context, span);
            }

            var positionalSpecs = new List<ArgumentSpec>(_arguments.Count);
            foreach (var argument in _arguments)
            {
                if (argument.IsPositional)
                {
                    positionalSpecs.Add(argument);
                }
            }

            var positionalIndex = 0;

            for (var index = 0; index < argv.Count; index++)
            {
                var token = argv[index];
                var spec = FindOptionalArgument(token);
                if (spec is null)
                {
                    if (token.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw CreateSystemExit($"unrecognized arguments: {token}", span);
                    }

                    if (positionalIndex >= positionalSpecs.Count)
                    {
                        throw CreateSystemExit($"unrecognized arguments: {token}", span);
                    }

                    var positionalSpec = positionalSpecs[positionalIndex];
                    if (positionalSpec.Nargs is "*" or "+")
                    {
                        var list = values[positionalSpec.Destination] as PyList ?? new PyList([], context.MemoryGovernor, span);
                        while (true)
                        {
                            list.Add(ConvertArgumentValue(positionalSpec, positionalSpec.Destination, token, span, context));
                            seenSpecs.Add(positionalSpec);
                            values[positionalSpec.Destination] = list;
                            if (index + 1 >= argv.Count)
                            {
                                break;
                            }

                            var nextToken = argv[index + 1];
                            if (nextToken.StartsWith("-", StringComparison.Ordinal) &&
                                HasOptionalArgumentNamed(nextToken))
                            {
                                break;
                            }

                            index++;
                            token = argv[index];
                        }

                        positionalIndex++;
                        continue;
                    }

                    values[positionalSpec.Destination] = ConvertArgumentValue(positionalSpec, positionalSpec.Destination, token, span, context);
                    seenSpecs.Add(positionalSpec);
                    positionalIndex++;
                    continue;
                }

                if (spec.Action == "store_true")
                {
                    values[spec.Destination] = true;
                    seenSpecs.Add(spec);
                    continue;
                }

                if (spec.Action == "store_false")
                {
                    values[spec.Destination] = false;
                    seenSpecs.Add(spec);
                    continue;
                }

                if (spec.Action == "store_const")
                {
                    values[spec.Destination] = spec.ConstValue;
                    seenSpecs.Add(spec);
                    continue;
                }

                if (index + 1 >= argv.Count)
                {
                    throw CreateSystemExit($"argument {token}: expected one argument", span);
                }

                index++;
                var converted = ConvertArgumentValue(spec, token, argv[index], span, context);
                if (spec.Action == "append")
                {
                    var list = values[spec.Destination] as PyList ?? new PyList([], context.MemoryGovernor, span);
                    list.Add(converted);
                    values[spec.Destination] = list;
                    seenSpecs.Add(spec);
                }
                else
                {
                    values[spec.Destination] = converted;
                    seenSpecs.Add(spec);
                }
            }

            foreach (var spec in _arguments)
            {
                if (spec.IsPositional && spec.Nargs == "+" && !seenSpecs.Contains(spec))
                {
                    var display = spec.OptionNames.FirstOrDefault() ?? spec.Destination;
                    throw CreateSystemExit($"the following arguments are required: {display}", span);
                }

                var current = values[spec.Destination];
                if (spec.Required && IsMissingValue(current, spec.Action))
                {
                    var display = spec.OptionNames.FirstOrDefault() ?? spec.Destination;
                    throw CreateSystemExit($"the following arguments are required: {display}", span);
                }
            }

            foreach (var group in _groups)
            {
                var present = 0;
                foreach (var spec in seenSpecs)
                {
                    if (spec.GroupId == group.Id)
                    {
                        present++;
                    }
                }

                if (present > 1)
                {
                    throw CreateSystemExit("mutually exclusive arguments must not be used together", span);
                }

                if (group.Required && present == 0)
                {
                    throw CreateSystemExit("one of the mutually exclusive arguments is required", span);
                }
            }

            return new ArgparseNamespaceObject(values);
        }

        private static object ConvertArgumentValue(ArgumentSpec spec, string optionName, string token, LythonSourceSpan span, ExecutionContext context)
        {
            object converted = PyString.FromString(token);
            if (spec.Converter is ICallable callable)
            {
                converted = RuntimeValue(callable.Invoke([new CallArgumentValue(null, converted)], span, context));
            }

            if (spec.Choices is not null)
            {
                var matched = false;
                for (var i = 0; i < spec.Choices.Count; i++)
                {
                    if (PyEquality.AreEqual(spec.Choices[i], converted))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    throw CreateSystemExit($"argument {optionName}: invalid choice: '{token}'", span);
                }
            }

            return converted;
        }

        private static object CloneDefault(object value, ExecutionContext context, LythonSourceSpan span)
        {
            return value switch
            {
                PyList list => new PyList(list, context.MemoryGovernor, span),
                PyDict dict => new PyDict(dict, context.MemoryGovernor, span),
                PySet set => new PySet(set, context.MemoryGovernor, span),
                _ => value
            };
        }

        private static bool IsMissingValue(object value, string action)
        {
            return action switch
            {
                "append" => value is PyList list && list.Count == 0,
                "store_true" => value is bool boolean && boolean == false,
                "store_false" => value is bool boolean && boolean == true,
                "store_const" => value is PyNone,
                _ => value is PyNone
            };
        }

        private static object DefaultForAction(string action, ExecutionContext context, LythonSourceSpan span)
        {
            return action switch
            {
                "store_true" => false,
                "store_false" => true,
                "append" => new PyList([], context.MemoryGovernor, span),
                "store_const" => PyNone.Instance,
                _ => PyNone.Instance
            };
        }

        private static void ValidateAction(string action, LythonSourceSpan span)
        {
            if (action is "store" or "store_true" or "store_false" or "append" or "store_const")
            {
                return;
            }

            throw new LythonRuntimeException(
                "ValueError",
                "argparse.ArgumentParser.add_argument(..., action=...) only supports 'store', 'store_true', 'store_false', 'append', or 'store_const'.",
                span);
        }

        private static object? ValidateConverter(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (value is ICallable)
            {
                return value;
            }

            throw new LythonRuntimeException(
                "TypeError",
                "argparse.ArgumentParser.add_argument(..., type=...) expects a callable or None.",
                span);
        }

        private static string? ValidateNargs(object value, bool isPositional, LythonSourceSpan span)
        {
            if (!isPositional || !PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) only supports positional nargs='*' or '+'.",
                    span);
            }

            var nargs = text.AsString();
            if (nargs is "*" or "+")
            {
                return nargs;
            }

            throw new LythonRuntimeException(
                "TypeError",
                "argparse.ArgumentParser.add_argument(..., nargs=...) only supports positional nargs='*' or '+'.",
                span);
        }

        private static ArgumentSpec CreateArgumentSpec(CallArgumentValue[] arguments, LythonSourceSpan span, int? groupId, ExecutionContext context)
        {
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects at least one argument name.", span);
            }

            var positional = new List<object>(arguments.Length);
            var keyword = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    positional.Add(argument.Value);
                }
                else
                {
                    keyword[argument.Name] = argument.Value;
                }
            }

            ValidateSupportedKeywords(keyword.Keys, ["dest", "action", "required", "default", "choices", "type", "nargs", "help", "const"], "argparse.ArgumentParser.add_argument", span);
            if (positional.Count == 0)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects at least one argument name.", span);
            }

            var optionNames = new string[positional.Count];
            for (var i = 0; i < positional.Count; i++)
            {
                var value = positional[i];
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects option names to be strings.", span);
                }

                optionNames[i] = text.AsString();
            }

            var dest = keyword.TryGetValue("dest", out var explicitDest)
                ? RequireString("dest", explicitDest, span).AsString()
                : InferDestination(optionNames, span);
            var action = keyword.TryGetValue("action", out var actionValue)
                ? RequireString("action", actionValue, span).AsString()
                : "store";
            ValidateAction(action, span);
            var isPositional = optionNames[0].Length != 0 && !optionNames[0].StartsWith("-", StringComparison.Ordinal);
            var nargs = keyword.TryGetValue("nargs", out var nargsValue)
                ? ValidateNargs(nargsValue, isPositional, span)
                : null;
            var required = keyword.TryGetValue("required", out var requiredValue) && IsTruthy(requiredValue);
            var defaultValue = keyword.TryGetValue("default", out var maybeDefault)
                ? maybeDefault
                : nargs == "*"
                    ? new PyList([], context.MemoryGovernor, span)
                    : DefaultForAction(action, context, span);
            object[]? choices = keyword.TryGetValue("choices", out var choicesValue)
                ? [.. ToSequence(choicesValue, span)]
                : null;
            var converter = keyword.TryGetValue("type", out var typeValue) ? ValidateConverter(typeValue, span) : null;
            var constValue = action == "store_const" && keyword.TryGetValue("const", out var constant)
                ? constant
                : PyNone.Instance;

            return new ArgumentSpec(optionNames, dest, action, required, defaultValue, choices, converter, isPositional, nargs, groupId, constValue);
        }

        private static void ValidateSupportedKeywords(IEnumerable<string> providedNames, IReadOnlyCollection<string> supportedNames, string signature, LythonSourceSpan span)
        {
            foreach (var name in providedNames)
            {
                if (!supportedNames.Contains(name))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{name}'.", span);
                }
            }
        }

        private static void ValidateSupportedKeywords(CallArgumentValue[] arguments, IReadOnlyCollection<string> supportedNames, string signature, LythonSourceSpan span)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var name = arguments[i].Name;
                if (name is not null && !supportedNames.Contains(name))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{name}'.", span);
                }
            }
        }

        private bool HasOptionalArgumentNamed(string token)
        {
            foreach (var candidate in _arguments)
            {
                if (!candidate.IsPositional && candidate.OptionNames.Contains(token, StringComparer.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private ArgumentSpec? FindOptionalArgument(string token)
        {
            foreach (var candidate in _arguments)
            {
                if (!candidate.IsPositional && candidate.OptionNames.Contains(token, StringComparer.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static PyString RequireString(string name, object value, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser.add_argument(..., {name}=...) expects a string.", span);
            }

            return text;
        }

        private static string InferDestination(IReadOnlyList<string> optionNames, LythonSourceSpan span)
        {
            string? preferred = null;
            for (var i = optionNames.Count - 1; i >= 0; i--)
            {
                var candidate = optionNames[i];
                if (candidate.StartsWith("--", StringComparison.Ordinal))
                {
                    preferred = candidate;
                    break;
                }

                preferred ??= candidate;
            }

            if (string.IsNullOrWhiteSpace(preferred))
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects a valid option name.", span);
            }

            return preferred.TrimStart('-').Replace('-', '_');
        }

        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span)
            => new("SystemExit", message, span, payload: BigInteger.One);
    }

    internal sealed class ArgparseNamespaceObject
    {
        private readonly Dictionary<string, object> _members;

        public ArgparseNamespaceObject(Dictionary<string, object> members)
        {
            _members = members;
        }

        public bool TryGetMember(string name, out object value) => _members.TryGetValue(name, out value!);
    }

    internal sealed class ArgparseMutuallyExclusiveGroupObject
    {
        private readonly ArgumentParserObject _parser;

        public ArgparseMutuallyExclusiveGroupObject(ArgumentParserObject parser, int id, bool required)
        {
            _parser = parser;
            Id = id;
            Required = required;
        }

        public int Id { get; }

        public bool Required { get; }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable("argparse._MutuallyExclusiveGroup.add_argument", AddArgument),
                _ => null!
            };

            return value is not null;
        }

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _parser.AddArgumentToGroup(arguments, span, Id, context);
            return PyNone.Instance;
        }
    }

    private sealed record ArgumentSpec(
        IReadOnlyList<string> OptionNames,
        string Destination,
        string Action,
        bool Required,
        object DefaultValue,
        IReadOnlyList<object>? Choices,
        object? Converter,
        bool IsPositional,
        string? Nargs,
        int? GroupId,
        object ConstValue);

    private sealed class CustomMethodCallable : ICallable
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public CustomMethodCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _name = name;
            _implementation = implementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }
    }

    private sealed class PathlibModule : PyModule
    {
        public static readonly PathlibModule Instance = new();

        private PathlibModule() : base("pathlib")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "Path" => new BuiltinCallable(LythonKnownCallableSignatures.PathlibPath, CreatePath),
                _ => null!
            };

            return value is not null;
        }

        private object CreatePath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "pathlib.Path(path[, ...]) expects one or more string arguments.", span);
            }

            PyString? path = null;
            foreach (var argument in arguments)
            {
                if (!PyStringOps.TryAsString(argument, out var segment))
                {
                    throw new LythonRuntimeException("TypeError", "pathlib.Path(path[, ...]) expects one or more string arguments.", span);
                }

                path = path is null ? segment : PathOps.Join(path, segment);
            }

            return new PyPath(PathOps.Normalize(path!));
        }
    }

    internal static class StringMembers
    {
        public static bool TryGetMember(PyString text, string name, out object value)
        {
            value = name switch
            {
                "replace" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 2 or > 3 ||
                        !PyStringOps.TryAsString(arguments[0], out var oldValue) ||
                        !PyStringOps.TryAsString(arguments[1], out var newValue))
                    {
                        throw new LythonRuntimeException("TypeError", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", span);
                    }

                    var count = arguments.Length == 3 ? ParseStringOptionalInt(arguments[2], "count", "str.replace(old, new[, count])", span) : -1;
                    return PyStringOps.Replace(text, oldValue, newValue, count);
                }, "str.replace", ["old", "new", "count"], 2),
                "startswith" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.startswith(prefix[, start[, end]])");
                    return StartsOrEndsWith(text, arguments[0], start, end, isStart: true, span);
                }, "str.startswith", ["prefix", "start", "end"], 1),
                "endswith" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.endswith(suffix[, start[, end]])");
                    return StartsOrEndsWith(text, arguments[0], start, end, isStart: false, span);
                }, "str.endswith", ["suffix", "start", "end"], 1),
                "lower" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.lower() expects no arguments.", span);
                    }

                    return text.ToLowerInvariant();
                }),
                "capitalize" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.capitalize() expects no arguments.", span);
                    }

                    return PyStringOps.Capitalize(text);
                }),
                "islower" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.islower() expects no arguments.", span);
                    }

                    return PyStringOps.IsLower(text);
                }),
                "upper" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.upper() expects no arguments.", span);
                    }

                    return text.ToUpperInvariant();
                }),
                "swapcase" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.swapcase() expects no arguments.", span);
                    }

                    return PyStringOps.SwapCase(text);
                }),
                "title" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.title() expects no arguments.", span);
                    }

                    return PyStringOps.Title(text);
                }),
                "isupper" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isupper() expects no arguments.", span);
                    }

                    return PyStringOps.IsUpper(text);
                }),
                "isalpha" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isalpha() expects no arguments.", span);
                    }

                    return PyStringOps.IsAlpha(text);
                }),
                "isdigit" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isdigit() expects no arguments.", span);
                    }

                    return PyStringOps.IsDigit(text);
                }),
                "isalnum" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isalnum() expects no arguments.", span);
                    }

                    return PyStringOps.IsAlnum(text);
                }),
                "isspace" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isspace() expects no arguments.", span);
                    }

                    return PyStringOps.IsSpace(text);
                }),
                "split" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return PyStringOps.SplitWhitespace(text, context.MemoryGovernor, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([separator[, maxsplit]])", span) : -1;
                        return PyStringOps.SplitWhitespace(text, maxSplit, context.MemoryGovernor, span);
                    }

                    if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.split([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([separator[, maxsplit]])", span) : -1;
                    try
                    {
                        return PyStringOps.Split(text, separator, maxSplit, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.split", ["separator", "maxsplit"], 0),
                "rsplit" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return PyStringOps.RSplitWhitespace(text, -1, context.MemoryGovernor, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([separator[, maxsplit]])", span) : -1;
                        return PyStringOps.RSplitWhitespace(text, maxSplit, context.MemoryGovernor, span);
                    }

                    if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rsplit([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([separator[, maxsplit]])", span) : -1;
                    try
                    {
                        return PyStringOps.RSplit(text, separator, maxSplit, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.rsplit", ["separator", "maxsplit"], 0),
                "splitlines" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                    }

                    var keepEnds = false;
                    if (arguments.Length == 1)
                    {
                        if (arguments[0] is not bool parsedKeepEnds)
                        {
                            throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                        }

                        keepEnds = parsedKeepEnds;
                    }

                    return PyStringOps.SplitLines(text, keepEnds, context.MemoryGovernor, span);
                }, "str.splitlines", ["keepends"], 0),
                "expandtabs" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.expandtabs([tabsize]) expects zero or one integer argument.", span);
                    }

                    var tabSize = arguments.Length == 1 ? ParseStringOptionalInt(arguments[0], "tabsize", "str.expandtabs([tabsize])", span) : 8;
                    return PyStringOps.ExpandTabs(text, tabSize);
                }, "str.expandtabs", ["tabsize"], 0),
                "strip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.strip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.Strip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.strip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.Strip(text, chars);
                }, "str.strip", ["chars"], 0),
                "lstrip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.lstrip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.LStrip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.lstrip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.LStrip(text, chars);
                }, "str.lstrip", ["chars"], 0),
                "rstrip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.rstrip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.RStrip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rstrip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.RStrip(text, chars);
                }, "str.rstrip", ["chars"], 0),
                "join" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.join(iterable) expects one argument.", span);
                    }

                    IEnumerable<PyString> EnumerateParts()
                    {
                        foreach (var part in ToSequence(arguments[0], span))
                        {
                            if (!PyStringOps.TryAsString(part, out var partText))
                            {
                                throw new LythonRuntimeException("TypeError", "str.join(iterable) expects an iterable of strings.", span);
                            }

                            yield return partText;
                        }
                    }

                    return JoinStrings(text, EnumerateParts());
                }, "str.join", ["iterable"]),
                "center" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.center(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.center(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.center(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.Center(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.center", ["width", "fillchar"], 1),
                "ljust" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.ljust(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.ljust(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.ljust(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.LJust(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.ljust", ["width", "fillchar"], 1),
                "rjust" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.rjust(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.rjust(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.rjust(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.RJust(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.rjust", ["width", "fillchar"], 1),
                "zfill" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.zfill(width) expects one integer width argument.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.zfill(width)", span);
                    return PyStringOps.ZFill(text, width);
                }, "str.zfill", ["width"]),
                "find" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.find(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.find(sub[, start[, end]])");
                    return PyStringOps.Find(text, needle, start, end);
                }, "str.find", ["sub", "start", "end"], 1),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.index(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.index(sub[, start[, end]])");
                    var result = PyStringOps.Find(text, needle, start, end);
                    if ((BigInteger)result < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "substring not found", span);
                    }

                    return result;
                }, "str.index", ["sub", "start", "end"], 1),
                "rfind" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rfind(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.rfind(sub[, start[, end]])");
                    return PyStringOps.RFind(text, needle, start, end);
                }, "str.rfind", ["sub", "start", "end"], 1),
                "rindex" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rindex(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.rindex(sub[, start[, end]])");
                    var result = PyStringOps.RFind(text, needle, start, end);
                    if ((BigInteger)result < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "substring not found", span);
                    }

                    return result;
                }, "str.rindex", ["sub", "start", "end"], 1),
                "count" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.count(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end) = ParseStringBounds(text.Length, arguments, span, "str.count(sub[, start[, end]])");
                    return PyStringOps.Count(text, needle, start, end);
                }, "str.count", ["sub", "start", "end"], 1),
                "removeprefix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var prefix))
                    {
                        throw new LythonRuntimeException("TypeError", "str.removeprefix(prefix) expects one string argument.", span);
                    }

                    return text.StartsWith(prefix)
                        ? SliceByByteCount(text, prefix.Utf8Bytes.Length, text.Utf8Bytes.Length - prefix.Utf8Bytes.Length)
                        : text;
                }, "str.removeprefix", ["prefix"]),
                "removesuffix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                    {
                        throw new LythonRuntimeException("TypeError", "str.removesuffix(suffix) expects one string argument.", span);
                    }

                    return suffix.Length != 0 && text.EndsWith(suffix)
                        ? SliceByByteCount(text, 0, text.Utf8Bytes.Length - suffix.Utf8Bytes.Length)
                        : text;
                }, "str.removesuffix", ["suffix"]),
                "partition" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.partition(sep) expects one string argument.", span);
                    }

                    try
                    {
                        return PyStringOps.Partition(text, separator, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.partition", ["sep"]),
                "rpartition" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rpartition(sep) expects one string argument.", span);
                    }

                    try
                    {
                        return PyStringOps.RPartition(text, separator, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.rpartition", ["sep"]),
                "format" => new CustomMethodCallable("str.format", (arguments, span, context) =>
                {
                    try
                    {
                        var positionalCount = 0;
                        foreach (var argument in arguments)
                        {
                            if (argument.Name is null)
                            {
                                positionalCount++;
                            }
                        }

                        var positional = new object[positionalCount];
                        var positionalIndex = 0;
                        foreach (var argument in arguments)
                        {
                            if (argument.Name is null)
                            {
                                positional[positionalIndex++] = argument.Value;
                            }
                        }

                        var keywords = new Dictionary<string, object>(StringComparer.Ordinal);
                        foreach (var argument in arguments)
                        {
                            if (argument.Name is null)
                            {
                                continue;
                            }

                            if (!keywords.TryAdd(argument.Name, argument.Value))
                            {
                                throw CallErrors.MultipleValues("Method", "str.format", argument.Name, span);
                            }
                        }

                        return PyStringOps.Format(text, positional, keywords, field => ResolveFormatField(field, positional, keywords, span, context));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new LythonRuntimeException("KeyError", ex.Message, span);
                    }
                }),
                "format_map" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict mapping)
                    {
                        throw new LythonRuntimeException("TypeError", "str.format_map(mapping) expects one dictionary argument.", span);
                    }

                    try
                    {
                        var keywords = PyStringOps.ExtractStringKeyDictionary(mapping);
                        return PyStringOps.Format(text, Array.Empty<object>(), keywords, field => ResolveFormatField(field, Array.Empty<object>(), keywords, span, context));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new LythonRuntimeException("KeyError", ex.Message, span);
                    }
                }, "str.format_map", ["mapping"]),
                _ => null!,
            };

            return value is not null;
        }

        private static int ParseStringOptionalInt(object value, string name, string signature, LythonSourceSpan span)
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

        private static (int Start, int End) ParseStringBounds(int textLength, object[] arguments, LythonSourceSpan span, string signature)
        {
            try
            {
                object? start = arguments.Length >= 2 ? arguments[1] : null;
                object? end = arguments.Length == 3 ? arguments[2] : null;
                return PyStringOps.NormalizeRange(textLength, start, end);
            }
            catch (InvalidOperationException)
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
            }
        }

        private static bool StartsOrEndsWith(PyString text, object prefixOrTuple, int start, int end, bool isStart, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(prefixOrTuple, out var single))
            {
                return isStart ? PyStringOps.StartsWith(text, single, start, end) : PyStringOps.EndsWith(text, single, start, end);
            }

            if (prefixOrTuple is not PyTuple tuple)
            {
                throw new LythonRuntimeException("TypeError",
                    isStart
                        ? "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds."
                        : "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.",
                    span);
            }

            foreach (var item in tuple)
            {
                if (!PyStringOps.TryAsString(item, out var textItem))
                {
                    throw new LythonRuntimeException("TypeError",
                        isStart
                            ? $"tuple for startswith must only contain str, not {TypeName(item)}"
                            : $"tuple for endswith must only contain str, not {TypeName(item)}",
                        span);
                }

                if (isStart ? PyStringOps.StartsWith(text, textItem, start, end) : PyStringOps.EndsWith(text, textItem, start, end))
                {
                    return true;
                }
            }

            return false;
        }

        private static PyString? RequireFillChar(object value, string signature, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var fill))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects fillchar to be a string.", span);
            }

            return fill;
        }

        private static string TypeName(object value)
        {
            return value switch
            {
                BigInteger => "int",
                int => "int",
                bool => "bool",
                PyString => "str",
                PyTuple => "tuple",
                PyList => "list",
                PyDict => "dict",
                PyNone => "NoneType",
                _ => value.GetType().Name
            };
        }

        private static object ResolveFormatField(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var index = 0;
            var current = ResolveFormatFieldRoot(field, positional, keywords, ref index);

            while (index < field.Length)
            {
                if (field[index] == '.')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] is not '.' and not '[')
                    {
                        index++;
                    }

                    if (start == index)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var memberName = field[start..index];
                    if (!PyMemberAccess.TryResolve(current, memberName, context, span, out current))
                    {
                        throw PyMemberAccess.CreateMissingMemberError(current, memberName, span);
                    }

                    continue;
                }

                if (field[index] == '[')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] != ']')
                    {
                        index++;
                    }

                    if (index >= field.Length)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var token = field[start..index];
                    index++;
                    object key = int.TryParse(token, out var intIndex)
                        ? new BigInteger(intIndex)
                        : PyString.FromString(token);
                    current = PyIndexing.ReadIndex(current, key, span);
                    continue;
                }

                throw new InvalidOperationException("Invalid format field.");
            }

            return current;
        }

        private static object ResolveFormatFieldRoot(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            ref int index)
        {
            var start = index;
            while (index < field.Length && field[index] is not '.' and not '[')
            {
                index++;
            }

            var root = field[start..index];
            if (root.Length == 0)
            {
                throw new InvalidOperationException("Invalid format field.");
            }

            if (int.TryParse(root, out var intIndex))
            {
                if (intIndex < 0 || intIndex >= positional.Count)
                {
                    throw new IndexOutOfRangeException($"Replacement index {intIndex} out of range for positional args tuple");
                }

                return positional[intIndex];
            }

            if (!keywords.TryGetValue(root, out var value))
            {
                throw new KeyNotFoundException(root);
            }

            return value;
        }
    }

    private sealed class BoundCallable : ICallable
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly LythonCallableSignature _signature;

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation = null)
        {
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
            _signature = signature;
        }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string? name = null,
            string[]? parameterNames = null,
            int? requiredCount = null)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount))
        {
        }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string? name = null,
            string[]? parameterNames = null,
            int? requiredCount = null)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount), asyncImplementation)
        {
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Method");
            return _implementation(positional, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Method");
            return _asyncImplementation is null
                ? _implementation(positional, span, context)
                : await _asyncImplementation(positional, span, context).ConfigureAwait(false);
        }
    }

    internal sealed class FnMatchModule : PyModule
    {
        public static readonly FnMatchModule Instance = new();

        private FnMatchModule() : base("fnmatch")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "fnmatch" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatch, Match),
                "filter" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchFilter, Filter),
                _ => null!,
            };

            return value is not null;
        }

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatch(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern);
        }

        private object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable and a string pattern.", span);
            }

            var result = new PyList([], context.MemoryGovernor, span);
            foreach (var item in ToSequence(arguments[0], span))
            {
                if (!PyStringOps.TryAsString(item, out var name))
                {
                    throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable of strings.", span);
                }

                if (MatchSimple(name, pattern))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        internal static bool MatchSimple(PyString name, PyString pattern)
        {
            var nameRunes = MaterializeRunes(name);
            var patternRunes = MaterializeRunes(pattern);
            var memo = new Dictionary<(int Name, int Pattern), bool>();
            return MatchSimple(nameRunes, 0, patternRunes, 0, memo);
        }

        private static PyString[] MaterializeRunes(PyString value)
        {
            var runes = new PyString[value.Length];
            var index = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                runes[index++] = rune;
            }

            return runes;
        }

        private static bool MatchSimple(
            IReadOnlyList<PyString> name,
            int nameIndex,
            IReadOnlyList<PyString> pattern,
            int patternIndex,
            Dictionary<(int Name, int Pattern), bool> memo)
        {
            if (memo.TryGetValue((nameIndex, patternIndex), out var cached))
            {
                return cached;
            }

            bool result;
            if (patternIndex == pattern.Count)
            {
                result = nameIndex == name.Count;
            }
            else
            {
                var token = pattern[patternIndex];
                if (token.Utf8Bytes.Length == 1 && token.Utf8Bytes.Span[0] == (byte)'*')
                {
                    result = MatchSimple(name, nameIndex, pattern, patternIndex + 1, memo);
                    for (var next = nameIndex; !result && next < name.Count; next++)
                    {
                        result = MatchSimple(name, next + 1, pattern, patternIndex + 1, memo);
                    }
                }
                else if (token.Utf8Bytes.Length == 1 && token.Utf8Bytes.Span[0] == (byte)'?')
                {
                    result = nameIndex < name.Count && MatchSimple(name, nameIndex + 1, pattern, patternIndex + 1, memo);
                }
                else
                {
                    result = nameIndex < name.Count &&
                        token.Equals(name[nameIndex]) &&
                        MatchSimple(name, nameIndex + 1, pattern, patternIndex + 1, memo);
                }
            }

            memo[(nameIndex, patternIndex)] = result;
            return result;
        }
    }

    private sealed class JsonModule : PyModule
    {
        public static readonly JsonModule Instance = new();

        private JsonModule() : base("json")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "loads" => new BuiltinCallable(LythonKnownCallableSignatures.JsonLoads, Loads),
                "dumps" => new BuiltinCallable(LythonKnownCallableSignatures.JsonDumps, Dumps),
                _ => null!,
            };

            return value is not null;
        }

        private object Loads(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "json.loads(text) expects one string argument.", span);
            }

            try
            {
                using var document = JsonDocument.Parse(text.Utf8Bytes);
                return ConvertJson(document.RootElement, context, span);
            }
            catch (JsonException ex)
            {
                throw new LythonRuntimeException("ValueError", ex.Message, span);
            }
        }

        private object Dumps(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj) expects one argument.", span);
            }

            try
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }))
                {
                    WriteJsonValue(writer, arguments[0], context, span);
                }

                return CreateUtf8String(stream.ToArray(), context, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        private static object ConvertJson(JsonElement element, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Object => ConvertJsonObject(element, context, span),
                    JsonValueKind.Array => ConvertJsonArray(element, context, span),
                    JsonValueKind.String => JsonStringToPyString(element, context, span),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => PyNone.Instance,
                    JsonValueKind.Number => ConvertJsonNumber(element),
                    _ => throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}")
                };
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static PyDict ConvertJsonObject(JsonElement element, ExecutionContext context, LythonSourceSpan span)
        {
            var result = new PyDict(context.MemoryGovernor, span);
            foreach (var property in element.EnumerateObject())
            {
                context.CheckExecutionBudget(span);
                result.SetItem(CreateString(property.Name, context, span), ConvertJson(property.Value, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private static PyList ConvertJsonArray(JsonElement element, ExecutionContext context, LythonSourceSpan span)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            foreach (var item in element.EnumerateArray())
            {
                context.CheckExecutionBudget(span);
                result.Add(ConvertJson(item, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private static object ConvertJsonNumber(JsonElement element)
        {
            if (element.TryGetInt64(out var integer))
            {
                return new BigInteger(integer);
            }

            var raw = element.GetRawText();
            if (!raw.Contains('.', StringComparison.Ordinal) &&
                !raw.Contains('e', StringComparison.OrdinalIgnoreCase))
            {
                return BigInteger.Parse(raw, CultureInfo.InvariantCulture);
            }

            return element.GetDouble();
        }

        private static void WriteJsonValue(Utf8JsonWriter writer, object value, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                _ = value switch
                {
                    PyNone => WriteJsonNull(writer),
                    PyString text => WriteJsonString(writer, text),
                    bool boolean => WriteJsonBoolean(writer, boolean),
                    BigInteger integer => WriteJsonBigInteger(writer, integer),
                    double floating => WriteJsonDouble(writer, floating),
                    PyList list => WriteJsonList(writer, list, context, span),
                    PyTuple tuple => WriteJsonTuple(writer, tuple, context, span),
                    PyDict dict => WriteJsonDict(writer, dict, context, span),
                    _ => throw new InvalidOperationException($"Unsupported json.dumps value type: {value.GetType().Name}")
                };
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static object WriteJsonNull(Utf8JsonWriter writer)
        {
            writer.WriteNullValue();
            return PyNone.Instance;
        }

        private static object WriteJsonString(Utf8JsonWriter writer, PyString text)
        {
            writer.WriteRawValue(EscapeJsonString(text).Utf8Bytes.Span, skipInputValidation: true);
            return PyNone.Instance;
        }

        private static object WriteJsonBoolean(Utf8JsonWriter writer, bool boolean)
        {
            writer.WriteBooleanValue(boolean);
            return PyNone.Instance;
        }

        private static object WriteJsonBigInteger(Utf8JsonWriter writer, BigInteger integer)
        {
            writer.WriteRawValue(integer.ToString(CultureInfo.InvariantCulture));
            return PyNone.Instance;
        }

        private static object WriteJsonDouble(Utf8JsonWriter writer, double floating)
        {
            writer.WriteNumberValue(floating);
            return PyNone.Instance;
        }

        private static object WriteJsonList(Utf8JsonWriter writer, PyList list, ExecutionContext context, LythonSourceSpan span)
        {
            writer.WriteStartArray();
            foreach (var item in list)
            {
                WriteJsonValue(writer, item, context, span);
            }

            writer.WriteEndArray();
            return PyNone.Instance;
        }

        private static object WriteJsonTuple(Utf8JsonWriter writer, PyTuple tuple, ExecutionContext context, LythonSourceSpan span)
        {
            writer.WriteStartArray();
            foreach (var item in tuple)
            {
                WriteJsonValue(writer, item, context, span);
            }

            writer.WriteEndArray();
            return PyNone.Instance;
        }

        private static object WriteJsonDict(Utf8JsonWriter writer, PyDict dict, ExecutionContext context, LythonSourceSpan span)
        {
            writer.WriteStartObject();
            foreach (var pair in dict)
            {
                context.CheckExecutionBudget(span);
                if (pair.Key is not PyString key)
                {
                    throw new LythonRuntimeException("TypeError", "json.dumps() requires dictionary keys to be strings.", span);
                }

                writer.WritePropertyName(key.Utf8Bytes.Span);
                WriteJsonValue(writer, pair.Value, context, span);
            }

            writer.WriteEndObject();
            return PyNone.Instance;
        }

        private static PyString JsonStringToPyString(JsonElement element, ExecutionContext context, LythonSourceSpan span)
        {
            return CreateString(element.GetString() ?? string.Empty, context, span);
        }

        private static PyString EscapeJsonString(PyString text)
        {
            var builder = text.OwnerMemoryGovernor is null
                ? new Utf8ValueBuilder(text.Utf8Bytes.Length + 2)
                : new Utf8ValueBuilder(text.OwnerMemoryGovernor, text.AllocationSpan, text.Utf8Bytes.Length + 2);
            builder.Append((byte)'"');
            foreach (var rune in text.EnumerateRunes())
            {
                var runeBytes = rune.Utf8Bytes.Span;
                if (runeBytes.Length == 1)
                {
                    switch (runeBytes[0])
                    {
                        case (byte)'"':
                            builder.AppendAscii("\\\"");
                            continue;
                        case (byte)'\\':
                            builder.AppendAscii("\\\\");
                            continue;
                        case (byte)'\b':
                            builder.AppendAscii("\\b");
                            continue;
                        case (byte)'\f':
                            builder.AppendAscii("\\f");
                            continue;
                        case (byte)'\n':
                            builder.AppendAscii("\\n");
                            continue;
                        case (byte)'\r':
                            builder.AppendAscii("\\r");
                            continue;
                        case (byte)'\t':
                            builder.AppendAscii("\\t");
                            continue;
                    }

                    if (runeBytes[0] < 0x20)
                    {
                        builder.AppendAscii($"\\u{runeBytes[0]:X4}");
                        continue;
                    }
                }

                builder.Append(runeBytes);
            }

            builder.Append((byte)'"');
            return builder.ToPyString();
        }
    }

    private sealed class CsvModule : PyModule
    {
        public static readonly CsvModule Instance = new();

        private CsvModule() : base("csv")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "reader" => new BuiltinCallable(LythonKnownCallableSignatures.CsvReader, Reader),
                "writer" => new BuiltinCallable(LythonKnownCallableSignatures.CsvWriter, Writer),
                _ => null!,
            };

            return value is not null;
        }

        private object Reader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "csv.reader(lines[, delimiter]) expects one argument and an optional delimiter.", span);
            }

            var delimiter = GetDelimiter(arguments, 1, span);
            var rows = new PyList([], context.MemoryGovernor, span);
            foreach (var item in ToSequence(arguments[0], span))
            {
                context.CheckExecutionBudget(span);
                if (!PyStringOps.TryAsString(item, out var line))
                {
                    throw new LythonRuntimeException("TypeError", "csv.reader(lines) expects an iterable of strings.", span);
                }

                try
                {
                    var fields = ParseCsvLine(line, delimiter);
                    var items = new object[fields.Count];
                    for (var i = 0; i < fields.Count; i++)
                    {
                        items[i] = fields[i];
                    }

                    rows.Add(new PyList(items, context.MemoryGovernor, span));
                }
                catch (InvalidOperationException ex)
                {
                    throw new LythonRuntimeException("ValueError", ex.Message, span);
                }
            }

            return rows;
        }

        private object Writer(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "csv.writer([delimiter]) expects zero or one arguments.", span);
            }

            return new CsvWriterObject(GetDelimiter(arguments, 0, span));
        }

        private static PyString GetDelimiter(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index)
            {
                return PyStringOps.CommaLiteral;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var delimiter))
            {
                throw new LythonRuntimeException("TypeError", "csv delimiter must be a string.", span);
            }

            if (delimiter.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "csv delimiter must be one character.", span);
            }

            var delimiterBytes = delimiter.Utf8Bytes.Span;
            if (delimiterBytes.SequenceEqual("\r"u8) || delimiterBytes.SequenceEqual("\n"u8))
            {
                throw new LythonRuntimeException("ValueError", "csv delimiter cannot be a newline.", span);
            }

            return delimiter;
        }

        private static List<PyString> ParseCsvLine(PyString line, PyString delimiter)
        {
            var fields = new List<PyString>();
            var current = new List<byte>();
            var inQuotes = false;
            var fieldStartedWithQuote = false;
            var lineBytes = line.Utf8Bytes.Span;
            var delimiterBytes = delimiter.Utf8Bytes.Span;

            for (var i = 0; i < lineBytes.Length; i++)
            {
                var c = lineBytes[i];
                if (inQuotes)
                {
                    if (c == (byte)'"')
                    {
                        if (i + 1 < lineBytes.Length && lineBytes[i + 1] == (byte)'"')
                        {
                            current.Add((byte)'"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Add(c);
                    }

                    continue;
                }

                if (MatchesAt(lineBytes, i, delimiterBytes))
                {
                    fields.Add(CreateCsvField(current, line));
                    current.Clear();
                    fieldStartedWithQuote = false;
                    i += delimiterBytes.Length - 1;
                    continue;
                }

                if (c == (byte)'"')
                {
                    if (current.Count != 0)
                    {
                        throw new InvalidOperationException("Invalid csv input.");
                    }

                    inQuotes = true;
                    fieldStartedWithQuote = true;
                    continue;
                }

                if (fieldStartedWithQuote)
                {
                    throw new InvalidOperationException("Invalid csv input.");
                }

                current.Add(c);
            }

            if (inQuotes)
            {
                throw new InvalidOperationException("Invalid csv input.");
            }

            fields.Add(CreateCsvField(current, line));
            return fields;
        }

        private static PyString CreateCsvField(List<byte> bytes, PyString owner)
        {
            var utf8 = bytes.ToArray();
            return owner.OwnerMemoryGovernor is null
                ? PyString.FromOwnedUtf8(utf8)
                : PyString.FromOwnedUtf8(utf8, owner.OwnerMemoryGovernor, owner.AllocationSpan);
        }
    }

    internal sealed class CsvWriterObject
    {
        public CsvWriterObject(PyString delimiter)
        {
            Delimiter = delimiter;
        }

        public readonly PyString Delimiter;
        public readonly List<PyString[]> Rows = new();
    }

    internal sealed class DictKeysView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictKeysView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Keys.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictValuesView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictValuesView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator() => _dict.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class DictItemsView : IReadOnlyCollection<object>
    {
        private readonly PyDict _dict;

        public DictItemsView(PyDict dict)
        {
            _dict = dict;
        }

        public int Count => _dict.Count;

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var pair in _dict.Items)
            {
                yield return _dict.OwnerMemoryGovernor is null
                    ? new PyTuple([pair.Key, pair.Value])
                    : new PyTuple([pair.Key, pair.Value], _dict.OwnerMemoryGovernor, _dict.AllocationSpan);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class CsvWriterMembers
    {
        public static bool TryGetMember(CsvWriterObject writer, string name, out object value)
        {
            value = name switch
            {
                "writerow" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerow(row) expects one argument.", span);
                    }

                    writer.Rows.Add(ToCsvRow(arguments[0], span));
                    return PyNone.Instance;
                }),
                "writerows" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerows(rows) expects one argument.", span);
                    }

                    foreach (var row in ToSequence(arguments[0], span))
                    {
                        writer.Rows.Add(ToCsvRow(row, span));
                    }

                    return PyNone.Instance;
                }),
                "getvalue" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.getvalue() expects no arguments.", span);
                    }

                    return RenderCsvDocument(writer.Rows, writer.Delimiter);
                }),
                _ => null!,
            };

            return value is not null;
        }

        private static PyString[] ToCsvRow(object row, LythonSourceSpan span)
        {
            var cells = new List<PyString>();
            foreach (var cell in ToSequence(row, span))
            {
                cells.Add(cell switch
                {
                    PyNone => PyString.Empty,
                    PyString text => text,
                    BigInteger integer => PyString.FromString(integer.ToString()),
                    bool boolean => PyString.FromString(boolean ? "True" : "False"),
                    double floating => PyString.FromString(floating.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    _ => throw new LythonRuntimeException("TypeError", "CSV rows must contain scalar values.", span)
                });
            }

            return [.. cells];
        }

        private static PyString RenderCsvDocument(List<PyString[]> rows, PyString delimiter)
        {
            var builder = new Utf8ValueBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i != 0)
                {
                    builder.Append((byte)'\n');
                }

                builder.Append(RenderCsvRow(rows[i], delimiter));
            }

            return builder.ToPyString();
        }

        private static PyString RenderCsvRow(PyString[] row, PyString delimiter)
        {
            var builder = new Utf8ValueBuilder();
            for (var i = 0; i < row.Length; i++)
            {
                if (i != 0)
                {
                    builder.Append(delimiter);
                }

                builder.Append(EscapeCsvField(row[i], delimiter));
            }

            return builder.ToPyString();
        }

        private static PyString EscapeCsvField(PyString field, PyString delimiter)
        {
            var fieldBytes = field.Utf8Bytes.Span;
            var delimiterBytes = delimiter.Utf8Bytes.Span;
            var needsQuotes =
                IndexOfBytes(fieldBytes, delimiterBytes) >= 0 ||
                fieldBytes.IndexOf((byte)'"') >= 0 ||
                fieldBytes.IndexOf((byte)'\n') >= 0;

            if (!needsQuotes)
            {
                return field;
            }

            var builder = new Utf8ValueBuilder(fieldBytes.Length + 2);
            builder.Append((byte)'"');
            foreach (var b in fieldBytes)
            {
                if (b == (byte)'"')
                {
                    builder.Append((byte)'"');
                }

                builder.Append(b);
            }

            builder.Append((byte)'"');
            return builder.ToPyString();
        }
    }

    private static PyString JoinStrings(PyString separator, IEnumerable<PyString> parts)
    {
        var builder = new Utf8ValueBuilder();
        var first = true;
        foreach (var part in parts)
        {
            if (!first)
            {
                builder.Append(separator);
            }

            builder.Append(part);
            first = false;
        }

        return builder.ToPyString();
    }

    private static PyString SliceByByteCount(PyString text, int start, int length)
        => text.SliceByByteRange(start, start + length);

    private static int IndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool MatchesAt(ReadOnlySpan<byte> haystack, int index, ReadOnlySpan<byte> needle)
        => index + needle.Length <= haystack.Length &&
           haystack.Slice(index, needle.Length).SequenceEqual(needle);
}
