using System.Numerics;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class MathModule
    {
        private const int MaxExactLoopCount = 1_000_000;

        private static readonly double[] LanczosCoefficients =
        [
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7,
        ];

        private static object Factorial(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var n = ExpectNonNegativeInteger(arguments[0], "math.factorial", span, context);
            var count = ExpectBoundedLoopCount(n, "math.factorial", span);
            var result = BigInteger.One;
            for (var i = 2; i <= count; i++)
            {
                result *= i;
            }

            return OwnHeapInteger(result, context.MemoryGovernor, span);
        }

        private static object Gcd(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var result = BigInteger.Zero;
            foreach (var argument in arguments)
            {
                result = BigInteger.GreatestCommonDivisor(result, BigInteger.Abs(ExpectInteger(argument, "math.gcd", span, context)));
            }

            return OwnHeapInteger(result, context.MemoryGovernor, span);
        }

        private static object Lcm(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var result = BigInteger.One;
            foreach (var argument in arguments)
            {
                var value = BigInteger.Abs(ExpectInteger(argument, "math.lcm", span, context));
                if (result.IsZero || value.IsZero)
                {
                    result = BigInteger.Zero;
                    continue;
                }

                result = BigInteger.Abs(result / BigInteger.GreatestCommonDivisor(result, value) * value);
            }

            return OwnHeapInteger(result, context.MemoryGovernor, span);
        }

        private static object Comb(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var n = ExpectNonNegativeInteger(arguments[0], "math.comb", span, context);
            var k = ExpectNonNegativeInteger(arguments[1], "math.comb", span, context);
            if (k > n)
            {
                return BigInteger.Zero;
            }

            k = BigInteger.Min(k, n - k);
            var count = ExpectBoundedLoopCount(k, "math.comb", span);
            var result = BigInteger.One;
            for (var i = 1; i <= count; i++)
            {
                result = result * (n - count + i) / i;
            }

            return OwnHeapInteger(result, context.MemoryGovernor, span);
        }

        private static object Perm(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var n = ExpectNonNegativeInteger(arguments[0], "math.perm", span, context);
            var k = arguments.Length >= 2 && !ReferenceEquals(arguments[1], PyNone.Instance)
                ? ExpectNonNegativeInteger(arguments[1], "math.perm", span, context)
                : n;
            if (k > n)
            {
                return BigInteger.Zero;
            }

            var count = ExpectBoundedLoopCount(k, "math.perm", span);
            var result = BigInteger.One;
            for (var i = 0; i < count; i++)
            {
                result *= n - i;
            }

            return OwnHeapInteger(result, context.MemoryGovernor, span);
        }

        private static object ISqrt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            return OwnHeapInteger(IntegerSquareRoot(ExpectNonNegativeInteger(arguments[0], "math.isqrt", span, context)), context.MemoryGovernor, span);
        }

        private static object Dist(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var p = ToSequence(arguments[0], span, context).ToArray();
            var q = ToSequence(arguments[1], span, context).ToArray();
            if (p.Length != q.Length)
            {
                throw new LythonRuntimeException("ValueError", "both points must have the same number of dimensions", span);
            }

            if (p.Length == 0)
            {
                return 0.0;
            }

            var coordinates = new double[p.Length];
            for (var i = 0; i < p.Length; i++)
            {
                coordinates[i] = ExpectReal(p[i], "math.dist", span) - ExpectReal(q[i], "math.dist", span);
            }

            return ScaledHypot(coordinates);
        }

        private static object Frexp(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.frexp", span, context);
            if (value == 0.0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return PyTuple.FromOwnedArray([value, BigInteger.Zero], context.MemoryGovernor, span);
            }

            var exponent = 0;
            var mantissa = value;
            var abs = Math.Abs(mantissa);
            while (abs < 0.5)
            {
                mantissa *= 2.0;
                exponent--;
                abs *= 2.0;
            }

            while (abs >= 1.0)
            {
                mantissa *= 0.5;
                exponent++;
                abs *= 0.5;
            }

            return PyTuple.FromOwnedArray([mantissa, new BigInteger(exponent)], context.MemoryGovernor, span);
        }

        private static object Ldexp(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectReal(arguments[0], "math.ldexp", span);
            var exponent = ExpectInteger(arguments[1], "math.ldexp", span, context);
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0.0)
            {
                return value;
            }

            if (exponent > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "math range error", span);
            }

            if (exponent < int.MinValue)
            {
                return Math.CopySign(0.0, value);
            }

            return CheckedMathResult(Math.ScaleB(value, (int)exponent), "math.ldexp", span, value);
        }

        private static object Modf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.modf", span, context);
            double fractional;
            double integral;
            if (double.IsInfinity(value))
            {
                fractional = Math.CopySign(0.0, value);
                integral = value;
            }
            else if (double.IsNaN(value))
            {
                fractional = double.NaN;
                integral = double.NaN;
            }
            else
            {
                integral = Math.Truncate(value);
                fractional = value - integral;
                if (fractional == 0.0)
                {
                    fractional = Math.CopySign(0.0, value);
                }
            }

            return PyTuple.FromOwnedArray([fractional, integral], context.MemoryGovernor, span);
        }

        private static object Remainder(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.remainder", span, context);
            if (y == 0.0 || double.IsInfinity(x))
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.IEEERemainder(x, y);
        }

        private static object NextAfter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var x = ExpectReal(arguments[0], "math.nextafter", span);
            var y = ExpectReal(arguments[1], "math.nextafter", span);
            var steps = arguments.Length >= 3 && !ReferenceEquals(arguments[2], PyNone.Instance)
                ? ExpectInteger(arguments[2], "math.nextafter", span, context)
                : BigInteger.One;
            if (steps < BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "steps must be a non-negative integer", span);
            }

            if (steps.IsZero)
            {
                return x;
            }

            if (steps > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "math.nextafter steps are too large for Lython.", span);
            }

            var current = x;
            for (var i = 0; i < (int)steps; i++)
            {
                if (current == y)
                {
                    return y;
                }

                current = NextAfterOne(current, y);
            }

            return current;
        }

        private static object Ulp(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.ulp", span, context);
            if (double.IsNaN(value))
            {
                return double.NaN;
            }

            if (double.IsInfinity(value))
            {
                return double.PositiveInfinity;
            }

            var abs = Math.Abs(value);
            if (abs == 0.0)
            {
                return double.Epsilon;
            }

            var next = Math.BitIncrement(abs);
            return double.IsInfinity(next) ? abs - Math.BitDecrement(abs) : next - abs;
        }

        private static object Exp2(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.exp2", span, context, static value => Math.Pow(2.0, value));

        private static object Expm1(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.expm1", span, context, Expm1Accurate);

        private static object Log1p(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.log1p", span, context);
            if (value <= -1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Log1pAccurate(value);
        }

        private static object Cbrt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.cbrt", span, context, Math.Cbrt);

        private static object Erf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.erf", span, context, FloatingPointSpecialFunctions.Erf);

        private static object Erfc(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.erfc", span, context, FloatingPointSpecialFunctions.Erfc);

        private static object Gamma(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.gamma", span, context);
            if (double.IsNaN(value))
            {
                return double.NaN;
            }

            if (double.IsPositiveInfinity(value))
            {
                return double.PositiveInfinity;
            }

            if (double.IsNegativeInfinity(value) || IsNonPositiveInteger(value))
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return CheckedMathResult(GammaLanczos(value), "math.gamma", span, value);
        }

        private static object LGamma(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.lgamma", span, context);
            if (double.IsNaN(value))
            {
                return double.NaN;
            }

            if (double.IsInfinity(value))
            {
                return double.PositiveInfinity;
            }

            if (IsNonPositiveInteger(value))
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return LogGammaLanczos(value);
        }

        private static object Fma(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var x = ExpectReal(arguments[0], "math.fma", span);
            var y = ExpectReal(arguments[1], "math.fma", span);
            var z = ExpectReal(arguments[2], "math.fma", span);
            if ((double.IsInfinity(x) && y == 0.0 || double.IsInfinity(y) && x == 0.0) && !double.IsNaN(z))
            {
                throw new LythonRuntimeException("ValueError", "invalid operation in fma", span);
            }

            var result = Math.FusedMultiplyAdd(x, y, z);
            if (double.IsInfinity(result) && double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z))
            {
                throw new LythonRuntimeException("OverflowError", "overflow in fma", span);
            }

            return result;
        }

        private static object SumProd(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var p = ToSequence(arguments[0], span, context).ToArray();
            var q = ToSequence(arguments[1], span, context).ToArray();
            if (p.Length != q.Length)
            {
                throw new LythonRuntimeException("ValueError", "Inputs are not the same length", span);
            }

            object total = BigInteger.Zero;
            for (var i = 0; i < p.Length; i++)
            {
                if (!PyNumberOps.TryAsNumber(p[i], out var left) || !PyNumberOps.TryAsNumber(q[i], out var right))
                {
                    throw new LythonRuntimeException("TypeError", "math.sumprod(...) expects iterables of real numbers.", span);
                }

                var product = OwnHeapInteger(PyNumberOps.Multiply(left, right), context.MemoryGovernor, span);
                total = EvaluateAdd(total, product, context, span);
            }

            return RuntimeValue(total);
        }

        private static BigInteger ExpectInteger(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            var converted = CoerceIndexProtocol(value, context, span);
            if (!PyNumberOps.TryAsInteger(converted, out var integer))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects integer arguments.", span);
            }

            return integer;
        }

        private static BigInteger ExpectNonNegativeInteger(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            var integer = ExpectInteger(value, owner, span, context);
            if (integer < BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects non-negative integer arguments.", span);
            }

            return integer;
        }

        private static int ExpectBoundedLoopCount(BigInteger value, string owner, LythonSourceSpan span)
        {
            if (value > MaxExactLoopCount)
            {
                throw new LythonRuntimeException("OverflowError", $"{owner} argument is too large for an exact Lython computation.", span);
            }

            return (int)value;
        }

        private static BigInteger IntegerSquareRoot(BigInteger value)
        {
            if (value < 2)
            {
                return value;
            }

            var x = value;
            var y = (x + value / x) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + value / x) >> 1;
            }

            return x;
        }

        private static double ScaledHypot(IEnumerable<double> values)
        {
            var max = 0.0;
            var materialized = values.Select(Math.Abs).ToArray();
            foreach (var value in materialized)
            {
                if (double.IsPositiveInfinity(value))
                {
                    return double.PositiveInfinity;
                }

                max = Math.Max(max, value);
            }

            if (max == 0.0 || double.IsNaN(max))
            {
                return max;
            }

            var scaled = 0.0;
            foreach (var value in materialized)
            {
                var ratio = value / max;
                scaled += ratio * ratio;
            }

            return max * Math.Sqrt(scaled);
        }

        private static double NextAfterOne(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                return double.NaN;
            }

            if (x == y)
            {
                return y;
            }

            if (x == 0.0)
            {
                return Math.CopySign(double.Epsilon, y);
            }

            return y > x ? Math.BitIncrement(x) : Math.BitDecrement(x);
        }

        private static bool IsNonPositiveInteger(double value)
            => value <= 0.0 && Math.Truncate(value) == value;

        private static double Expm1Accurate(double value)
        {
            if (Math.Abs(value) >= 1e-5 || !double.IsFinite(value))
            {
                return Math.Exp(value) - 1.0;
            }

            var term = value;
            var sum = value;
            for (var n = 2; n <= 18; n++)
            {
                term *= value / n;
                sum += term;
            }

            return sum;
        }

        private static double Log1pAccurate(double value)
        {
            if (Math.Abs(value) >= 1e-4)
            {
                return Math.Log(1.0 + value);
            }

            var term = value;
            var sum = value;
            for (var n = 2; n <= 24; n++)
            {
                term *= -value;
                sum += term / n;
            }

            return sum;
        }

        private static double GammaLanczos(double value)
        {
            if (value < 0.5)
            {
                return Math.PI / (Math.Sin(Math.PI * value) * GammaLanczos(1.0 - value));
            }

            var z = value - 1.0;
            var x = 0.99999999999980993;
            for (var i = 0; i < LanczosCoefficients.Length; i++)
            {
                x += LanczosCoefficients[i] / (z + i + 1.0);
            }

            var t = z + LanczosCoefficients.Length - 0.5;
            return Math.Sqrt(2.0 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * x;
        }

        private static double LogGammaLanczos(double value)
        {
            if (value < 0.5)
            {
                return Math.Log(Math.PI) - Math.Log(Math.Abs(Math.Sin(Math.PI * value))) - LogGammaLanczos(1.0 - value);
            }

            var z = value - 1.0;
            var x = 0.99999999999980993;
            for (var i = 0; i < LanczosCoefficients.Length; i++)
            {
                x += LanczosCoefficients[i] / (z + i + 1.0);
            }

            var t = z + LanczosCoefficients.Length - 0.5;
            return 0.5 * Math.Log(2.0 * Math.PI) + (z + 0.5) * Math.Log(t) - t + Math.Log(x);
        }
    }
}
