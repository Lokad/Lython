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

            var partials = new List<double>();
            var infinitySign = 0;
            var sawNaN = false;
            foreach (var item in ToSequence(arguments[0], span))
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

            return FloatToInteger(number.Floating, owner, span, func);
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
