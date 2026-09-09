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
    private sealed partial class MathModule : PyModule
    {
        private readonly record struct BinaryRealArguments(double X, double Y);

        public static readonly MathModule Instance = new();

        private MathModule() : base("math")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "pi" => Math.PI,
                "e" => Math.E,
                "tau" => Math.Tau,
                "inf" => double.PositiveInfinity,
                "nan" => double.NaN,
                "sqrt" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathSqrt, Sqrt),
                "exp" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathExp, Exp),
                "log" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLog, Log),
                "log10" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLog10, Log10),
                "log2" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLog2, Log2),
                "sin" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathSin, Sin),
                "cos" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathCos, Cos),
                "tan" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathTan, Tan),
                "asin" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathAsin, Asin),
                "acos" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathAcos, Acos),
                "atan" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathAtan, Atan),
                "atan2" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathAtan2, Atan2),
                "sinh" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathSinh, Sinh),
                "cosh" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathCosh, Cosh),
                "tanh" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathTanh, Tanh),
                "floor" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFloor, Floor),
                "ceil" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathCeil, Ceil),
                "fabs" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFabs, Fabs),
                "trunc" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathTrunc, Trunc),
                "degrees" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathDegrees, Degrees),
                "radians" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathRadians, Radians),
                "isfinite" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathIsFinite, IsFinite),
                "isinf" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathIsInf, IsInf),
                "isnan" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathIsNaN, IsNaN),
                "pow" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathPow, Pow),
                "hypot" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathHypot, Hypot),
                "fmod" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFmod, Fmod),
                "copysign" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathCopySign, CopySign),
                "isclose" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathIsClose, IsClose),
                "prod" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathProd, Prod),
                "fsum" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFsum, Fsum),
                "factorial" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFactorial, Factorial),
                "gcd" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathGcd, Gcd),
                "lcm" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLcm, Lcm),
                "comb" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathComb, Comb),
                "perm" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathPerm, Perm),
                "isqrt" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathIsqrt, ISqrt),
                "dist" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathDist, Dist),
                "frexp" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFrexp, Frexp),
                "ldexp" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLdexp, Ldexp),
                "modf" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathModf, Modf),
                "remainder" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathRemainder, Remainder),
                "nextafter" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathNextAfter, NextAfter),
                "ulp" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathUlp, Ulp),
                "exp2" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathExp2, Exp2),
                "expm1" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathExpm1, Expm1),
                "log1p" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLog1p, Log1p),
                "cbrt" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathCbrt, Cbrt),
                "erf" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathErf, Erf),
                "erfc" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathErfc, Erfc),
                "gamma" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathGamma, Gamma),
                "lgamma" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathLgamma, LGamma),
                "fma" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathFma, Fma),
                "sumprod" => BuiltinCallable.Create(LythonKnownCallableSignatures.MathSumProd, SumProd),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
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
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.floor(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.floor", "__floor__", span, context, static x => Math.Floor(x));
        }

        private static object Ceil(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.ceil(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.ceil", "__ceil__", span, context, static x => Math.Ceiling(x));
        }

        private static object Fabs(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.fabs", span, context, Math.Abs);

        private static object Trunc(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.trunc(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.trunc", "__trunc__", span, context, static x => Math.Truncate(x));
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
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                total = OwnHeapInteger(MultiplyNumeric(total, item, span), context.MemoryGovernor, span);
            }

            return RuntimeValue(total);
        }

        private static object Fsum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.fsum(iterable) expects one iterable.", span);
            }

            var partials = new List<double>();
            var infinitySign = 0;
            var sawNaN = false;
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                var value = ExpectReal(item, "math.fsum", span);
                if (double.IsNaN(value))
                {
                    sawNaN = true;
                    continue;
                }

                if (double.IsInfinity(value))
                {
                    var sign = value > 0.0 ? 1 : -1;
                    if (infinitySign != 0 && infinitySign != sign)
                    {
                        throw new LythonRuntimeException("ValueError", "-inf + inf in fsum", span);
                    }

                    infinitySign = sign;
                    continue;
                }

                var x = value;
                var writeIndex = 0;
                for (var i = 0; i < partials.Count; i++)
                {
                    var y = partials[i];
                    if (Math.Abs(x) < Math.Abs(y))
                    {
                        (x, y) = (y, x);
                    }

                    var high = x + y;
                    if (double.IsInfinity(high))
                    {
                        throw new LythonRuntimeException("OverflowError", "intermediate overflow in fsum", span);
                    }

                    var low = y - (high - x);
                    if (low != 0.0)
                    {
                        partials[writeIndex++] = low;
                    }

                    x = high;
                }

                if (writeIndex < partials.Count)
                {
                    partials.RemoveRange(writeIndex, partials.Count - writeIndex);
                }

                partials.Add(x);
            }

            if (infinitySign != 0)
            {
                return infinitySign > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            }

            if (sawNaN)
            {
                return double.NaN;
            }

            var total = 0.0;
            for (var i = partials.Count - 1; i >= 0; i--)
            {
                total += partials[i];
            }

            return total;
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

        private static BinaryRealArguments ExpectBinaryReal(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two numeric arguments.", span);
            }

            return new BinaryRealArguments(ExpectReal(arguments[0], owner, span), ExpectReal(arguments[1], owner, span));
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

        private static object ExpectFloorLike(
            object value,
            string owner,
            string specialMethod,
            LythonSourceSpan span,
            ExecutionContext context,
            Func<double, double> func)
        {
            if (value is PyInstance instance &&
                instance.TryGetAttribute(specialMethod, context, span, out var member) &&
                member is ICallable callable)
            {
                return callable.Invoke([], span, context);
            }

            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            if (!number.IsFloat)
            {
                return number.Integer;
            }

            return OwnHeapInteger(FloatToInteger(number.Floating, owner, span, func), context.MemoryGovernor, span);
        }
    }

    private sealed class DatetimeModule : PyModule
    {
        public static readonly DatetimeModule Instance = new();

        private DatetimeModule() : base("datetime")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

}
