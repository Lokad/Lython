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
        Utf8PythonRegex Regex,
        int CaptureSlotCount,
        IReadOnlyDictionary<string, int> NamedGroups);

    internal readonly record struct RegexSubjectRange(
        PyString Original,
        PyString Segment,
        int Pos,
        int EndPos,
        bool IsValid = true);

    private sealed partial class MathModule : PyModule
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
                "sqrt" => new BuiltinCallable(LythonKnownCallableSignatures.MathSqrt, Sqrt),
                "exp" => new BuiltinCallable(LythonKnownCallableSignatures.MathExp, Exp),
                "log" => new BuiltinCallable(LythonKnownCallableSignatures.MathLog, Log),
                "log10" => new BuiltinCallable(LythonKnownCallableSignatures.MathLog10, Log10),
                "log2" => new BuiltinCallable(LythonKnownCallableSignatures.MathLog2, Log2),
                "sin" => new BuiltinCallable(LythonKnownCallableSignatures.MathSin, Sin),
                "cos" => new BuiltinCallable(LythonKnownCallableSignatures.MathCos, Cos),
                "tan" => new BuiltinCallable(LythonKnownCallableSignatures.MathTan, Tan),
                "asin" => new BuiltinCallable(LythonKnownCallableSignatures.MathAsin, Asin),
                "acos" => new BuiltinCallable(LythonKnownCallableSignatures.MathAcos, Acos),
                "atan" => new BuiltinCallable(LythonKnownCallableSignatures.MathAtan, Atan),
                "atan2" => new BuiltinCallable(LythonKnownCallableSignatures.MathAtan2, Atan2),
                "sinh" => new BuiltinCallable(LythonKnownCallableSignatures.MathSinh, Sinh),
                "cosh" => new BuiltinCallable(LythonKnownCallableSignatures.MathCosh, Cosh),
                "tanh" => new BuiltinCallable(LythonKnownCallableSignatures.MathTanh, Tanh),
                "floor" => new BuiltinCallable(LythonKnownCallableSignatures.MathFloor, Floor),
                "ceil" => new BuiltinCallable(LythonKnownCallableSignatures.MathCeil, Ceil),
                "fabs" => new BuiltinCallable(LythonKnownCallableSignatures.MathFabs, Fabs),
                "trunc" => new BuiltinCallable(LythonKnownCallableSignatures.MathTrunc, Trunc),
                "degrees" => new BuiltinCallable(LythonKnownCallableSignatures.MathDegrees, Degrees),
                "radians" => new BuiltinCallable(LythonKnownCallableSignatures.MathRadians, Radians),
                "isfinite" => new BuiltinCallable(LythonKnownCallableSignatures.MathIsFinite, IsFinite),
                "isinf" => new BuiltinCallable(LythonKnownCallableSignatures.MathIsInf, IsInf),
                "isnan" => new BuiltinCallable(LythonKnownCallableSignatures.MathIsNaN, IsNaN),
                "pow" => new BuiltinCallable(LythonKnownCallableSignatures.MathPow, Pow),
                "hypot" => new BuiltinCallable(LythonKnownCallableSignatures.MathHypot, Hypot),
                "fmod" => new BuiltinCallable(LythonKnownCallableSignatures.MathFmod, Fmod),
                "copysign" => new BuiltinCallable(LythonKnownCallableSignatures.MathCopySign, CopySign),
                "isclose" => new BuiltinCallable(LythonKnownCallableSignatures.MathIsClose, IsClose),
                "prod" => new BuiltinCallable(LythonKnownCallableSignatures.MathProd, Prod),
                "fsum" => new BuiltinCallable(LythonKnownCallableSignatures.MathFsum, Fsum),
                "factorial" => new BuiltinCallable(LythonKnownCallableSignatures.MathFactorial, Factorial),
                "gcd" => new BuiltinCallable(LythonKnownCallableSignatures.MathGcd, Gcd),
                "lcm" => new BuiltinCallable(LythonKnownCallableSignatures.MathLcm, Lcm),
                "comb" => new BuiltinCallable(LythonKnownCallableSignatures.MathComb, Comb),
                "perm" => new BuiltinCallable(LythonKnownCallableSignatures.MathPerm, Perm),
                "isqrt" => new BuiltinCallable(LythonKnownCallableSignatures.MathIsqrt, ISqrt),
                "dist" => new BuiltinCallable(LythonKnownCallableSignatures.MathDist, Dist),
                "frexp" => new BuiltinCallable(LythonKnownCallableSignatures.MathFrexp, Frexp),
                "ldexp" => new BuiltinCallable(LythonKnownCallableSignatures.MathLdexp, Ldexp),
                "modf" => new BuiltinCallable(LythonKnownCallableSignatures.MathModf, Modf),
                "remainder" => new BuiltinCallable(LythonKnownCallableSignatures.MathRemainder, Remainder),
                "nextafter" => new BuiltinCallable(LythonKnownCallableSignatures.MathNextAfter, NextAfter),
                "ulp" => new BuiltinCallable(LythonKnownCallableSignatures.MathUlp, Ulp),
                "exp2" => new BuiltinCallable(LythonKnownCallableSignatures.MathExp2, Exp2),
                "expm1" => new BuiltinCallable(LythonKnownCallableSignatures.MathExpm1, Expm1),
                "log1p" => new BuiltinCallable(LythonKnownCallableSignatures.MathLog1p, Log1p),
                "cbrt" => new BuiltinCallable(LythonKnownCallableSignatures.MathCbrt, Cbrt),
                "erf" => new BuiltinCallable(LythonKnownCallableSignatures.MathErf, Erf),
                "erfc" => new BuiltinCallable(LythonKnownCallableSignatures.MathErfc, Erfc),
                "gamma" => new BuiltinCallable(LythonKnownCallableSignatures.MathGamma, Gamma),
                "lgamma" => new BuiltinCallable(LythonKnownCallableSignatures.MathLgamma, LGamma),
                "fma" => new BuiltinCallable(LythonKnownCallableSignatures.MathFma, Fma),
                "sumprod" => new BuiltinCallable(LythonKnownCallableSignatures.MathSumProd, SumProd),
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
            => UnaryCheckedFloat(arguments, "math.exp", span, context, Math.Exp);

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
            => UnaryCheckedFloat(arguments, "math.sinh", span, context, Math.Sinh);

        private static object Cosh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.cosh", span, context, Math.Cosh);

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
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.pow", span, context);
            if (x == 0.0 && y < 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            var result = Math.Pow(x, y);
            if (double.IsNaN(result) && !double.IsNaN(x) && !double.IsNaN(y))
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return CheckedMathResult(result, "math.pow", span, x, y);
        }

        private static object Hypot(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length == 0)
            {
                return 0.0;
            }

            var max = 0.0;
            var values = new double[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                var value = Math.Abs(ExpectReal(arguments[i], "math.hypot", span));
                if (double.IsPositiveInfinity(value))
                {
                    return double.PositiveInfinity;
                }

                values[i] = value;
                max = Math.Max(max, value);
            }

            if (max == 0.0 || double.IsNaN(max))
            {
                return max;
            }

            var scaled = 0.0;
            foreach (var value in values)
            {
                var ratio = value / max;
                scaled += ratio * ratio;
            }

            return max * Math.Sqrt(scaled);
        }

        private static object Fmod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.fmod", span, context);
            if (y == 0.0 || double.IsInfinity(x))
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

        private static object UnaryCheckedFloat(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double> func)
        {
            var value = ExpectUnaryReal(arguments, owner, span, context);
            return CheckedMathResult(func(value), owner, span, value);
        }

        private static double CheckedMathResult(double result, string owner, LythonSourceSpan span, params double[] inputs)
        {
            if (double.IsInfinity(result) && inputs.All(double.IsFinite))
            {
                throw new LythonRuntimeException("OverflowError", "math range error", span);
            }

            return result;
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

            return FloatToInteger(number.Floating, owner, span, func);
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
                "UTC" => PyTimezone.Utc,
                "timedelta" => PyDateTimeOps.TimedeltaType,
                "date" => PyDateTimeOps.DateType,
                "time" => PyDateTimeOps.TimeType,
                "datetime" => PyDateTimeOps.DateTimeType,
                "tzinfo" => PyDateTimeOps.TzInfoType,
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
                "purge" => new BuiltinCallable(LythonKnownCallableSignatures.RePurge, Purge),
                "error" => new ExceptionTypeValue("error"),
                "PatternError" => new ExceptionTypeValue("PatternError"),
                "RegexFlag" => new RegexFlagFactory(),
                "NOFLAG" => BigInteger.Zero,
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
                "ASCII" => new BigInteger((int)PythonReCompileOptions.Ascii),
                "A" => new BigInteger((int)PythonReCompileOptions.Ascii),
                "LOCALE" => new BigInteger((int)PythonReCompileOptions.Locale),
                "L" => new BigInteger((int)PythonReCompileOptions.Locale),
                "DEBUG" => new BigInteger(RegexDebugFlag),
                "Pattern" => PyString.FromString("re.Pattern"),
                "Match" => PyString.FromString("re.Match"),
                _ => null!,
            };

            return value is not null;
        }

        private const int RegexDebugFlag = 1 << 20;

        private sealed class RegexFlagFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
        {
            public string Name => "re.RegexFlag";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var bound = CallBinder.BindNamedArguments(arguments, span, new LythonCallableSignature("re.RegexFlag", ["value"], RequiredCount: 0), "Builtin");
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
            return new ReFindAllResult(RePatternMembers.ProjectFindAllResult(pattern.Regex.FindAllToUtf8(range.Segment.Utf8Bytes.Span), span, context));
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
                var (captureSlotCount, namedGroups) = SummarizePatternGroups(pattern.AsString());
                return new RePatternObject(pattern, options, compiled, captureSlotCount, namedGroups);
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
            if ((flagBits & RegexDebugFlag) != 0)
            {
                throw new LythonRuntimeException("NotImplementedError", "re.DEBUG is not supported by Lython's regex runtime.", span);
            }

            var options = (PythonReCompileOptions)flagBits;
            if ((options & PythonReCompileOptions.Locale) != 0)
            {
                throw new LythonRuntimeException("NotImplementedError", "re.LOCALE is not supported by Lython's Unicode-only regex runtime.", span);
            }

            return options;
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
                () => [new CallArgumentValue(null, matchObject)]);

            if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
            {
                throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", state.Span);
            }

            return replacementText.AsString();
        }

        internal static ReMatchObject CreateMatchObject(
            RePatternObject pattern,
            RegexSubjectRange range,
            Utf8PythonDetailedMatchData match,
            ExecutionContext? context = null,
            LythonSourceSpan? span = null)
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

        private static (int CaptureSlotCount, IReadOnlyDictionary<string, int> NamedGroups) SummarizePatternGroups(string pattern)
        {
            var count = 1;
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            var inClass = false;
            for (var i = 0; i < pattern.Length; i++)
            {
                var ch = pattern[i];
                if (ch == '\\')
                {
                    i++;
                    continue;
                }

                if (ch == '[')
                {
                    inClass = true;
                    continue;
                }

                if (ch == ']' && inClass)
                {
                    inClass = false;
                    continue;
                }

                if (inClass || ch != '(')
                {
                    continue;
                }

                if (i + 1 >= pattern.Length || pattern[i + 1] != '?')
                {
                    count++;
                    continue;
                }

                if (i + 3 < pattern.Length && pattern[i + 2] == 'P' && pattern[i + 3] == '<')
                {
                    var end = pattern.IndexOf('>', i + 4);
                    if (end < 0)
                    {
                        continue;
                    }

                    var name = pattern[(i + 4)..end];
                    names[name] = count;
                    count++;
                    i = end;
                    continue;
                }

                if (i + 2 < pattern.Length && pattern[i + 2] is ':' or '=' or '!')
                {
                    continue;
                }

                if (i + 3 < pattern.Length && pattern[i + 2] == '<' && pattern[i + 3] is '=' or '!')
                {
                    continue;
                }

                if (TrySkipInlineRegexFlags(pattern, i + 2, out var endIndex))
                {
                    i = endIndex;
                    continue;
                }
            }

            return (count, names.Count == 0 ? EmptyRegexNamedGroups : names);
        }

        private static bool TrySkipInlineRegexFlags(string pattern, int start, out int endIndex)
        {
            var i = start;
            while (i < pattern.Length && (char.IsLetter(pattern[i]) || pattern[i] == '-'))
            {
                i++;
            }

            if (i == start || i >= pattern.Length)
            {
                endIndex = start;
                return false;
            }

            if (pattern[i] is ')' or ':')
            {
                endIndex = i;
                return true;
            }

            endIndex = start;
            return false;
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
            _matches = range.IsValid
                ? pattern.Regex.FindIterDetailed(range.Segment.Utf8Bytes.Span).GetEnumerator()
                : null;
        }

        public override bool TryMoveNext(out object value)
        {
            if (_matches is null || !_matches.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _context.CheckExecutionBudget(_span);
            value = ReModule.CreateMatchObject(_pattern, _range, (Utf8PythonDetailedMatchData)_matches.Current!, _context, _span);
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
        public static bool TryGetMember(ReMatchObject match, string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        private static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template, ExecutionContext context, LythonSourceSpan span)
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

    private sealed class SysModule : PyModule
    {
        private const int VersionMajor = 3;
        private const int VersionMinor = 11;
        private const int VersionMicro = 0;
        private const int HexVersion = (VersionMajor << 24) | (VersionMinor << 16) | (VersionMicro << 8) | 0xF0;

        private readonly ExecutionContext _context;
        private readonly PyList _argv;
        private readonly HostTextInputHandle _stdin;
        private readonly HostTextOutputHandle _stdout;
        private readonly HostTextOutputHandle _stderr;

        public SysModule(ExecutionContext context) : base("sys")
        {
            _context = context;
            var state = context.State;
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
                "version" => PyString.FromString("3.11.0 (Lython)"),
                "version_info" => VersionInfo(_context),
                "hexversion" => new BigInteger(HexVersion),
                "implementation" => new SysImplementationObject(VersionInfo(_context), new BigInteger(HexVersion)),
                "platform" => PyString.FromString("lython"),
                "maxsize" => new BigInteger(long.MaxValue),
                "byteorder" => PyString.FromString("little"),
                "prefix" => PyString.FromString(_context.Host.Cwd),
                "base_prefix" => PyString.FromString(_context.Host.Cwd),
                "executable" => PyString.FromString("lython"),
                "path" => new PyList([PyString.FromString(ContainedImportBaseDirectory(_context))], _context.MemoryGovernor),
                "modules" => CreateModulesSnapshot(_context),
                "builtin_module_names" => CreateBuiltinModuleNames(_context),
                "stdlib_module_names" => new PySet(EnumerateDiscoverableBuiltinModuleNames(_context).Order(StringComparer.Ordinal).Select(PyString.FromString), _context.MemoryGovernor),
                "exit" => new BuiltinCallable(LythonKnownCallableSignatures.SysExit, Exit),
                "getdefaultencoding" => new BuiltinCallable(LythonKnownCallableSignatures.SysGetDefaultEncoding, GetDefaultEncoding),
                "exc_info" => new BuiltinCallable(LythonKnownCallableSignatures.SysExcInfo, ExcInfo),
                "getsizeof" => new BuiltinCallable(LythonKnownCallableSignatures.SysGetSizeOf, GetSizeOf),
                "settrace" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetTrace, "sys.settrace(...) is not supported by Lython."),
                "setprofile" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetProfile, "sys.setprofile(...) is not supported by Lython."),
                "setrecursionlimit" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetRecursionLimit, "sys.setrecursionlimit(...) is not supported by Lython; use LythonRunOptions.MaxRecursionDepth."),
                "addaudithook" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysAddAuditHook, "sys.addaudithook(...) is not supported by Lython."),
                "audit" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysAudit, "sys.audit(...) is not supported by Lython."),
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

        private static PyTuple VersionInfo(ExecutionContext context)
            => new(
                [
                    new BigInteger(VersionMajor),
                    new BigInteger(VersionMinor),
                    new BigInteger(VersionMicro),
                    PyString.FromString("final"),
                    BigInteger.Zero
                ],
                context.MemoryGovernor);

        private static object GetDefaultEncoding(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "sys.getdefaultencoding() expects no arguments.", span);
            }

            return PyString.FromString("utf-8");
        }

        private static object ExcInfo(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "sys.exc_info() expects no arguments.", span);
            }

            return context.Services.CurrentException is { } exception
                ? new PyTuple(
                    [
                        new ExceptionTypeValue(exception.TypeName),
                        exception,
                        PyNone.Instance
                    ],
                    context.MemoryGovernor,
                    span)
                : new PyTuple([PyNone.Instance, PyNone.Instance, PyNone.Instance], context.MemoryGovernor, span);
        }

        private static object GetSizeOf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = span;
            _ = context;
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "sys.getsizeof(object[, default]) expects one or two arguments.", span);
            }

            if (TryEstimateSize(arguments[0], out var size))
            {
                return new BigInteger(size);
            }

            return arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        }

        private static bool TryEstimateSize(object value, out long size)
        {
            size = value switch
            {
                PyNone => 16,
                bool => 16,
                BigInteger integer => RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer),
                double => 24,
                PyString text => 40 + text.Utf8Bytes.Length,
                string text => 40 + global::System.Text.Encoding.UTF8.GetByteCount(text),
                PyBytes bytes => 40 + bytes.Length,
                PyPath path => 40 + path.Value.Utf8Bytes.Length,
                PyList list => 32 + (16L * list.Count),
                PyTuple tuple => PyTuple.EstimateApproximateBytes(tuple.Count),
                PyDict dict => 64 + (32L * dict.Count),
                PySet set => 80 + (24L * set.Count),
                PyException => 64,
                PyModule => 64,
                HostTextInputHandle or HostTextOutputHandle => 32,
                _ => 0
            };

            return size > 0;
        }

        private static BuiltinCallable UnsupportedSysCallable(LythonCallableSignature signature, string message)
            => new(signature, (_, span, _) => throw new LythonRuntimeException("NotImplementedError", message, span));

        private static string ContainedImportBaseDirectory(ExecutionContext context)
            => context.SourcePath is null ? context.Host.Cwd : PathOps.Parent(context.SourcePath);

        private static PyTuple CreateBuiltinModuleNames(ExecutionContext context)
            => new(
                EnumerateDiscoverableBuiltinModuleNames(context)
                    .Order(StringComparer.Ordinal)
                    .Select(PyString.FromString)
                    .ToArray(),
                context.MemoryGovernor);

        private static PyDict CreateModulesSnapshot(ExecutionContext context)
        {
            var modules = new PyDict(context.MemoryGovernor);
            foreach (var name in EnumerateDiscoverableBuiltinModuleNames(context).Order(StringComparer.Ordinal))
            {
                var module = ResolveBuiltinModule(name, context);
                if (module is not null)
                {
                    modules.SetItem(PyString.FromString(name), module);
                }
            }

            foreach (var pair in context.State.ImportedModules.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                modules.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            return modules;
        }
    }

    private sealed class SysImplementationObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly PyTuple _version;
        private readonly BigInteger _hexversion;

        public SysImplementationObject(PyTuple version, BigInteger hexversion)
        {
            _version = version;
            _hexversion = hexversion;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString("lython"),
                "version" => _version,
                "hexversion" => _hexversion,
                "cache_tag" => PyString.FromString("lython-3.11"),
                _ => null!
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("namespace(name='lython', cache_tag='lython-3.11')");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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
                value = PyDataclass.DataclassCallable;
                return true;
            }

            if (name == "Field")
            {
                value = PyDataclass.FieldType;
                return true;
            }

            if (name == "field")
            {
                value = PyDataclass.FieldCallable;
                return true;
            }

            if (name == "make_dataclass")
            {
                value = PyDataclass.MakeDataclassCallable;
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

        public override IReadOnlyList<string> ExportedNames => PyTyping.ExportedNames;

        public override bool TryGetMember(string name, out object value)
            => PyTyping.TryGetMember(name, out value);
    }

    internal static class RePatternMembers
    {
        public static bool TryGetMember(RePatternObject pattern, string name, out object value)
        {
            value = name switch
            {
                "pattern" => pattern.Pattern,
                "flags" => new BigInteger((int)pattern.Options),
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
                _ => null!,
            };

            return value is not null;
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
            return new ReFindAllResult(ProjectFindAllResult(pattern.Regex.FindAllToUtf8(range.Segment.Utf8Bytes.Span), span, context));
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

    private sealed class ArgparseModule : PyModule
    {
        public static readonly ArgparseModule Instance = new();

        private static readonly IReadOnlyDictionary<string, ArgparseFormatterClass> FormatterClasses =
            new Dictionary<string, ArgparseFormatterClass>(StringComparer.Ordinal)
            {
                ["HelpFormatter"] = new("HelpFormatter"),
                ["RawDescriptionHelpFormatter"] = new("RawDescriptionHelpFormatter"),
                ["RawTextHelpFormatter"] = new("RawTextHelpFormatter"),
                ["ArgumentDefaultsHelpFormatter"] = new("ArgumentDefaultsHelpFormatter"),
            };

        private ArgparseModule() : base("argparse")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ArgumentParser" => new ArgumentParserFactory(),
                "Namespace" => new ArgparseNamespaceFactory(),
                "FileType" => new BuiltinCallable(LythonKnownCallableSignatures.ArgparseFileType, CreateFileType),
                "ArgumentError" => new ExceptionTypeValue("ArgumentError"),
                "ArgumentTypeError" => new ExceptionTypeValue("ArgumentTypeError"),
                "SUPPRESS" => ArgparseSuppressValue.Instance,
                "OPTIONAL" => PyString.FromString("?"),
                "ZERO_OR_MORE" => PyString.FromString("*"),
                "ONE_OR_MORE" => PyString.FromString("+"),
                "PARSER" => PyString.FromString("A..."),
                "REMAINDER" => PyString.FromString("..."),
                "HelpFormatter" => FormatterClasses["HelpFormatter"],
                "RawDescriptionHelpFormatter" => FormatterClasses["RawDescriptionHelpFormatter"],
                "RawTextHelpFormatter" => FormatterClasses["RawTextHelpFormatter"],
                "ArgumentDefaultsHelpFormatter" => FormatterClasses["ArgumentDefaultsHelpFormatter"],
                _ => null!
            };

            return value is not null;
        }

        private static object CreateParser(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = new ArgparseParserOptions(DefaultProgramName(context));
            var positionalIndex = 0;
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex >= ArgparseParserOptions.SupportedConstructorParameters.Length)
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser(...) received too many positional arguments.", span);
                    }

                    AssignParserOption(options, ArgparseParserOptions.SupportedConstructorParameters[positionalIndex++], argument.Value, assigned, span);
                    continue;
                }

                if (argument.Name is "fromfile_prefix_chars" or "parents" or "conflict_handler" or "prefix_chars" or "argument_default")
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        $"argparse.ArgumentParser(..., {argument.Name}=...) is not supported by Lython.",
                        span);
                }

                if (!ArgparseParserOptions.SupportedConstructorParameters.Contains(argument.Name, StringComparer.Ordinal))
                {
                    throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                AssignParserOption(options, argument.Name, argument.Value, assigned, span);
            }

            return new ArgumentParserObject(options);
        }

        private static void AssignParserOption(
            ArgparseParserOptions options,
            string name,
            object value,
            HashSet<string> assigned,
            LythonSourceSpan span)
        {
            if (!assigned.Add(name))
            {
                throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser(...) got multiple values for argument '{name}'.", span);
            }

            switch (name)
            {
                case "prog":
                    options.Prog = OptionalString(value, "prog", "argparse.ArgumentParser", span) ?? options.Prog;
                    break;
                case "usage":
                    options.Usage = OptionalString(value, "usage", "argparse.ArgumentParser", span);
                    break;
                case "description":
                    options.Description = OptionalString(value, "description", "argparse.ArgumentParser", span);
                    break;
                case "epilog":
                    options.Epilog = OptionalString(value, "epilog", "argparse.ArgumentParser", span);
                    break;
                case "formatter_class":
                    options.FormatterClass = ReferenceEquals(value, PyNone.Instance) ? null : value;
                    break;
                case "add_help":
                    options.AddHelp = RequireBool(value, "add_help", "argparse.ArgumentParser", span);
                    break;
                case "allow_abbrev":
                    options.AllowAbbrev = RequireBool(value, "allow_abbrev", "argparse.ArgumentParser", span);
                    break;
                case "exit_on_error":
                    options.ExitOnError = RequireBool(value, "exit_on_error", "argparse.ArgumentParser", span);
                    break;
            }
        }

        private static object CreateFileType(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var mode = arguments.Length >= 1 && !ReferenceEquals(arguments[0], PyNone.Instance)
                ? RequireStringValue(arguments[0], "mode", "argparse.FileType", span).AsString()
                : "r";
            if (mode.Contains('b'))
            {
                throw new LythonRuntimeException("ValueError", "argparse.FileType only supports host-mediated UTF-8 text modes.", span);
            }

            mode = ParseTextOpenMode(PyString.FromString(mode), "argparse.FileType", span);
            if (arguments.Length >= 2)
            {
                ValidateTextBuffering(arguments[1], "argparse.FileType", span);
            }

            if (mode is not ("r" or "w" or "a"))
            {
                throw new LythonRuntimeException("ValueError", "argparse.FileType only supports modes 'r', 'w', and 'a'.", span);
            }

            var encodingMode = arguments.Length >= 3
                ? ParseTextEncoding(arguments[2], "argparse.FileType", span)
                : TextEncodingMode.Utf8;
            var errorsMode = arguments.Length >= 4
                ? ParseTextErrors(arguments[3], "argparse.FileType", span)
                : TextErrorMode.Strict;
            var encoding = arguments.Length >= 3 ? (encodingMode == TextEncodingMode.Utf8Bom ? "utf-8-sig" : "utf-8") : null;
            var errors = arguments.Length >= 4
                ? errorsMode switch
                {
                    TextErrorMode.Ignore => "ignore",
                    TextErrorMode.Replace => "replace",
                    TextErrorMode.BackslashReplace => "backslashreplace",
                    _ => "strict"
                }
                : null;
            return new ArgparseFileTypeObject(mode, encoding, errors);
        }

        private static PyString RequireStringValue(object value, string name, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
            }

            return text;
        }

        private static string? OptionalString(object value, string name, string owner, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            return RequireStringValue(value, name, owner, span).AsString();
        }

        private static bool RequireBool(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is bool boolean)
            {
                return boolean;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a bool.", span);
        }

        private static string DefaultProgramName(ExecutionContext context)
        {
            if (context.SourcePath is not null)
            {
                var normalized = context.SourcePath.Replace('\\', '/');
                var slash = normalized.LastIndexOf('/');
                return slash >= 0 ? normalized[(slash + 1)..] : normalized;
            }

            return "lython";
        }

        private sealed class ArgumentParserFactory : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                return CreateParser(arguments, span, context);
            }
        }

        private sealed class ArgparseNamespaceFactory : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var members = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var argument in arguments)
                {
                    if (argument.Name is null)
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.Namespace(...) accepts keyword arguments only.", span);
                    }

                    members[argument.Name] = argument.Value;
                }

                return new ArgparseNamespaceObject(members);
            }
        }
    }

    internal sealed class ArgparseParserOptions
    {
        public static readonly string[] SupportedConstructorParameters =
        [
            "prog",
            "usage",
            "description",
            "epilog",
            "formatter_class",
            "add_help",
            "allow_abbrev",
            "exit_on_error",
        ];

        public ArgparseParserOptions(string prog)
        {
            Prog = prog;
        }

        public string Prog { get; set; }

        public string? Usage { get; set; }

        public string? Description { get; set; }

        public string? Epilog { get; set; }

        public object? FormatterClass { get; set; }

        public bool AddHelp { get; set; } = true;

        public bool AllowAbbrev { get; set; } = true;

        public bool ExitOnError { get; set; } = true;
    }

    internal sealed class ArgumentParserObject
    {
        private readonly List<ArgumentSpec> _arguments = [];
        private readonly List<ArgparseMutuallyExclusiveGroupObject> _groups = [];
        private readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
        private readonly ArgparseParserOptions _options;
        private int _nextGroupId;

        public ArgumentParserObject(ArgparseParserOptions options)
        {
            _options = options;
            if (_options.AddHelp)
            {
                _arguments.Add(new ArgumentSpec(
                    ["-h", "--help"],
                    "help",
                    "help",
                    Required: false,
                    DefaultValue: ArgparseSuppressValue.Instance,
                    Choices: null,
                    Converter: null,
                    IsPositional: false,
                    Nargs: null,
                    GroupId: null,
                    ConstValue: PyNone.Instance,
                    HelpText: "show this help message and exit",
                    Metavar: null,
                    VersionText: null,
                    SuppressHelp: false));
            }
        }

        public PyString? Description => _options.Description is null ? null : PyString.FromString(_options.Description);

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable("argparse.ArgumentParser.add_argument", AddArgument),
                "add_mutually_exclusive_group" => new CustomMethodCallable("argparse.ArgumentParser.add_mutually_exclusive_group", AddMutuallyExclusiveGroup),
                "parse_args" => new CustomMethodCallable("argparse.ArgumentParser.parse_args", ParseArgs),
                "parse_known_args" => new CustomMethodCallable("argparse.ArgumentParser.parse_known_args", ParseKnownArgs),
                "format_usage" => new CustomMethodCallable("argparse.ArgumentParser.format_usage", FormatUsage),
                "format_help" => new CustomMethodCallable("argparse.ArgumentParser.format_help", FormatHelp),
                "print_usage" => new CustomMethodCallable("argparse.ArgumentParser.print_usage", PrintUsage),
                "print_help" => new CustomMethodCallable("argparse.ArgumentParser.print_help", PrintHelp),
                "error" => new CustomMethodCallable("argparse.ArgumentParser.error", Error),
                "exit" => new CustomMethodCallable("argparse.ArgumentParser.exit", Exit),
                "set_defaults" => new CustomMethodCallable("argparse.ArgumentParser.set_defaults", SetDefaults),
                "get_default" => new CustomMethodCallable("argparse.ArgumentParser.get_default", GetDefault),
                "add_subparsers" => new CustomMethodCallable("argparse.ArgumentParser.add_subparsers", AddSubparsers),
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
            var result = ParseArguments(arguments, "argparse.ArgumentParser.parse_args", collectUnknown: false, span, context);
            if (result.Unknown.Count != 0)
            {
                throw CreateParseFailure($"unrecognized arguments: {JoinUnknownArguments(result.Unknown)}", span);
            }

            return result.Namespace;
        }

        private object ParseKnownArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var result = ParseArguments(arguments, "argparse.ArgumentParser.parse_known_args", collectUnknown: true, span, context);
            return new PyTuple(
                [
                    result.Namespace,
                    new PyList(result.Unknown.Select(static item => (object)PyString.FromString(item)), context.MemoryGovernor, span)
                ],
                context.MemoryGovernor,
                span);
        }

        private ParseResult ParseArguments(
            CallArgumentValue[] arguments,
            string methodName,
            bool collectUnknown,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var (argv, namespaceObject) = ResolveParseInvocation(arguments, methodName, span, context);
            var values = namespaceObject is null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(namespaceObject.Members, StringComparer.Ordinal);
            var seenSpecs = new HashSet<ArgumentSpec>();
            var unknown = new List<string>();

            ApplyDefaults(values, context, span);

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
                if (token == "--")
                {
                    index++;
                    while (index < argv.Count)
                    {
                        ConsumePositional(argv, ref index, positionalSpecs, ref positionalIndex, seenSpecs, values, unknown, collectUnknown, span, context);
                    }

                    break;
                }

                if (TryExpandShortFlagCluster(token, out var clusterSpecs))
                {
                    foreach (var clusterSpec in clusterSpecs)
                    {
                        ApplyNoValueOptional(clusterSpec, token, seenSpecs, values, span, context);
                    }

                    continue;
                }

                var optionalSpec = FindOptionalArgument(token, out var inlineValue, out var optionError);
                if (optionError is not null)
                {
                    throw CreateParseFailure(optionError, span);
                }

                if (optionalSpec is not null)
                {
                    ApplyOptional(optionalSpec, token, inlineValue, argv, ref index, seenSpecs, values, span, context);
                    continue;
                }

                if (LooksLikeOptionalToken(token))
                {
                    if (collectUnknown)
                    {
                        unknown.Add(token);
                        continue;
                    }

                    throw CreateParseFailure($"unrecognized arguments: {token}", span);
                }

                ConsumePositional(argv, ref index, positionalSpecs, ref positionalIndex, seenSpecs, values, unknown, collectUnknown, span, context);
            }

            ValidateRequiredArguments(seenSpecs, values, span);
            ValidateMutuallyExclusiveGroups(seenSpecs, span);
            var resultNamespace = namespaceObject ?? new ArgparseNamespaceObject(new Dictionary<string, object>(StringComparer.Ordinal));
            resultNamespace.ReplaceMembers(values);
            return new ParseResult(resultNamespace, unknown);
        }

        private (List<string> Argv, ArgparseNamespaceObject? Namespace) ResolveParseInvocation(
            CallArgumentValue[] arguments,
            string methodName,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            object? argsValue = ArgparseUnspecifiedValue.Instance;
            object? namespaceValue = ArgparseUnspecifiedValue.Instance;
            var positionalIndex = 0;
            var assignedArgs = false;
            var assignedNamespace = false;

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex == 0)
                    {
                        if (assignedArgs)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'args'.", span);
                        }

                        argsValue = argument.Value;
                        assignedArgs = true;
                    }
                    else if (positionalIndex == 1)
                    {
                        if (assignedNamespace)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'namespace'.", span);
                        }

                        namespaceValue = argument.Value;
                        assignedNamespace = true;
                    }
                    else
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}([args][, namespace]) expects zero to two arguments.", span);
                    }

                    positionalIndex++;
                    continue;
                }

                switch (argument.Name)
                {
                    case "args":
                        if (assignedArgs)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'args'.", span);
                        }

                        argsValue = argument.Value;
                        assignedArgs = true;
                        break;
                    case "namespace":
                        if (assignedNamespace)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'namespace'.", span);
                        }

                        namespaceValue = argument.Value;
                        assignedNamespace = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }
            }

            var argv = ReferenceEquals(argsValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(argsValue, PyNone.Instance)
                ? context.State.Args.Select(static item => item.AsString()).ToList()
                : ToStringList(argsValue!, $"{methodName}(args) expects an iterable of strings.", span);

            var namespaceObject = ReferenceEquals(namespaceValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(namespaceValue, PyNone.Instance)
                ? null
                : namespaceValue as ArgparseNamespaceObject ??
                  throw new LythonRuntimeException("TypeError", $"{methodName}(namespace) expects an argparse.Namespace instance.", span);

            return (argv, namespaceObject);
        }

        private void ApplyDefaults(Dictionary<string, object> values, ExecutionContext context, LythonSourceSpan span)
        {
            foreach (var spec in _arguments)
            {
                var defaultValue = _defaults.TryGetValue(spec.Destination, out var parserDefault)
                    ? parserDefault
                    : spec.DefaultValue;
                if (IsSuppress(defaultValue) || values.ContainsKey(spec.Destination))
                {
                    continue;
                }

                values[spec.Destination] = CloneDefault(defaultValue, context, span);
            }

            foreach (var pair in _defaults)
            {
                if (!values.ContainsKey(pair.Key) && !IsSuppress(pair.Value))
                {
                    values[pair.Key] = CloneDefault(pair.Value, context, span);
                }
            }
        }

        private void ConsumePositional(
            List<string> argv,
            ref int index,
            IReadOnlyList<ArgumentSpec> positionalSpecs,
            ref int positionalIndex,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            List<string> unknown,
            bool collectUnknown,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (positionalIndex >= positionalSpecs.Count)
            {
                if (collectUnknown)
                {
                    unknown.Add(argv[index]);
                    return;
                }

                throw CreateParseFailure($"unrecognized arguments: {argv[index]}", span);
            }

            var spec = positionalSpecs[positionalIndex];
            var tokens = CollectPositionalTokens(spec, argv, ref index, positionalSpecs, positionalIndex, span);
            if (tokens.Count == 0)
            {
                return;
            }

            StoreParsedValue(spec, CreateParsedValue(spec, tokens, spec.Destination, span, context), seenSpecs, values, context, span);
            positionalIndex++;
        }

        private List<string> CollectPositionalTokens(
            ArgumentSpec spec,
            IReadOnlyList<string> argv,
            ref int index,
            IReadOnlyList<ArgumentSpec> positionalSpecs,
            int positionalIndex,
            LythonSourceSpan span)
        {
            var tokens = new List<string>();
            var fixedCount = FixedNargsCount(spec.Nargs);
            if (fixedCount is not null)
            {
                for (var i = 0; i < fixedCount.Value; i++)
                {
                    if (index >= argv.Count || HasOptionalArgumentNamed(argv[index]))
                    {
                        throw CreateParseFailure($"argument {spec.Destination}: expected {fixedCount.Value} arguments", span);
                    }

                    tokens.Add(argv[index]);
                    if (i + 1 < fixedCount.Value)
                    {
                        index++;
                    }
                }

                return tokens;
            }

            switch (spec.Nargs)
            {
                case null:
                case "?":
                    tokens.Add(argv[index]);
                    return tokens;
                case "*" or "+":
                    var requiredAfter = RequiredPositionalSlotsAfter(positionalSpecs, positionalIndex);
                    while (index < argv.Count &&
                           !HasOptionalArgumentNamed(argv[index]) &&
                           CountRemainingPositionalCandidates(argv, index) > requiredAfter)
                    {
                        tokens.Add(argv[index]);
                        if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            break;
                        }

                        index++;
                    }

                    if (spec.Nargs == "+" && tokens.Count == 0)
                    {
                        throw CreateParseFailure($"the following arguments are required: {spec.Destination}", span);
                    }

                    return tokens;
                default:
                    throw new InvalidOperationException("Unsupported argparse nargs shape.");
            }
        }

        private void ApplyOptional(
            ArgumentSpec spec,
            string optionToken,
            string? inlineValue,
            IReadOnlyList<string> argv,
            ref int index,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (IsNoValueAction(spec.Action))
            {
                if (inlineValue is not null)
                {
                    throw CreateParseFailure($"argument {optionToken}: ignored explicit argument '{inlineValue}'", span);
                }

                ApplyNoValueOptional(spec, optionToken, seenSpecs, values, span, context);
                return;
            }

            var tokens = CollectOptionalTokens(spec, optionToken, inlineValue, argv, ref index, span);
            StoreParsedValue(spec, CreateParsedValue(spec, tokens, optionToken, span, context), seenSpecs, values, context, span);
        }

        private void ApplyNoValueOptional(
            ArgumentSpec spec,
            string optionToken,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            switch (spec.Action)
            {
                case "store_true":
                    values[spec.Destination] = true;
                    break;
                case "store_false":
                    values[spec.Destination] = false;
                    break;
                case "store_const":
                    values[spec.Destination] = CloneDefault(spec.ConstValue, context, span);
                    break;
                case "count":
                    values[spec.Destination] = IncrementCount(values.TryGetValue(spec.Destination, out var current) ? current : PyNone.Instance);
                    break;
                case "help":
                    WriteToTarget(FormatHelpText(context), context.State.Stdout, span);
                    throw CreateSystemExit(string.Empty, span, status: 0);
                case "version":
                    var versionText = spec.VersionText ?? string.Empty;
                    if (!versionText.EndsWith('\n'))
                    {
                        versionText += "\n";
                    }

                    WriteToTarget(PyString.FromString(ApplyFormatSubstitutions(versionText, spec, context)), context.State.Stdout, span);
                    throw CreateSystemExit(string.Empty, span, status: 0);
                default:
                    throw new InvalidOperationException("Unsupported no-value argparse action.");
            }

            seenSpecs.Add(spec);
        }

        private List<string> CollectOptionalTokens(
            ArgumentSpec spec,
            string optionToken,
            string? inlineValue,
            IReadOnlyList<string> argv,
            ref int index,
            LythonSourceSpan span)
        {
            var tokens = new List<string>();
            if (inlineValue is not null)
            {
                tokens.Add(inlineValue);
            }

            var fixedCount = FixedNargsCount(spec.Nargs);
            if (fixedCount is not null)
            {
                while (tokens.Count < fixedCount.Value)
                {
                    if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                    {
                        throw CreateParseFailure($"argument {optionToken}: expected {fixedCount.Value} arguments", span);
                    }

                    index++;
                    tokens.Add(argv[index]);
                }

                return tokens;
            }

            switch (spec.Nargs)
            {
                case null:
                    if (tokens.Count == 0)
                    {
                        if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            throw CreateParseFailure($"argument {optionToken}: expected one argument", span);
                        }

                        index++;
                        tokens.Add(argv[index]);
                    }

                    return tokens;
                case "?":
                    if (tokens.Count == 0)
                    {
                        if (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            index++;
                            tokens.Add(argv[index]);
                        }
                    }

                    return tokens;
                case "*" or "+":
                    while (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                    {
                        index++;
                        tokens.Add(argv[index]);
                    }

                    if (spec.Nargs == "+" && tokens.Count == 0)
                    {
                        throw CreateParseFailure($"argument {optionToken}: expected at least one argument", span);
                    }

                    return tokens;
                default:
                    throw new InvalidOperationException("Unsupported argparse nargs shape.");
            }
        }

        private object CreateParsedValue(ArgumentSpec spec, IReadOnlyList<string> tokens, string displayName, LythonSourceSpan span, ExecutionContext context)
        {
            if (spec.Nargs == "?" && tokens.Count == 0)
            {
                return ReferenceEquals(spec.ConstValue, PyNone.Instance)
                    ? CloneDefault(spec.DefaultValue, context, span)
                    : CloneDefault(spec.ConstValue, context, span);
            }

            if (ProducesListValue(spec))
            {
                var values = new PyList([], context.MemoryGovernor, span);
                foreach (var token in tokens)
                {
                    values.Add(ConvertArgumentValue(spec, displayName, token, span, context));
                }

                return values;
            }

            if (tokens.Count == 0)
            {
                return PyNone.Instance;
            }

            return ConvertArgumentValue(spec, displayName, tokens[0], span, context);
        }

        private void StoreParsedValue(
            ArgumentSpec spec,
            object parsed,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (spec.Action == "append")
            {
                var list = values.TryGetValue(spec.Destination, out var existing) && existing is PyList existingList
                    ? existingList
                    : new PyList([], context.MemoryGovernor, span);
                list.Add(parsed);
                values[spec.Destination] = list;
            }
            else
            {
                values[spec.Destination] = parsed;
            }

            seenSpecs.Add(spec);
        }

        private object ConvertArgumentValue(ArgumentSpec spec, string optionName, string token, LythonSourceSpan span, ExecutionContext context)
        {
            object converted = PyString.FromString(token);
            if (spec.Converter is ICallable callable)
            {
                try
                {
                    converted = RuntimeValue(callable.Invoke([new CallArgumentValue(null, converted)], span, context));
                }
                catch (LythonRuntimeException ex) when (ex.ExceptionType is "ArgumentTypeError" or "ValueError" or "TypeError")
                {
                    throw CreateParseFailure($"argument {optionName}: {ex.Message}", span);
                }
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
                    throw CreateParseFailure($"argument {optionName}: invalid choice: '{token}'", span);
                }
            }

            return converted;
        }

        private void ValidateRequiredArguments(HashSet<ArgumentSpec> seenSpecs, Dictionary<string, object> values, LythonSourceSpan span)
        {
            var missing = new List<string>();
            foreach (var spec in _arguments)
            {
                if (spec.Action is "help" or "version")
                {
                    continue;
                }

                if (spec.IsPositional)
                {
                    if (spec.Nargs is "*" or "?")
                    {
                        continue;
                    }

                    if (!seenSpecs.Contains(spec))
                    {
                        missing.Add(spec.Destination);
                    }

                    continue;
                }

                if (spec.Required && !seenSpecs.Contains(spec))
                {
                    missing.Add(spec.OptionNames.FirstOrDefault() ?? spec.Destination);
                }
            }

            if (missing.Count != 0)
            {
                throw CreateParseFailure($"the following arguments are required: {string.Join(", ", missing)}", span);
            }
        }

        private void ValidateMutuallyExclusiveGroups(HashSet<ArgumentSpec> seenSpecs, LythonSourceSpan span)
        {
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
                    throw CreateParseFailure("mutually exclusive arguments must not be used together", span);
                }

                if (group.Required && present == 0)
                {
                    throw CreateParseFailure("one of the mutually exclusive arguments is required", span);
                }
            }
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

        private static object DefaultForAction(string action, ExecutionContext context, LythonSourceSpan span)
        {
            return action switch
            {
                "store_true" => false,
                "store_false" => true,
                "append" => new PyList([], context.MemoryGovernor, span),
                "store_const" => PyNone.Instance,
                "count" => PyNone.Instance,
                "help" or "version" => ArgparseSuppressValue.Instance,
                _ => PyNone.Instance
            };
        }

        private static void ValidateAction(string action, LythonSourceSpan span)
        {
            if (action is "store" or "store_true" or "store_false" or "append" or "store_const" or "count" or "version")
            {
                return;
            }

            throw new LythonRuntimeException(
                "ValueError",
                "argparse.ArgumentParser.add_argument(..., action=...) only supports 'store', 'store_true', 'store_false', 'append', 'store_const', 'count', or 'version'.",
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

        private static string? ValidateNargs(object value, string action, LythonSourceSpan span)
        {
            if (IsNoValueAction(action))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) is not supported for no-value actions.",
                    span);
            }

            if (value is BigInteger integer)
            {
                if (integer > BigInteger.Zero && integer <= int.MaxValue)
                {
                    return integer.ToString(CultureInfo.InvariantCulture);
                }

                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                    span);
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                    span);
            }

            var nargs = text.AsString();
            if (nargs is "?" or "*" or "+")
            {
                return nargs;
            }

            if (int.TryParse(nargs, NumberStyles.None, CultureInfo.InvariantCulture, out var fixedCount) && fixedCount > 0)
            {
                return nargs;
            }

            throw new LythonRuntimeException(
                "TypeError",
                "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
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

            ValidateSupportedKeywords(
                keyword.Keys,
                ["dest", "action", "required", "default", "choices", "type", "nargs", "help", "const", "metavar", "version"],
                "argparse.ArgumentParser.add_argument",
                span);
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
            if (isPositional && keyword.ContainsKey("required"))
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(..., required=...) is not valid for positional arguments.", span);
            }

            var nargs = keyword.TryGetValue("nargs", out var nargsValue)
                ? ValidateNargs(nargsValue, action, span)
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
            var constValue = (action == "store_const" || nargs == "?") && keyword.TryGetValue("const", out var constant)
                ? constant
                : PyNone.Instance;
            var suppressHelp = keyword.TryGetValue("help", out var helpValue) && IsSuppress(helpValue);
            var helpText = keyword.TryGetValue("help", out helpValue)
                ? suppressHelp
                    ? null
                    : OptionalString(helpValue, "help", "argparse.ArgumentParser.add_argument", span)
                : null;
            var metavar = keyword.TryGetValue("metavar", out var metavarValue)
                ? OptionalString(metavarValue, "metavar", "argparse.ArgumentParser.add_argument", span)
                : null;
            var versionText = keyword.TryGetValue("version", out var versionValue)
                ? OptionalString(versionValue, "version", "argparse.ArgumentParser.add_argument", span)
                : null;

            return new ArgumentSpec(optionNames, dest, action, required, defaultValue, choices, converter, isPositional, nargs, groupId, constValue, helpText, metavar, versionText, suppressHelp);
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
            => FindOptionalArgument(token, out _, out _) is not null;

        private ArgumentSpec? FindOptionalArgument(string token, out string? inlineValue, out string? error)
        {
            inlineValue = null;
            error = null;
            if (!LooksLikeOptionalToken(token))
            {
                return null;
            }

            var optionToken = token;
            var equalsIndex = token.IndexOf('=');
            if (equalsIndex > 0)
            {
                optionToken = token[..equalsIndex];
                inlineValue = token[(equalsIndex + 1)..];
            }

            var exact = FindExactOptionalArgument(optionToken);
            if (exact is not null)
            {
                return exact;
            }

            if (!optionToken.StartsWith("--", StringComparison.Ordinal) &&
                optionToken.Length > 2 &&
                FindExactOptionalArgument(optionToken[..2]) is { } shortWithInline &&
                !IsNoValueAction(shortWithInline.Action))
            {
                inlineValue = optionToken[2..] + (inlineValue is null ? string.Empty : "=" + inlineValue);
                return shortWithInline;
            }

            if (_options.AllowAbbrev && optionToken.StartsWith("--", StringComparison.Ordinal))
            {
                var matches = new List<ArgumentSpec>();
                foreach (var candidate in _arguments)
                {
                    if (candidate.IsPositional)
                    {
                        continue;
                    }

                    foreach (var name in candidate.OptionNames)
                    {
                        if (name.StartsWith("--", StringComparison.Ordinal) &&
                            name.StartsWith(optionToken, StringComparison.Ordinal))
                        {
                            matches.Add(candidate);
                            break;
                        }
                    }
                }

                if (matches.Count == 1)
                {
                    return matches[0];
                }

                if (matches.Count > 1)
                {
                    error = $"ambiguous option: {optionToken}";
                    return null;
                }
            }

            return null;
        }

        private ArgumentSpec? FindExactOptionalArgument(string token)
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

        private bool TryExpandShortFlagCluster(string token, out List<ArgumentSpec> specs)
        {
            specs = [];
            if (!LooksLikeOptionalToken(token) ||
                token.StartsWith("--", StringComparison.Ordinal) ||
                token.Length <= 2 ||
                FindExactOptionalArgument(token) is not null)
            {
                return false;
            }

            for (var i = 1; i < token.Length; i++)
            {
                var spec = FindExactOptionalArgument("-" + token[i]);
                if (spec is null || !IsNoValueAction(spec.Action))
                {
                    specs.Clear();
                    return false;
                }

                specs.Add(spec);
            }

            return specs.Count != 0;
        }

        private static PyString RequireString(string name, object value, LythonSourceSpan span)
        {
            return RequireStringValue(value, name, "argparse.ArgumentParser.add_argument", span);
        }

        private static string InferDestination(IReadOnlyList<string> optionNames, LythonSourceSpan span)
        {
            string? preferred = null;
            for (var i = 0; i < optionNames.Count; i++)
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

        private object FormatUsage(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            RequireNoArguments(arguments, "argparse.ArgumentParser.format_usage", span);
            return FormatUsageText();
        }

        private object FormatHelp(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            RequireNoArguments(arguments, "argparse.ArgumentParser.format_help", span);
            return FormatHelpText(context);
        }

        private object PrintUsage(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var target = ResolveOptionalFile(arguments, "argparse.ArgumentParser.print_usage", span);
            WriteToTarget(FormatUsageText(), target ?? context.State.Stdout, span);
            return PyNone.Instance;
        }

        private object PrintHelp(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var target = ResolveOptionalFile(arguments, "argparse.ArgumentParser.print_help", span);
            WriteToTarget(FormatHelpText(context), target ?? context.State.Stdout, span);
            return PyNone.Instance;
        }

        private object Error(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var message = RequireSingleStringArgument(arguments, "argparse.ArgumentParser.error", "message", span);
            WriteParserError(message.AsString(), context, span);
            throw CreateSystemExit(message.AsString(), span, status: 2);
        }

        private object Exit(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var status = BigInteger.Zero;
            object message = PyNone.Instance;
            BindExitArguments(arguments, ref status, ref message, span);
            if (!ReferenceEquals(message, PyNone.Instance))
            {
                if (!PyStringOps.TryAsString(message, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(..., message=...) expects a string or None.", span);
                }

                WriteToTarget(text, context.State.Stderr, span);
            }

            throw CreateSystemExit(string.Empty, span, status);
        }

        private object SetDefaults(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.set_defaults(...) accepts keyword arguments only.", span);
                }

                _defaults[argument.Name] = argument.Value;
            }

            return PyNone.Instance;
        }

        private object GetDefault(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var destination = RequireSingleStringArgument(arguments, "argparse.ArgumentParser.get_default", "dest", span).AsString();
            if (_defaults.TryGetValue(destination, out var parserDefault))
            {
                return parserDefault;
            }

            foreach (var argument in _arguments)
            {
                if (string.Equals(argument.Destination, destination, StringComparison.Ordinal))
                {
                    return argument.DefaultValue;
                }
            }

            return PyNone.Instance;
        }

        private object AddSubparsers(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", "argparse.ArgumentParser.add_subparsers(...) is not supported by Lython.", span);
        }

        private PyString FormatUsageText()
        {
            var usage = _options.Usage;
            if (usage is not null)
            {
                return PyString.FromString(usage.StartsWith("usage:", StringComparison.Ordinal) ? EnsureTrailingNewline(usage) : "usage: " + EnsureTrailingNewline(usage));
            }

            var parts = new List<string> { "usage:", _options.Prog };
            foreach (var argument in _arguments)
            {
                if (argument.Action == "help" && !_options.AddHelp || argument.SuppressHelp)
                {
                    continue;
                }

                parts.Add(RenderUsagePart(argument));
            }

            return PyString.FromString(string.Join(" ", parts) + "\n");
        }

        private PyString FormatHelpText(ExecutionContext context)
        {
            var builder = new StringBuilder();
            builder.Append(FormatUsageText().AsString());
            if (_options.Description is not null)
            {
                builder.Append('\n');
                builder.AppendLine(_options.Description);
            }

            AppendHelpGroup(builder, "positional arguments", _arguments.Where(static argument => argument.IsPositional), context);
            AppendHelpGroup(builder, "options", _arguments.Where(static argument => !argument.IsPositional), context);

            if (_options.Epilog is not null)
            {
                builder.Append('\n');
                builder.AppendLine(_options.Epilog);
            }

            return PyString.FromString(builder.ToString());
        }

        private void AppendHelpGroup(StringBuilder builder, string title, IEnumerable<ArgumentSpec> arguments, ExecutionContext context)
        {
            var visible = arguments.Where(static argument => !argument.SuppressHelp).ToArray();
            if (visible.Length == 0)
            {
                return;
            }

            builder.Append('\n');
            builder.Append(title);
            builder.AppendLine(":");
            foreach (var argument in visible)
            {
                builder.Append("  ");
                builder.Append(RenderHelpInvocation(argument));
                if (argument.HelpText is not null)
                {
                    builder.Append("  ");
                    builder.Append(ApplyFormatSubstitutions(argument.HelpText, argument, context));
                }

                if (ShowsArgumentDefaults() && !IsSuppress(argument.DefaultValue) && argument.DefaultValue is not PyNone)
                {
                    builder.Append(" (default: ");
                    builder.Append(PyRendering.ToPythonString(argument.DefaultValue, new PyRenderingContext(context)));
                    builder.Append(')');
                }

                builder.Append('\n');
            }
        }

        private bool ShowsArgumentDefaults()
            => _options.FormatterClass is ArgparseFormatterClass { Name: "ArgumentDefaultsHelpFormatter" };

        private string RenderUsagePart(ArgumentSpec argument)
        {
            if (argument.IsPositional)
            {
                return argument.Nargs switch
                {
                    "?" => $"[{argument.DisplayMetavar}]",
                    "*" => $"[{argument.DisplayMetavar} ...]",
                    "+" => $"{argument.DisplayMetavar} [{argument.DisplayMetavar} ...]",
                    _ when FixedNargsCount(argument.Nargs) is int count => string.Join(" ", Enumerable.Repeat(argument.DisplayMetavar, count)),
                    _ => argument.DisplayMetavar
                };
            }

            var name = argument.OptionNames.FirstOrDefault(static item => item.StartsWith("--", StringComparison.Ordinal)) ??
                       argument.OptionNames.FirstOrDefault() ??
                       argument.Destination;
            var invocation = IsNoValueAction(argument.Action)
                ? name
                : $"{name} {argument.DisplayMetavar}";
            return argument.Required ? invocation : $"[{invocation}]";
        }

        private string RenderHelpInvocation(ArgumentSpec argument)
        {
            if (argument.IsPositional)
            {
                return argument.DisplayMetavar;
            }

            var names = string.Join(", ", argument.OptionNames);
            return IsNoValueAction(argument.Action) ? names : $"{names} {argument.DisplayMetavar}";
        }

        private string ApplyFormatSubstitutions(string text, ArgumentSpec argument, ExecutionContext context)
            => text
                .Replace("%(prog)s", _options.Prog, StringComparison.Ordinal)
                .Replace("%(default)s", PyRendering.ToPythonString(argument.DefaultValue, new PyRenderingContext(context)), StringComparison.Ordinal);

        private void WriteParserError(string message, ExecutionContext context, LythonSourceSpan span)
        {
            WriteToTarget(FormatUsageText(), context.State.Stderr, span);
            WriteToTarget(PyString.FromString($"{_options.Prog}: error: {message}\n"), context.State.Stderr, span);
        }

        private LythonRuntimeException CreateParseFailure(string message, LythonSourceSpan span)
            => _options.ExitOnError
                ? CreateSystemExit(message, span, status: 2)
                : new LythonRuntimeException("ArgumentError", message, span);

        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span, BigInteger status)
            => new("SystemExit", message, span, payload: status);

        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span, int status)
            => CreateSystemExit(message, span, new BigInteger(status));

        private static bool LooksLikeOptionalToken(string token)
            => token.Length > 1 && token[0] == '-' && token != "-";

        private static bool IsNoValueAction(string action)
            => action is "store_true" or "store_false" or "store_const" or "count" or "help" or "version";

        private static bool ProducesListValue(ArgumentSpec spec)
            => spec.Nargs is "*" or "+" || FixedNargsCount(spec.Nargs) is not null;

        private static int? FixedNargsCount(string? nargs)
            => nargs is not null && int.TryParse(nargs, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                ? count
                : null;

        private static BigInteger IncrementCount(object current)
            => current switch
            {
                BigInteger integer => integer + BigInteger.One,
                int integer => new BigInteger(integer + 1),
                PyNone => BigInteger.One,
                _ => BigInteger.One
            };

        private static int RequiredPositionalSlotsAfter(IReadOnlyList<ArgumentSpec> positionals, int index)
        {
            var count = 0;
            for (var i = index + 1; i < positionals.Count; i++)
            {
                var spec = positionals[i];
                count += spec.Nargs switch
                {
                    null => 1,
                    "+" => 1,
                    _ when FixedNargsCount(spec.Nargs) is int fixedCount => fixedCount,
                    _ => 0
                };
            }

            return count;
        }

        private bool HasOptionalArgumentNamed(string token, bool exactOnly)
        {
            _ = exactOnly;
            return HasOptionalArgumentNamed(token);
        }

        private int CountRemainingPositionalCandidates(IReadOnlyList<string> argv, int index)
        {
            var count = 0;
            for (var i = index; i < argv.Count; i++)
            {
                if (HasOptionalArgumentNamed(argv[i]))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static string JoinUnknownArguments(IEnumerable<string> unknown)
            => string.Join(" ", unknown);

        private static List<string> ToStringList(object value, string message, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out _))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            var result = new List<string>();
            foreach (var item in ToSequence(value, span))
            {
                if (!PyStringOps.TryAsString(item, out var text))
                {
                    throw new LythonRuntimeException("TypeError", message, span);
                }

                result.Add(text.AsString());
            }

            return result;
        }

        private static PyString RequireSingleStringArgument(CallArgumentValue[] arguments, string methodName, string parameterName, LythonSourceSpan span)
        {
            object? value = ArgparseUnspecifiedValue.Instance;
            var assigned = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument '{parameterName}'.", span);
                    }

                    value = argument.Value;
                    assigned = true;
                    continue;
                }

                if (argument.Name != parameterName)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                if (assigned)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument '{parameterName}'.", span);
                }

                value = argument.Value;
                assigned = true;
            }

            if (!assigned)
            {
                throw new LythonRuntimeException("TypeError", $"{methodName}({parameterName}) expects one argument.", span);
            }

            return RequireStringValue(value!, parameterName, methodName, span);
        }

        private static void RequireNoArguments(CallArgumentValue[] arguments, string methodName, LythonSourceSpan span)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"{methodName}() expects no arguments.", span);
            }
        }

        private static object? ResolveOptionalFile(CallArgumentValue[] arguments, string methodName, LythonSourceSpan span)
        {
            object? target = null;
            var assigned = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}([file]) expects zero or one argument.", span);
                    }

                    target = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                    assigned = true;
                    continue;
                }

                if (argument.Name != "file")
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                if (assigned)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'file'.", span);
                }

                target = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                assigned = true;
            }

            return target;
        }

        private static void BindExitArguments(CallArgumentValue[] arguments, ref BigInteger status, ref object message, LythonSourceSpan span)
        {
            var positionalIndex = 0;
            var assignedStatus = false;
            var assignedMessage = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex == 0)
                    {
                        if (assignedStatus)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'status'.", span);
                        }

                        status = RequireInteger(argument.Value, "status", "argparse.ArgumentParser.exit", span);
                        assignedStatus = true;
                    }
                    else if (positionalIndex == 1)
                    {
                        if (assignedMessage)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'message'.", span);
                        }

                        message = argument.Value;
                        assignedMessage = true;
                    }
                    else
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit([status][, message]) expects zero to two arguments.", span);
                    }

                    positionalIndex++;
                    continue;
                }

                switch (argument.Name)
                {
                    case "status":
                        if (assignedStatus)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'status'.", span);
                        }

                        status = RequireInteger(argument.Value, "status", "argparse.ArgumentParser.exit", span);
                        assignedStatus = true;
                        break;
                    case "message":
                        if (assignedMessage)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'message'.", span);
                        }

                        message = argument.Value;
                        assignedMessage = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser.exit(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }
            }
        }

        private static void WriteToTarget(PyString text, object target, LythonSourceSpan span)
        {
            switch (target)
            {
                case HostTextOutputHandle output:
                    _ = output.Write(text, span);
                    break;
                case ExecutionContext.TextFileHandle file:
                    _ = file.Write(text);
                    break;
                default:
                    throw new LythonRuntimeException("TypeError", "argparse print methods expect a writable Lython text stream or file handle.", span);
            }
        }

        private static string EnsureTrailingNewline(string text)
            => text.EndsWith('\n') ? text : text + "\n";

        private static PyString RequireStringValue(object value, string name, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
            }

            return text;
        }

        private static string? OptionalString(object value, string name, string owner, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            return RequireStringValue(value, name, owner, span).AsString();
        }

        private static bool RequireBool(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is bool boolean)
            {
                return boolean;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a bool.", span);
        }

        private static BigInteger RequireInteger(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is BigInteger integer)
            {
                return integer;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects an integer.", span);
        }

        private static bool IsSuppress(object? value)
            => ReferenceEquals(value, ArgparseSuppressValue.Instance) ||
               value is ArgparseSuppressValue ||
               (PyStringOps.TryAsString(value ?? PyNone.Instance, out var text) &&
                text.AsString() is "SUPPRESS" or "==SUPPRESS==");
    }

    internal sealed class ArgparseNamespaceObject
        : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly Dictionary<string, object> _members;

        public ArgparseNamespaceObject(Dictionary<string, object> members)
        {
            _members = members;
        }

        public IReadOnlyDictionary<string, object> Members => _members;

        public bool TryGetMember(string name, out object value) => _members.TryGetValue(name, out value!);

        public bool TrySetMember(string name, object value)
        {
            _members[name] = value;
            return true;
        }

        public void ReplaceMembers(Dictionary<string, object> members)
        {
            _members.Clear();
            foreach (var pair in members)
            {
                _members[pair.Key] = pair.Value;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var parts = _members
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + PyRendering.ToReprPyString(pair.Value, context).AsString());
            return PyString.FromString("Namespace(" + string.Join(", ", parts) + ")");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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
        object ConstValue,
        string? HelpText,
        string? Metavar,
        string? VersionText,
        bool SuppressHelp)
    {
        public string DisplayMetavar => Metavar ?? Destination.ToUpperInvariant();
    }

    private sealed record ParseResult(ArgparseNamespaceObject Namespace, List<string> Unknown);

    private sealed class ArgparseFormatterClass(string name) : IPyRenderableValue
    {
        public string Name { get; } = name;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'argparse." + Name + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class ArgparseSuppressValue : IPyRenderableValue
    {
        public static readonly ArgparseSuppressValue Instance = new();

        private ArgparseSuppressValue()
        {
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("==SUPPRESS==");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class ArgparseUnspecifiedValue
    {
        public static readonly ArgparseUnspecifiedValue Instance = new();

        private ArgparseUnspecifiedValue()
        {
        }
    }

    private sealed class ArgparseFileTypeObject(string mode, string? encoding, string? errors) : ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || !PyStringOps.TryAsString(arguments[0].Value, out var filename))
            {
                throw new LythonRuntimeException("TypeError", "argparse.FileType callable expects one filename argument.", span);
            }

            var values = new List<object> { filename, PyString.FromString(mode) };
            if (encoding is not null || errors is not null)
            {
                values.Add(PyNone.Instance);
                values.Add(encoding is null ? PyNone.Instance : PyString.FromString(encoding));
                values.Add(errors is null ? PyNone.Instance : PyString.FromString(errors));
                values.Add(PyString.Empty);
            }

            return Open(values.ToArray(), span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("FileType('" + mode + "')");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

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
        private static readonly PathlibPathType PathType = new("Path", isSupported: true);
        private static readonly PathlibPathType PurePathType = new("PurePath", isSupported: true);
        private static readonly PathlibPathType PurePosixPathType = new("PurePosixPath", isSupported: true);
        private static readonly PathlibPathType PosixPathType = new("PosixPath", isSupported: true);
        private static readonly PathlibPathType PureWindowsPathType = new("PureWindowsPath", isSupported: false);
        private static readonly PathlibPathType WindowsPathType = new("WindowsPath", isSupported: false);

        private PathlibModule() : base("pathlib")
        {
        }

        public override IReadOnlyList<string> ExportedNames
            => ["Path", "PurePath", "PurePosixPath", "PosixPath", "PureWindowsPath", "WindowsPath"];

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "Path" => PathType,
                "PurePath" => PurePathType,
                "PurePosixPath" => PurePosixPathType,
                "PosixPath" => PosixPathType,
                "PureWindowsPath" => PureWindowsPathType,
                "WindowsPath" => WindowsPathType,
                _ => null!
            };

            return value is not null;
        }

        private static object CreatePath(IReadOnlyList<object> arguments, LythonSourceSpan span)
        {
            if (arguments.Count == 0)
            {
                return new PyPath(PyStringOps.DotLiteral);
            }

            PyString? path = null;
            foreach (var argument in arguments)
            {
                PyString segment;
                if (argument is PyPath pyPath)
                {
                    segment = pyPath.Value;
                }
                else if (!PyStringOps.TryAsString(argument, out segment))
                {
                    throw new LythonRuntimeException("TypeError", "pathlib.Path([path][, ...]) expects string or Path arguments.", span);
                }

                path = path is null ? segment : PathOps.Join(path, segment);
            }

            return new PyPath(PathOps.Normalize(path!));
        }

        private sealed class PathlibPathType : ICallable, IPyDynamicAttributes, IPyRenderableValue, INamedRuntimeCallable
        {
            private readonly bool _isSupported;

            public PathlibPathType(string shortName, bool isSupported)
            {
                ShortName = shortName;
                _isSupported = isSupported;
            }

            public string ShortName { get; }

            public string Name => $"pathlib.{ShortName}";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (!_isSupported)
                {
                    throw UnsupportedWindowsPath(span);
                }

                var values = new object[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].Name is not null)
                    {
                        throw CallErrors.NoKeywordArguments("Builtin", Name, span);
                    }

                    values[i] = arguments[i].Value;
                }

                return CreatePath(values, span);
            }

            public bool TryGetMember(string name, out object value)
            {
                value = name switch
                {
                    "cwd" when _isSupported => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.cwd() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return new PyPath(PyString.FromString(PathOps.Normalize(context.Host.Cwd)));
                    }, $"{Name}.cwd", []),
                    "home" when _isSupported => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.home() expects no arguments.", span);
                        }

                        throw new LythonRuntimeException("NotImplementedError", $"{Name}.home() is not supported by Lython; the host does not expose an ambient user home directory.", span);
                    }, $"{Name}.home", []),
                    "__name__" => PyString.FromString(ShortName),
                    _ => null!
                };

                return value is not null;
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString($"<class 'pathlib.{ShortName}'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private LythonRuntimeException UnsupportedWindowsPath(LythonSourceSpan span)
                => new(
                    "NotImplementedError",
                    $"{Name} is not supported by Lython's normalized POSIX-like path model.",
                    span);
        }
    }

    internal static class StringMembers
    {
        public static bool TryGetMember(PyString text, string name, out object value)
        {
            value = name switch
            {
                "encode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.encode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "str.encode()", span)
                        : TextEncodingMode.Utf8;
                    _ = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "str.encode()", span)
                        : TextErrorMode.Strict;
                    return CreateBytes(EncodeUtf8Text(text, encoding, TextNewlineMode.PreserveUniversal), context, span);
                }, "str.encode", ["encoding", "errors"], 0),
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

                    var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.startswith(prefix[, start[, end]])");
                    return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: true, span);
                }, "str.startswith", ["prefix", "start", "end"], 1),
                "endswith" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                    }

                    var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.endswith(suffix[, start[, end]])");
                    return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: false, span);
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

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.find(sub[, start[, end]])");
                    return PyStringOps.Find(text, needle, start, end);
                }, "str.find", ["sub", "start", "end"], 1),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.index(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.index(sub[, start[, end]])");
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

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.rfind(sub[, start[, end]])");
                    return PyStringOps.RFind(text, needle, start, end);
                }, "str.rfind", ["sub", "start", "end"], 1),
                "rindex" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rindex(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.rindex(sub[, start[, end]])");
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

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.count(sub[, start[, end]])");
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

        private static (int Start, int End, bool StartBeyondLength) ParseStringBounds(
            int textLength,
            object[] arguments,
            LythonSourceSpan span,
            string signature)
        {
            try
            {
                object? start = arguments.Length >= 2 ? arguments[1] : null;
                object? end = arguments.Length == 3 ? arguments[2] : null;
                var normalized = PyStringOps.NormalizeRange(textLength, start, end);
                var startBeyondLength = start switch
                {
                    BigInteger integer => integer > textLength,
                    int integer => integer > textLength,
                    _ => false
                };
                return (normalized.Start, normalized.End, startBeyondLength);
            }
            catch (InvalidOperationException)
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
            }
        }

        private static bool StartsOrEndsWith(
            PyString text,
            object prefixOrTuple,
            int start,
            int end,
            bool startBeyondLength,
            bool isStart,
            LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(prefixOrTuple, out var single))
            {
                return !startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, single, start, end) : PyStringOps.EndsWith(text, single, start, end));
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

                if (!startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, textItem, start, end) : PyStringOps.EndsWith(text, textItem, start, end)))
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

    internal static class BytesMembers
    {
        public static bool TryGetMember(PyBytes bytes, string name, out object value)
        {
            value = name switch
            {
                "decode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.decode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    var text = DecodeUtf8Text(bytes.ToArray(), context, span, errors, TextNewlineMode.PreserveUniversal);
                    if (encoding == TextEncodingMode.Utf8Bom)
                    {
                        var decoded = text.AsString();
                        if (decoded.Length > 0 && decoded[0] == '\uFEFF')
                        {
                            return PyString.FromString(decoded[1..], context.MemoryGovernor, span);
                        }
                    }

                    return text;
                }, "bytes.decode", ["encoding", "errors"], 0),
                _ => null!
            };

            return value is not null;
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
                "fnmatchcase" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchCase, MatchCase),
                "filter" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchFilter, Filter),
                "translate" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchTranslate, Translate),
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

        private object MatchCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatchcase(name, pattern) expects two string arguments.", span);
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

        private object Translate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.translate(pattern) expects a string pattern.", span);
            }

            return CreateString(TranslatePattern(pattern.AsString()), context, span);
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
                else if (IsAsciiRune(token, '[') &&
                         nameIndex < name.Count &&
                         TryMatchCharacterClass(name[nameIndex], pattern, patternIndex, out var classCloseIndex, out var classMatches))
                {
                    result = classMatches && MatchSimple(name, nameIndex + 1, pattern, classCloseIndex + 1, memo);
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

        private static bool TryMatchCharacterClass(
            PyString value,
            IReadOnlyList<PyString> pattern,
            int openIndex,
            out int closeIndex,
            out bool matches)
        {
            closeIndex = -1;
            matches = false;

            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var negated = IsAsciiRune(pattern[contentStart], '!');
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var searchStart = contentStart;
            if (IsAsciiRune(pattern[searchStart], ']'))
            {
                searchStart++;
            }

            for (var index = searchStart; index < pattern.Count; index++)
            {
                if (IsAsciiRune(pattern[index], ']'))
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                return false;
            }

            var valueScalar = RuneScalarValue(value);
            var included = CharacterClassIncludes(valueScalar, pattern, contentStart, closeIndex);
            matches = negated ? !included : included;
            return true;
        }

        private static bool CharacterClassIncludes(int valueScalar, IReadOnlyList<PyString> pattern, int start, int closeIndex)
        {
            for (var index = start; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && IsAsciiRune(pattern[index + 1], '-'))
                {
                    var rangeStart = RuneScalarValue(pattern[index]);
                    var rangeEnd = RuneScalarValue(pattern[index + 2]);
                    if (rangeStart <= rangeEnd && valueScalar >= rangeStart && valueScalar <= rangeEnd)
                    {
                        return true;
                    }

                    index += 2;
                    continue;
                }

                if (RuneScalarValue(pattern[index]) == valueScalar)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TranslatePattern(string pattern)
        {
            var builder = new StringBuilder(pattern.Length + 2);
            builder.Append('^');
            for (var index = 0; index < pattern.Length; index++)
            {
                var ch = pattern[index];
                switch (ch)
                {
                    case '*':
                        builder.Append(".*");
                        break;
                    case '?':
                        builder.Append('.');
                        break;
                    case '[':
                        index = AppendTranslatedCharacterClass(builder, pattern, index);
                        break;
                    default:
                        AppendEscapedRegexLiteral(builder, ch);
                        break;
                }
            }

            builder.Append('$');
            return builder.ToString();
        }

        private static int AppendTranslatedCharacterClass(StringBuilder builder, string pattern, int openIndex)
        {
            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Length)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var negated = pattern[contentStart] == '!';
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Length)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var searchStart = contentStart;
            if (pattern[searchStart] == ']')
            {
                searchStart++;
            }

            var closeIndex = -1;
            for (var index = searchStart; index < pattern.Length; index++)
            {
                if (pattern[index] == ']')
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var classBuilder = new StringBuilder(closeIndex - contentStart);
            for (var index = contentStart; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && pattern[index + 1] == '-')
                {
                    if (char.ConvertToUtf32(pattern, index) <= char.ConvertToUtf32(pattern, index + 2))
                    {
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: true);
                        classBuilder.Append('-');
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index + 2], allowRangeHyphen: true);
                    }

                    index += 2;
                    continue;
                }

                AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: false);
            }

            if (classBuilder.Length == 0)
            {
                builder.Append(negated ? "." : "(?!)");
                return closeIndex;
            }

            builder.Append('[');
            if (negated)
            {
                builder.Append('^');
            }

            builder.Append(classBuilder);
            builder.Append(']');
            return closeIndex;
        }

        private static void AppendEscapedRegexClassCharacter(StringBuilder builder, char ch, bool allowRangeHyphen)
        {
            if (ch is '\\' or ']' or '^' || ch == '-' && !allowRangeHyphen)
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        private static bool IsAsciiRune(PyString value, char ch)
            => value.Utf8Bytes.Length == 1 && value.Utf8Bytes.Span[0] == (byte)ch;

        private static int RuneScalarValue(PyString value)
        {
            var text = value.AsString();
            return char.ConvertToUtf32(text, 0);
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
                "load" => new BuiltinCallable(LythonKnownCallableSignatures.JsonLoad, Load),
                "loads" => new BuiltinCallable(LythonKnownCallableSignatures.JsonLoads, Loads),
                "dump" => new BuiltinCallable(LythonKnownCallableSignatures.JsonDump, Dump),
                "dumps" => new BuiltinCallable(LythonKnownCallableSignatures.JsonDumps, Dumps),
                "JSONDecodeError" => new ExceptionTypeValue("JSONDecodeError"),
                "JSONEncoder" => new UnsupportedJsonClassFactory("json.JSONEncoder"),
                "JSONDecoder" => new UnsupportedJsonClassFactory("json.JSONDecoder"),
                _ => null!,
            };

            return value is not null;
        }

        private object Load(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.load(fp, *, ...) expects a readable text file handle.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(file.Read(), loadOptions, context, span);
        }

        private object Loads(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects a string argument.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(text, loadOptions, context, span);
        }

        private object Dump(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[1] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.dump(obj, fp, *, ...) expects an object and writable text file handle.", span);
            }

            var text = SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, dumpsForm: false, span), context, span);
            _ = file.Write(text);
            return PyNone.Instance;
        }

        private object Dumps(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, dumpsForm: true, span), context, span);
        }

        private object ParseJsonText(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            if (TryConvertJsonConstant(text, options, context, span, out var constant))
            {
                return constant;
            }

            try
            {
                using var document = JsonDocument.Parse(text.Utf8Bytes);
                return ConvertJson(document.RootElement, options, context, span);
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(text, ex, span, context);
            }
        }

        private static object ConvertJson(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Object => ConvertJsonObject(element, options, context, span),
                    JsonValueKind.Array => ConvertJsonArray(element, options, context, span),
                    JsonValueKind.String => JsonStringToPyString(element, context, span),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => PyNone.Instance,
                    JsonValueKind.Number => ConvertJsonNumber(element, options, context, span),
                    _ => throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}")
                };
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static object ConvertJsonObject(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            if (options.ObjectPairsHook is not null)
            {
                var pairs = new PyList([], context.MemoryGovernor, span);
                foreach (var property in element.EnumerateObject())
                {
                    context.CheckExecutionBudget(span);
                    pairs.Add(new PyTuple([
                        CreateString(property.Name, context, span),
                        ConvertJson(property.Value, options, context, span)
                    ], context.MemoryGovernor, span));
                    context.ObserveCollectionCount(pairs.Count, span);
                }

                return InvokeJsonCallback(options.ObjectPairsHook, pairs, context, span);
            }

            var result = new PyDict(context.MemoryGovernor, span);
            foreach (var property in element.EnumerateObject())
            {
                context.CheckExecutionBudget(span);
                result.SetItem(CreateString(property.Name, context, span), ConvertJson(property.Value, options, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return options.ObjectHook is null
                ? result
                : InvokeJsonCallback(options.ObjectHook, result, context, span);
        }

        private static PyList ConvertJsonArray(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            foreach (var item in element.EnumerateArray())
            {
                context.CheckExecutionBudget(span);
                result.Add(ConvertJson(item, options, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private static object ConvertJsonNumber(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var raw = element.GetRawText();
            var isFloat = raw.Contains('.', StringComparison.Ordinal) ||
                raw.Contains('e', StringComparison.OrdinalIgnoreCase);
            if (isFloat && options.ParseFloat is not null)
            {
                return InvokeJsonCallback(options.ParseFloat, CreateString(raw, context, span), context, span);
            }

            if (!isFloat && options.ParseInt is not null)
            {
                return InvokeJsonCallback(options.ParseInt, CreateString(raw, context, span), context, span);
            }

            if (element.TryGetInt64(out var integer))
            {
                return new BigInteger(integer);
            }

            if (!isFloat)
            {
                return BigInteger.Parse(raw, CultureInfo.InvariantCulture);
            }

            return element.GetDouble();
        }

        private static JsonLoadOptions ParseJsonLoadOptions(object[] arguments, LythonSourceSpan span)
        {
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, 1), "cls", span);
            return new JsonLoadOptions(
                OptionalJsonCallable(GetOptional(arguments, 2), "object_hook", span),
                OptionalJsonCallable(GetOptional(arguments, 3), "parse_float", span),
                OptionalJsonCallable(GetOptional(arguments, 4), "parse_int", span),
                OptionalJsonCallable(GetOptional(arguments, 5), "parse_constant", span),
                OptionalJsonCallable(GetOptional(arguments, 6), "object_pairs_hook", span));
        }

        private static JsonDumpOptions ParseJsonDumpOptions(object[] arguments, bool dumpsForm, LythonSourceSpan span)
        {
            var offset = dumpsForm ? 0 : 1;
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, offset + 5), "cls", span);
            var skipKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 1), defaultValue: false);
            var ensureAscii = ParseJsonBoolOption(GetOptional(arguments, offset + 2), defaultValue: true);
            var checkCircular = ParseJsonBoolOption(GetOptional(arguments, offset + 3), defaultValue: true);
            var allowNan = ParseJsonBoolOption(GetOptional(arguments, offset + 4), defaultValue: true);
            var indent = ParseJsonIndent(GetOptional(arguments, offset + 6), span);
            var separators = ParseJsonSeparators(GetOptional(arguments, offset + 7), indent is not null, span);
            var defaultCallable = OptionalJsonCallable(GetOptional(arguments, offset + 8), "default", span);
            var sortKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 9), defaultValue: false);
            return new JsonDumpOptions(skipKeys, ensureAscii, checkCircular, allowNan, indent, separators.ItemSeparator, separators.KeySeparator, defaultCallable, sortKeys);
        }

        private static PyString SerializeJsonText(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            try
            {
                var builder = new StringBuilder();
                var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                AppendJsonValue(builder, value, options, context, span, depth: 0, active);
                return CreateString(builder.ToString(), context, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        private static void AppendJsonValue(
            StringBuilder builder,
            object value,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            context.CheckExecutionBudget(span);
            switch (value)
            {
                case PyNone:
                    builder.Append("null");
                    return;
                case PyString text:
                    AppendJsonString(builder, text.AsString(), options.EnsureAscii);
                    return;
                case string text:
                    AppendJsonString(builder, text, options.EnsureAscii);
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
                case BigInteger integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    return;
                case int integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    return;
                case double floating:
                    AppendJsonDouble(builder, floating, options, span);
                    return;
                case PyDecimal decimalValue:
                    builder.Append(PyDecimalOps.Format(decimalValue.Value));
                    return;
                case PyList list:
                    AppendJsonSequence(builder, list, options, context, span, depth, active);
                    return;
                case PyTuple tuple:
                    AppendJsonSequence(builder, tuple, options, context, span, depth, active);
                    return;
                case PyDict dict:
                    AppendJsonDict(builder, dict, options, context, span, depth, active);
                    return;
                default:
                    if (options.DefaultCallable is not null)
                    {
                        var replacement = InvokeJsonCallback(options.DefaultCallable, value, context, span);
                        if (ReferenceEquals(replacement, value))
                        {
                            throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", span);
                        }

                        AppendJsonValue(builder, replacement, options, context, span, depth, active);
                        return;
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {value.GetType().Name}");
            }
        }

        private static void AppendJsonSequence(
            StringBuilder builder,
            IEnumerable<object> sequence,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            if (active is not null && !active.Add(sequence))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                var items = sequence as IReadOnlyCollection<object> ?? sequence.ToArray();
                builder.Append('[');
                var index = 0;
                foreach (var item in items)
                {
                    if (index > 0)
                    {
                        builder.Append(options.ItemSeparator);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index);
                    AppendJsonValue(builder, item, options, context, span, depth + 1, active);
                    index++;
                }

                if (index > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth);
                }

                builder.Append(']');
            }
            finally
            {
                _ = active?.Remove(sequence);
            }
        }

        private static void AppendJsonDict(
            StringBuilder builder,
            PyDict dict,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            if (active is not null && !active.Add(dict))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                var entries = new List<(string Key, object Value)>();
                foreach (var pair in dict)
                {
                    context.CheckExecutionBudget(span);
                    if (TryConvertJsonObjectKey(pair.Key, options.SkipKeys, out var key))
                    {
                        entries.Add((key, pair.Value));
                    }
                }

                if (options.SortKeys)
                {
                    entries.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
                }

                builder.Append('{');
                for (var index = 0; index < entries.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(options.ItemSeparator);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index);
                    AppendJsonString(builder, entries[index].Key, options.EnsureAscii);
                    builder.Append(options.KeySeparator);
                    AppendJsonValue(builder, entries[index].Value, options, context, span, depth + 1, active);
                }

                if (entries.Count > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth);
                }

                builder.Append('}');
            }
            finally
            {
                _ = active?.Remove(dict);
            }
        }

        private static bool TryConvertJsonObjectKey(object keyValue, bool skipKeys, out string key)
        {
            switch (keyValue)
            {
                case PyString text:
                    key = text.AsString();
                    return true;
                case string text:
                    key = text;
                    return true;
                case BigInteger integer:
                    key = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                case int integer:
                    key = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                case double floating when double.IsFinite(floating):
                    key = floating.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case bool boolean:
                    key = boolean ? "true" : "false";
                    return true;
                case PyNone:
                    key = "null";
                    return true;
                default:
                    if (skipKeys)
                    {
                        key = string.Empty;
                        return false;
                    }

                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            }
        }

        private static void AppendJsonValuePrefix(StringBuilder builder, JsonDumpOptions options, int depth, int index)
        {
            _ = index;
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth);
        }

        private static void AppendJsonContainerSuffix(StringBuilder builder, JsonDumpOptions options, int depth)
        {
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth);
        }

        private static void AppendJsonIndent(StringBuilder builder, JsonDumpOptions options, int depth)
        {
            for (var i = 0; i < depth; i++)
            {
                builder.Append(options.IndentUnit);
            }
        }

        private static void AppendJsonString(StringBuilder builder, string text, bool ensureAscii)
        {
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        continue;
                    case '\\':
                        builder.Append("\\\\");
                        continue;
                    case '\b':
                        builder.Append("\\b");
                        continue;
                    case '\f':
                        builder.Append("\\f");
                        continue;
                    case '\n':
                        builder.Append("\\n");
                        continue;
                    case '\r':
                        builder.Append("\\r");
                        continue;
                    case '\t':
                        builder.Append("\\t");
                        continue;
                }

                if (ch < 0x20 || ensureAscii && ch > 0x7f)
                {
                    builder.Append("\\u");
                    builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(ch);
            }

            builder.Append('"');
        }

        private static void AppendJsonDouble(StringBuilder builder, double value, JsonDumpOptions options, LythonSourceSpan span)
        {
            if (double.IsNaN(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("NaN");
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("Infinity");
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("-Infinity");
                return;
            }

            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool TryConvertJsonConstant(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, out object value)
        {
            var trimmed = text.AsString().Trim();
            if (trimmed is not ("NaN" or "Infinity" or "-Infinity"))
            {
                value = PyNone.Instance;
                return false;
            }

            var constantText = CreateString(trimmed, context, span);
            value = options.ParseConstant is not null
                ? InvokeJsonCallback(options.ParseConstant, constantText, context, span)
                : trimmed switch
                {
                    "NaN" => double.NaN,
                    "Infinity" => double.PositiveInfinity,
                    _ => double.NegativeInfinity
                };
            return true;
        }

        private static LythonRuntimeException CreateJsonDecodeError(PyString document, JsonException exception, LythonSourceSpan span, ExecutionContext context)
        {
            var docText = document.AsString();
            var line = exception.LineNumber.GetValueOrDefault();
            var bytePosition = exception.BytePositionInLine.GetValueOrDefault();
            var position = ComputeJsonErrorPosition(docText, line, bytePosition);
            var payload = new PyDict(context.MemoryGovernor, span);
            payload.SetItem(PyString.FromString("msg"), CreateString(exception.Message, context, span));
            payload.SetItem(PyString.FromString("doc"), document);
            payload.SetItem(PyString.FromString("pos"), new BigInteger(position));
            payload.SetItem(PyString.FromString("lineno"), new BigInteger(line + 1));
            payload.SetItem(PyString.FromString("colno"), new BigInteger(bytePosition + 1));
            return new LythonRuntimeException("JSONDecodeError", exception.Message, span, exception, payload);
        }

        private static int ComputeJsonErrorPosition(string text, long lineNumber, long bytePositionInLine)
        {
            var line = 0L;
            var position = 0;
            while (line < lineNumber && position < text.Length)
            {
                if (text[position++] == '\n')
                {
                    line++;
                }
            }

            return (int)Math.Min(text.Length, position + bytePositionInLine);
        }

        private static object InvokeJsonCallback(object callable, object argument, ExecutionContext context, LythonSourceSpan span)
            => InvokeCallableTarget(callable, span, span, context, [new CallArgumentValue(null, argument)]);

        private static object? OptionalJsonCallable(object value, string parameterName, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (value is ICallable)
            {
                return value;
            }

            throw new LythonRuntimeException("TypeError", $"json option {parameterName}=... expects a callable or None.", span);
        }

        private static void EnsureUnsupportedJsonClassIsNone(object value, string parameterName, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return;
            }

            throw new LythonRuntimeException("NotImplementedError", $"json {parameterName}=... custom encoder/decoder classes are not supported by Lython.", span);
        }

        private static bool ParseJsonBoolOption(object value, bool defaultValue)
            => ReferenceEquals(value, PyNone.Instance) ? defaultValue : IsTruthy(value);

        private static string? ParseJsonIndent(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
            }

            if (value is BigInteger integer)
            {
                if (integer <= BigInteger.Zero)
                {
                    return string.Empty;
                }

                if (integer > 32)
                {
                    throw new LythonRuntimeException("OverflowError", "json.dumps(indent=...) is too large for Lython.", span);
                }

                return new string(' ', (int)integer);
            }

            throw new LythonRuntimeException("TypeError", "json.dumps(indent=...) expects an integer, string, or None.", span);
        }

        private static (string ItemSeparator, string KeySeparator) ParseJsonSeparators(object value, bool pretty, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return pretty ? (",", ": ") : (",", ":");
            }

            if (value is not PyTuple and not PyList)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings or None.", span);
            }

            var items = ToSequence(value, span).ToArray();
            if (items.Length != 2 ||
                !PyStringOps.TryAsString(items[0], out var itemSeparator) ||
                !PyStringOps.TryAsString(items[1], out var keySeparator))
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings.", span);
            }

            return (itemSeparator.AsString(), keySeparator.AsString());
        }

        private static object GetOptional(object[] arguments, int index)
            => index < arguments.Length ? arguments[index] : PyNone.Instance;

        private sealed record JsonLoadOptions(
            object? ObjectHook,
            object? ParseFloat,
            object? ParseInt,
            object? ParseConstant,
            object? ObjectPairsHook);

        private sealed record JsonDumpOptions(
            bool SkipKeys,
            bool EnsureAscii,
            bool CheckCircular,
            bool AllowNan,
            string? IndentUnit,
            string ItemSeparator,
            string KeySeparator,
            object? DefaultCallable,
            bool SortKeys);

        private sealed class UnsupportedJsonClassFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
        {
            public UnsupportedJsonClassFactory(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                _ = arguments;
                context.CheckExecutionBudget(span);
                throw new LythonRuntimeException("NotImplementedError", $"{Name} custom classes are not supported by Lython's JSON subset.", span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<class '" + Name + "'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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

    private const int CsvQuoteMinimal = 0;
    private const int CsvQuoteAll = 1;
    private const int CsvQuoteNonNumeric = 2;
    private const int CsvQuoteNone = 3;

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
                "DictReader" => new BuiltinCallable(LythonKnownCallableSignatures.CsvDictReader, DictReader),
                "DictWriter" => new BuiltinCallable(LythonKnownCallableSignatures.CsvDictWriter, DictWriter),
                "Error" => new ExceptionTypeValue("Error"),
                "QUOTE_MINIMAL" => new BigInteger(CsvQuoteMinimal),
                "QUOTE_ALL" => new BigInteger(CsvQuoteAll),
                "QUOTE_NONNUMERIC" => new BigInteger(CsvQuoteNonNumeric),
                "QUOTE_NONE" => new BigInteger(CsvQuoteNone),
                _ => null!,
            };

            return value is not null;
        }

        private object Reader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var options = GetOptions(arguments, dialectIndex: 1, delimiterIndex: 2, quotecharIndex: 3, quotingIndex: 4, doublequoteIndex: 5, escapecharIndex: 6, skipinitialspaceIndex: 7, lineterminatorIndex: 8, strictIndex: 9, span);
            var records = ParseCsvRecords(arguments[0], options, span, context);
            return new CsvReaderObject(records.Rows, records.PhysicalLineCount, context.MemoryGovernor, span);
        }

        private object DictReader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictReader(f[, fieldnames][, restkey][, restval][, ...]) expects at least one argument.", span);
            }

            var options = GetOptions(arguments, dialectIndex: 4, delimiterIndex: 5, quotecharIndex: 6, quotingIndex: 7, doublequoteIndex: 8, escapecharIndex: 9, skipinitialspaceIndex: 10, lineterminatorIndex: 11, strictIndex: 12, span);
            var records = ParseCsvRecords(arguments[0], options, span, context);
            var fieldNames = arguments.Length > 1 && arguments[1] is not PyNone
                ? ToCsvFieldNames(arguments[1], "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", span)
                : records.Rows.Count == 0
                    ? null
                    : ToFieldNameList((PyList)records.Rows[0], span);

            var firstDataRow = arguments.Length > 1 && arguments[1] is not PyNone ? 0 : 1;
            var rows = new PyList([], context.MemoryGovernor, span);
            if (fieldNames is not null)
            {
                for (var i = firstDataRow; i < records.Rows.Count; i++)
                {
                    rows.Add(CreateDictReaderRow(
                        (PyList)records.Rows[i],
                        fieldNames,
                        RestKey(arguments, 2),
                        RestValue(arguments, 3),
                        context,
                        span));
                }
            }

            return new CsvDictReaderObject(rows, fieldNames, records.PhysicalLineCount, context.MemoryGovernor, span);
        }

        private object Writer(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            ExecutionContext.TextFileHandle? file = null;
            object? legacyDelimiter = null;
            if (arguments.Length > 0 && arguments[0] is not PyNone)
            {
                if (arguments[0] is ExecutionContext.TextFileHandle handle)
                {
                    file = handle;
                }
                else if (PyStringOps.TryAsString(arguments[0], out _) &&
                    (arguments.Length <= 1 || arguments[1] is PyNone) &&
                    (arguments.Length <= 2 || arguments[2] is PyNone))
                {
                    legacyDelimiter = arguments[0];
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", "csv.writer(fileobj[, dialect][, ...]) expects a text file handle or a legacy delimiter string.", span);
                }
            }

            var options = GetOptions(arguments, dialectIndex: 1, delimiterIndex: 2, quotecharIndex: 3, quotingIndex: 4, doublequoteIndex: 5, escapecharIndex: 6, skipinitialspaceIndex: 7, lineterminatorIndex: 8, strictIndex: 9, span, legacyDelimiter);
            return new CsvWriterObject(options, file);
        }

        private object DictWriter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(fileobj, fieldnames, ...) expects a text file handle and field names.", span);
            }

            var fieldNames = ToCsvFieldNames(arguments[1], "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", span);
            var restVal = arguments.Length > 2 && arguments[2] is not PyNone ? arguments[2] : PyString.Empty;
            var extrasAction = GetExtrasAction(arguments, 3, span);
            var options = GetOptions(arguments, dialectIndex: 4, delimiterIndex: 5, quotecharIndex: 6, quotingIndex: 7, doublequoteIndex: 8, escapecharIndex: 9, skipinitialspaceIndex: 10, lineterminatorIndex: 11, strictIndex: 12, span);
            return new CsvDictWriterObject(new CsvWriterObject(options, file), fieldNames, restVal, extrasAction);
        }

        private static CsvOptions GetOptions(
            object[] arguments,
            int dialectIndex,
            int delimiterIndex,
            int quotecharIndex,
            int quotingIndex,
            int doublequoteIndex,
            int escapecharIndex,
            int skipinitialspaceIndex,
            int lineterminatorIndex,
            int strictIndex,
            LythonSourceSpan span,
            object? legacyDelimiter = null)
        {
            ValidateDialect(arguments, dialectIndex, span);

            var dialectDelimiter = TryGetLegacyDialectDelimiter(arguments, dialectIndex, delimiterIndex, out var delimiterFromDialect)
                ? delimiterFromDialect
                : null;
            var delimiter = (legacyDelimiter is null && dialectDelimiter is null
                ? GetCharacterOption(arguments, delimiterIndex, PyStringOps.CommaLiteral, "delimiter", allowNone: false, span)
                : GetRequiredCharacter(legacyDelimiter ?? dialectDelimiter!, "delimiter", allowNone: false, span))!;
            if (delimiter.AsString() is "\r" or "\n")
            {
                throw CsvError("csv delimiter cannot be a newline.", span);
            }

            var quotechar = GetCharacterOption(arguments, quotecharIndex, PyString.FromString("\""), "quotechar", allowNone: true, span);
            var quoting = GetQuoting(arguments, quotingIndex, span);
            var doublequote = GetBooleanOption(arguments, doublequoteIndex, defaultValue: true, "doublequote", span);
            var escapechar = GetCharacterOption(arguments, escapecharIndex, null, "escapechar", allowNone: true, span);
            var skipinitialspace = GetBooleanOption(arguments, skipinitialspaceIndex, defaultValue: false, "skipinitialspace", span);
            var lineterminator = GetStringOption(arguments, lineterminatorIndex, PyString.FromString("\n"), "lineterminator", allowNone: false, span);
            var strict = GetBooleanOption(arguments, strictIndex, defaultValue: false, "strict", span);

            if (quoting == CsvQuoteNone && escapechar is null)
            {
                // This is legal until escaping is actually required.
            }

            return new CsvOptions(delimiter, quotechar, quoting, doublequote, escapechar, skipinitialspace, lineterminator, strict);
        }

        private static bool TryGetLegacyDialectDelimiter(object[] arguments, int dialectIndex, int delimiterIndex, out object delimiter)
        {
            if (arguments.Length > delimiterIndex && arguments[delimiterIndex] is not PyNone)
            {
                delimiter = PyNone.Instance;
                return false;
            }

            if (arguments.Length > dialectIndex &&
                arguments[dialectIndex] is not PyNone &&
                PyStringOps.TryAsString(arguments[dialectIndex], out var text) &&
                text.Length == 1)
            {
                delimiter = arguments[dialectIndex];
                return true;
            }

            delimiter = PyNone.Instance;
            return false;
        }

        private static void ValidateDialect(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return;
            }

            if (PyStringOps.TryAsString(arguments[index], out var dialect) &&
                string.Equals(dialect.AsString(), "excel", StringComparison.Ordinal))
            {
                return;
            }

            if (PyStringOps.TryAsString(arguments[index], out var legacyDelimiter) && legacyDelimiter.Length == 1)
            {
                return;
            }

            throw new LythonRuntimeException("TypeError", "csv dialect registry is unsupported; pass explicit CSV options instead.", span);
        }

        private static PyString GetStringOption(object[] arguments, int index, PyString defaultValue, string name, bool allowNone, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                if (!allowNone && arguments.Length > index && arguments[index] is PyNone)
                {
                    throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
                }

                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var value))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            return value;
        }

        private static PyString? GetCharacterOption(object[] arguments, int index, PyString? defaultValue, string name, bool allowNone, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            return GetRequiredCharacter(arguments[index], name, allowNone, span);
        }

        private static PyString? GetRequiredCharacter(object value, string name, bool allowNone, LythonSourceSpan span)
        {
            if (value is PyNone)
            {
                if (allowNone)
                {
                    return null;
                }

                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a string.", span);
            }

            if (text.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be one character.", span);
            }

            return text;
        }

        private static int GetQuoting(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return CsvQuoteMinimal;
            }

            if (arguments[index] is not BigInteger integer || integer < CsvQuoteMinimal || integer > CsvQuoteNone)
            {
                throw new LythonRuntimeException("TypeError", "csv quoting must be one of the QUOTE_* constants.", span);
            }

            return (int)integer;
        }

        private static bool GetBooleanOption(object[] arguments, int index, bool defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (arguments[index] is not bool value)
            {
                throw new LythonRuntimeException("TypeError", $"csv {name} must be a bool.", span);
            }

            return value;
        }

        private static PyString[] ToCsvFieldNames(object value, string message, LythonSourceSpan span)
        {
            var names = new List<PyString>();
            foreach (var item in ToSequence(value, span))
            {
                if (!PyStringOps.TryAsString(item, out var name))
                {
                    throw new LythonRuntimeException("TypeError", message, span);
                }

                names.Add(name);
            }

            return [.. names];
        }

        private static PyString[] ToFieldNameList(PyList row, LythonSourceSpan span)
        {
            var names = new PyString[row.Count];
            for (var i = 0; i < row.Count; i++)
            {
                if (!PyStringOps.TryAsString(row[i], out var name))
                {
                    throw CsvError("csv.DictReader header row must contain strings.", span);
                }

                names[i] = name;
            }

            return names;
        }

        private static object RestKey(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static object RestValue(object[] arguments, int index)
            => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

        private static string GetExtrasAction(object[] arguments, int index, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return "raise";
            }

            if (!PyStringOps.TryAsString(arguments[index], out var action))
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter(..., extrasaction=...) expects a string.", span);
            }

            var text = action.AsString();
            if (!string.Equals(text, "raise", StringComparison.Ordinal) &&
                !string.Equals(text, "ignore", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("ValueError", "extrasaction must be 'raise' or 'ignore'.", span);
            }

            return text;
        }

        private static PyDict CreateDictReaderRow(PyList row, PyString[] fieldNames, object restKey, object restValue, ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            var count = Math.Min(row.Count, fieldNames.Length);
            for (var i = 0; i < count; i++)
            {
                dict.SetItem(fieldNames[i], row[i]);
            }

            for (var i = count; i < fieldNames.Length; i++)
            {
                dict.SetItem(fieldNames[i], restValue);
            }

            if (row.Count > fieldNames.Length)
            {
                var extras = new object[row.Count - fieldNames.Length];
                for (var i = fieldNames.Length; i < row.Count; i++)
                {
                    extras[i - fieldNames.Length] = row[i];
                }

                dict.SetItem(ValidateDictionaryKey(restKey, span, context.MemoryGovernor), new PyList(extras, context.MemoryGovernor, span));
            }

            return dict;
        }

        private static CsvReadResult ParseCsvRecords(object source, CsvOptions options, LythonSourceSpan span, ExecutionContext context)
        {
            var parser = new CsvRecordParser(options, context, span);
            var physicalLineCount = 0;
            foreach (var item in ToSequence(source, span))
            {
                context.CheckExecutionBudget(span);
                if (!PyStringOps.TryAsString(item, out var line))
                {
                    throw new LythonRuntimeException("TypeError", "csv.reader(csvfile) expects an iterable of strings.", span);
                }

                physicalLineCount++;
                parser.Feed(line.AsString());
            }

            parser.Finish();
            return new CsvReadResult(parser.Rows, physicalLineCount);
        }

        private sealed record CsvReadResult(PyList Rows, int PhysicalLineCount);

        private sealed class CsvRecordParser
        {
            private readonly CsvOptions _options;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly List<object> _row = new();
            private readonly StringBuilder _field = new();
            private bool _inQuotes;
            private bool _fieldStarted;
            private bool _afterQuote;
            private bool _recordStarted;

            public CsvRecordParser(CsvOptions options, ExecutionContext context, LythonSourceSpan span)
            {
                _options = options;
                _context = context;
                _span = span;
                Rows = new PyList([], context.MemoryGovernor, span);
            }

            public PyList Rows { get; }

            public void Feed(string text)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    if (_inQuotes)
                    {
                        if (TryConsumeEscape(text, ref i))
                        {
                            continue;
                        }

                        if (IsQuote(c))
                        {
                            if (_options.DoubleQuote && i + 1 < text.Length && IsQuote(text[i + 1]))
                            {
                                _field.Append(c);
                                i++;
                            }
                            else
                            {
                                _inQuotes = false;
                                _afterQuote = true;
                            }

                            continue;
                        }

                        _field.Append(c);
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        FinishRecord();
                        if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }

                        continue;
                    }

                    if (MatchesAt(text, i, _options.Delimiter.AsString()))
                    {
                        FinishField();
                        i += _options.Delimiter.AsString().Length - 1;
                        _recordStarted = true;
                        _afterQuote = false;
                        continue;
                    }

                    if (_options.SkipInitialSpace && !_fieldStarted && _field.Length == 0 && c == ' ')
                    {
                        continue;
                    }

                    if (IsQuote(c) && !_fieldStarted)
                    {
                        _inQuotes = true;
                        _fieldStarted = true;
                        _recordStarted = true;
                        continue;
                    }

                    if (_afterQuote)
                    {
                        throw CsvError("Invalid csv input.", _span);
                    }

                    if (TryConsumeEscape(text, ref i))
                    {
                        continue;
                    }

                    _fieldStarted = true;
                    _recordStarted = true;
                    _field.Append(c);
                }

                if (_inQuotes)
                {
                    if (text.Length == 0 || (text[^1] != '\n' && text[^1] != '\r'))
                    {
                        _field.Append('\n');
                    }

                    return;
                }

                if (text.Length == 0)
                {
                    Rows.Add(new PyList([], _context.MemoryGovernor, _span));
                    return;
                }

                if (text[^1] != '\n' && text[^1] != '\r')
                {
                    FinishRecord();
                }
            }

            public void Finish()
            {
                if (_inQuotes)
                {
                    throw CsvError("Invalid csv input.", _span);
                }
            }

            private bool TryConsumeEscape(string text, ref int index)
            {
                if (_options.EscapeChar is null || !MatchesAt(text, index, _options.EscapeChar.AsString()))
                {
                    return false;
                }

                if (index + _options.EscapeChar.AsString().Length >= text.Length)
                {
                    if (_options.Strict)
                    {
                        throw CsvError("Invalid csv input.", _span);
                    }

                    _field.Append(_options.EscapeChar.AsString());
                    index += _options.EscapeChar.AsString().Length - 1;
                    return true;
                }

                index += _options.EscapeChar.AsString().Length;
                _field.Append(text[index]);
                _fieldStarted = true;
                _recordStarted = true;
                return true;
            }

            private void FinishField()
            {
                _row.Add(PyString.FromString(_field.ToString()));
                _field.Clear();
                _fieldStarted = false;
                _afterQuote = false;
            }

            private void FinishRecord()
            {
                if (!_recordStarted && !_fieldStarted && _field.Length == 0 && _row.Count == 0)
                {
                    Rows.Add(new PyList([], _context.MemoryGovernor, _span));
                    return;
                }

                FinishField();
                Rows.Add(new PyList(_row, _context.MemoryGovernor, _span));
                _row.Clear();
                _recordStarted = false;
                _afterQuote = false;
            }

            private bool IsQuote(char c)
                => _options.QuoteChar is not null && MatchesAt(c.ToString(), 0, _options.QuoteChar.AsString());
        }

        private static bool MatchesAt(string text, int index, string value)
        {
            if (index + value.Length > text.Length)
            {
                return false;
            }

            return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }
    }

    internal sealed record CsvOptions(
        PyString Delimiter,
        PyString? QuoteChar,
        int Quoting,
        bool DoubleQuote,
        PyString? EscapeChar,
        bool SkipInitialSpace,
        PyString LineTerminator,
        bool Strict);

    private static LythonRuntimeException CsvError(string message, LythonSourceSpan span)
        => new("Error", message, span);

    internal sealed class CsvReaderObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
    {
        public CsvReaderObject(PyList rows, int lineNum, MemoryGovernor governor, LythonSourceSpan span)
        {
            Rows = rows;
            LineNum = lineNum;
            _governor = governor;
            _span = span;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;

        public PyList Rows { get; }

        public int LineNum { get; }

        public int Count => Rows.Count;

        public int Length => Rows.Length;

        public object this[int index] => Rows[index];

        public object GetItem(int index) => Rows.GetItem(index);

        public object CreateSlice(IEnumerable<object> items) => new PyList(items, _governor, _span);

        public object GetIndex(int index) => Rows.GetIndex(index);

        public object GetSlice(IEnumerable<int> indices) => Rows.GetSlice(indices);

        public bool IsTruthy() => Rows.IsTruthy();

        public IEnumerable<object> Iterate() => Rows;

        public PyString RenderPython(PyRenderingContext context) => Rows.RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => Rows.RenderInterpolated(context);

        public IEnumerator<object> GetEnumerator() => Rows.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CsvDictReaderObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue
    {
        public CsvDictReaderObject(PyList rows, PyString[]? fieldNames, int lineNum, MemoryGovernor governor, LythonSourceSpan span)
        {
            Rows = rows;
            FieldNames = fieldNames;
            LineNum = lineNum;
            _governor = governor;
            _span = span;
        }

        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan _span;

        public PyList Rows { get; }

        public PyString[]? FieldNames { get; }

        public int LineNum { get; }

        public int Count => Rows.Count;

        public int Length => Rows.Length;

        public object this[int index] => Rows[index];

        public object GetItem(int index) => Rows.GetItem(index);

        public object CreateSlice(IEnumerable<object> items) => new PyList(items, _governor, _span);

        public object GetIndex(int index) => Rows.GetIndex(index);

        public object GetSlice(IEnumerable<int> indices) => Rows.GetSlice(indices);

        public bool IsTruthy() => Rows.IsTruthy();

        public IEnumerable<object> Iterate() => Rows;

        public PyString RenderPython(PyRenderingContext context) => Rows.RenderPython(context);

        public PyString RenderInterpolated(PyRenderingContext context) => Rows.RenderInterpolated(context);

        public IEnumerator<object> GetEnumerator() => Rows.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class CsvWriterObject
    {
        public CsvWriterObject(CsvOptions options, ExecutionContext.TextFileHandle? file)
        {
            Options = options;
            File = file;
        }

        public readonly CsvOptions Options;
        public readonly ExecutionContext.TextFileHandle? File;
        public readonly List<CsvCell[]> Rows = new();
    }

    internal sealed class CsvDictWriterObject
    {
        public CsvDictWriterObject(CsvWriterObject writer, PyString[] fieldNames, object restValue, string extrasAction)
        {
            Writer = writer;
            FieldNames = fieldNames;
            RestValue = restValue;
            ExtrasAction = extrasAction;
        }

        public CsvWriterObject Writer { get; }

        public PyString[] FieldNames { get; }

        public object RestValue { get; }

        public string ExtrasAction { get; }
    }

    internal readonly record struct CsvCell(PyString Text, bool IsNumeric);

    internal static class CsvReaderMembers
    {
        public static bool TryGetMember(CsvReaderObject reader, string name, out object value)
        {
            value = name switch
            {
                "line_num" => new BigInteger(reader.LineNum),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class CsvDictReaderMembers
    {
        public static bool TryGetMember(CsvDictReaderObject reader, string name, out object value)
        {
            value = name switch
            {
                "fieldnames" => reader.FieldNames is null ? PyNone.Instance : new PyList(reader.FieldNames),
                "line_num" => new BigInteger(reader.LineNum),
                _ => null!
            };

            return value is not null;
        }
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

                    WriteRow(writer, ToCsvRow(arguments[0], span), span);
                    return PyNone.Instance;
                }, "csv.writerow", ["row"]),
                "writerows" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.writerows(rows) expects one argument.", span);
                    }

                    foreach (var row in ToSequence(arguments[0], span))
                    {
                        WriteRow(writer, ToCsvRow(row, span), span);
                    }

                    return PyNone.Instance;
                }, "csv.writerows", ["rows"]),
                "getvalue" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.getvalue() expects no arguments.", span);
                    }

                    return RenderCsvDocument(writer.Rows, writer.Options, trailingTerminator: false, span);
                }),
                _ => null!,
            };

            return value is not null;
        }

        public static void WriteRow(CsvWriterObject writer, CsvCell[] row, LythonSourceSpan span)
        {
            writer.Rows.Add(row);
            if (writer.File is not null)
            {
                var rendered = RenderCsvDocument([row], writer.Options, trailingTerminator: true, span);
                writer.File.Write(rendered);
            }
        }

        public static CsvCell[] ToCsvRow(object row, LythonSourceSpan span)
        {
            var cells = new List<CsvCell>();
            foreach (var cell in ToSequence(row, span))
            {
                cells.Add(cell switch
                {
                    PyNone => new CsvCell(PyString.Empty, IsNumeric: false),
                    PyString text => new CsvCell(text, IsNumeric: false),
                    BigInteger integer => new CsvCell(PyString.FromString(integer.ToString()), IsNumeric: true),
                    bool boolean => new CsvCell(PyString.FromString(boolean ? "True" : "False"), IsNumeric: false),
                    double floating => new CsvCell(PyString.FromString(floating.ToString(System.Globalization.CultureInfo.InvariantCulture)), IsNumeric: true),
                    _ => throw new LythonRuntimeException("TypeError", "CSV rows must contain scalar values.", span)
                });
            }

            return [.. cells];
        }

        public static PyString RenderCsvDocument(IReadOnlyList<CsvCell[]> rows, CsvOptions options, bool trailingTerminator, LythonSourceSpan span)
        {
            var builder = new Utf8ValueBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i != 0)
                {
                    builder.Append(options.LineTerminator);
                }

                builder.Append(RenderCsvRow(rows[i], options, span));
            }

            if (trailingTerminator && rows.Count > 0)
            {
                builder.Append(options.LineTerminator);
            }

            return builder.ToPyString();
        }

        private static PyString RenderCsvRow(CsvCell[] row, CsvOptions options, LythonSourceSpan span)
        {
            var builder = new Utf8ValueBuilder();
            for (var i = 0; i < row.Length; i++)
            {
                if (i != 0)
                {
                    builder.Append(options.Delimiter);
                }

                builder.Append(EscapeCsvField(row[i], options, span));
            }

            return builder.ToPyString();
        }

        private static PyString EscapeCsvField(CsvCell cell, CsvOptions options, LythonSourceSpan span)
        {
            var field = cell.Text;
            var fieldBytes = field.Utf8Bytes.Span;
            var delimiterBytes = options.Delimiter.Utf8Bytes.Span;
            var quoteBytes = options.QuoteChar is null ? ReadOnlySpan<byte>.Empty : options.QuoteChar.Utf8Bytes.Span;
            var needsQuotes =
                options.Quoting == CsvQuoteAll ||
                options.Quoting == CsvQuoteNonNumeric && !cell.IsNumeric ||
                options.Quoting == CsvQuoteMinimal && (
                IndexOfBytes(fieldBytes, delimiterBytes) >= 0 ||
                fieldBytes.IndexOf((byte)'\n') >= 0 ||
                fieldBytes.IndexOf((byte)'\r') >= 0 ||
                (options.QuoteChar is not null && IndexOfBytes(fieldBytes, quoteBytes) >= 0));

            if (options.Quoting == CsvQuoteNone)
            {
                return EscapeUnquotedField(field, options, span);
            }

            if (!needsQuotes || options.QuoteChar is null)
            {
                return field;
            }

            var builder = new Utf8ValueBuilder(fieldBytes.Length + 2);
            builder.Append(options.QuoteChar);
            for (var i = 0; i < fieldBytes.Length; i++)
            {
                if (MatchesAt(fieldBytes, i, quoteBytes))
                {
                    if (options.DoubleQuote)
                    {
                        builder.Append(options.QuoteChar);
                    }
                    else if (options.EscapeChar is not null)
                    {
                        builder.Append(options.EscapeChar);
                    }
                    else
                    {
                        throw CsvError("need to escape, but no escapechar set", span);
                    }
                }

                builder.Append(fieldBytes[i]);
            }

            builder.Append(options.QuoteChar);
            return builder.ToPyString();
        }

        private static PyString EscapeUnquotedField(PyString field, CsvOptions options, LythonSourceSpan span)
        {
            var fieldBytes = field.Utf8Bytes.Span;
            var delimiterBytes = options.Delimiter.Utf8Bytes.Span;
            var quoteBytes = options.QuoteChar is null ? ReadOnlySpan<byte>.Empty : options.QuoteChar.Utf8Bytes.Span;
            var builder = new Utf8ValueBuilder(fieldBytes.Length);
            for (var i = 0; i < fieldBytes.Length; i++)
            {
                var needsEscape =
                    MatchesAt(fieldBytes, i, delimiterBytes) ||
                    fieldBytes[i] is (byte)'\n' or (byte)'\r' ||
                    (options.QuoteChar is not null && MatchesAt(fieldBytes, i, quoteBytes));
                if (needsEscape)
                {
                    if (options.EscapeChar is null)
                    {
                        throw CsvError("need to escape, but no escapechar set", span);
                    }

                    builder.Append(options.EscapeChar);
                }

                builder.Append(fieldBytes[i]);
            }

            return builder.ToPyString();
        }

        private static bool MatchesAt(ReadOnlySpan<byte> text, int index, ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty || index + value.Length > text.Length)
            {
                return false;
            }

            return text.Slice(index, value.Length).SequenceEqual(value);
        }
    }

    internal static class CsvDictWriterMembers
    {
        public static bool TryGetMember(CsvDictWriterObject writer, string name, out object value)
        {
            value = name switch
            {
                "writeheader" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writeheader() expects no arguments.", span);
                    }

                    var row = new PyDict();
                    foreach (var fieldName in writer.FieldNames)
                    {
                        row.SetItem(fieldName, fieldName);
                    }

                    CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, row, span), span);
                    return PyNone.Instance;
                }),
                "writerow" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerow(rowdict) expects one argument.", span);
                    }

                    CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, arguments[0], span), span);
                    return PyNone.Instance;
                }, "csv.DictWriter.writerow", ["rowdict"]),
                "writerows" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "csv.DictWriter.writerows(rowdicts) expects one argument.", span);
                    }

                    foreach (var row in ToSequence(arguments[0], span))
                    {
                        CsvWriterMembers.WriteRow(writer.Writer, ToDictCsvRow(writer, row, span), span);
                    }

                    return PyNone.Instance;
                }, "csv.DictWriter.writerows", ["rowdicts"]),
                _ => null!
            };

            return value is not null;
        }

        private static CsvCell[] ToDictCsvRow(CsvDictWriterObject writer, object row, LythonSourceSpan span)
        {
            if (row is not PyDict dict)
            {
                throw new LythonRuntimeException("TypeError", "csv.DictWriter rows must be dictionaries.", span);
            }

            var known = new HashSet<object>(writer.FieldNames, PyValueComparer.Instance);
            foreach (var key in dict.Keys)
            {
                if (!known.Contains(key))
                {
                    if (string.Equals(writer.ExtrasAction, "ignore", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    throw CsvError("dict contains fields not in fieldnames", span);
                }
            }

            var cells = new List<object>(writer.FieldNames.Length);
            foreach (var fieldName in writer.FieldNames)
            {
                cells.Add(dict.TryGetValue(fieldName, out var value) ? value : writer.RestValue);
            }

            return CsvWriterMembers.ToCsvRow(new PyList(cells), span);
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
