using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

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
                "nan" => PythonNaN,
                "sqrt" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Sqrt, Sqrt),
                "exp" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Exp, Exp),
                "log" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Log, Log),
                "log10" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Log10, Log10),
                "log2" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Log2, Log2),
                "sin" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Sin, Sin),
                "cos" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Cos, Cos),
                "tan" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Tan, Tan),
                "asin" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Asin, Asin),
                "acos" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Acos, Acos),
                "atan" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Atan, Atan),
                "atan2" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Atan2, Atan2),
                "sinh" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Sinh, Sinh),
                "cosh" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Cosh, Cosh),
                "tanh" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Tanh, Tanh),
                "floor" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Floor, Floor),
                "ceil" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Ceil, Ceil),
                "fabs" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Fabs, Fabs),
                "trunc" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Trunc, Trunc),
                "degrees" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Degrees, Degrees),
                "radians" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Radians, Radians),
                "isfinite" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.IsFinite, IsFinite),
                "isinf" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.IsInf, IsInf),
                "isnan" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.IsNaN, IsNaN),
                "pow" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Pow, Pow),
                "hypot" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Hypot, Hypot),
                "fmod" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Fmod, Fmod),
                "copysign" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.CopySign, CopySign),
                "isclose" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.IsClose, IsClose),
                "prod" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Prod, Prod, ProdAsync),
                "fsum" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Fsum, Fsum, FsumAsync),
                "factorial" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Factorial, Factorial),
                "gcd" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Gcd, Gcd),
                "lcm" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Lcm, Lcm),
                "comb" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Comb, Comb),
                "perm" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Perm, Perm),
                "isqrt" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Isqrt, ISqrt),
                "dist" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.Dist, Dist, DistAsync),
                "frexp" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Frexp, Frexp),
                "ldexp" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Ldexp, Ldexp),
                "modf" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Modf, Modf),
                "remainder" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Remainder, Remainder),
                "nextafter" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.NextAfter, NextAfter),
                "ulp" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Ulp, Ulp),
                "exp2" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Exp2, Exp2),
                "expm1" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Expm1, Expm1),
                "log1p" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Log1p, Log1p),
                "cbrt" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Cbrt, Cbrt),
                "erf" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Erf, Erf),
                "erfc" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Erfc, Erfc),
                "gamma" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Gamma, Gamma),
                "lgamma" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Lgamma, LGamma),
                "fma" => BuiltinCallable.CreateBorrowed(LythonKnownCallableSignatures.Math.Fma, Fma),
                "sumprod" => BuiltinCallable.Create(LythonKnownCallableSignatures.Math.SumProd, SumProd, SumProdAsync),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Sqrt(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.sqrt", span, context);
            if (value < 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Sqrt(value);
        }

        private static object Exp(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.exp", span, context, Math.Exp);

        private static object Log(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
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

        private static object Log10(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.log10", span, context);
            if (value <= 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Log10(value);
        }

        private static object Log2(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.log2", span, context);
            if (value <= 0.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Log2(value);
        }

        private static object Sin(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.sin", span, context, Math.Sin);

        private static object Cos(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.cos", span, context, Math.Cos);

        private static object Tan(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.tan", span, context, Math.Tan);

        private static object Asin(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.asin", span, context);
            if (value is < -1.0 or > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Asin(value);
        }

        private static object Acos(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = ExpectUnaryReal(arguments, "math.acos", span, context);
            if (value is < -1.0 or > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return Math.Acos(value);
        }

        private static object Atan(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.atan", span, context, Math.Atan);

        private static object Atan2(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.atan2", span, context, Math.Atan2);

        private static object Sinh(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.sinh", span, context, Math.Sinh);

        private static object Cosh(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryCheckedFloat(arguments, "math.cosh", span, context, Math.Cosh);

        private static object Tanh(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.tanh", span, context, Math.Tanh);

        private static object Floor(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.floor(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.floor", "__floor__", span, context, static x => Math.Floor(x));
        }

        private static object Ceil(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.ceil(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.ceil", "__ceil__", span, context, static x => Math.Ceiling(x));
        }

        private static object Fabs(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.fabs", span, context, Math.Abs);

        private static object Trunc(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.trunc(x) expects one numeric argument.", span);
            }

            return ExpectFloorLike(arguments[0], "math.trunc", "__trunc__", span, context, static x => Math.Truncate(x));
        }

        private static object Degrees(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.degrees", span, context, static x => x * (180.0 / Math.PI));

        private static object Radians(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryFloat(arguments, "math.radians", span, context, static x => x * (Math.PI / 180.0));

        private static object IsFinite(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isfinite", span, context, static x => double.IsFinite(x));

        private static object IsInf(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isinf", span, context, static x => double.IsInfinity(x));

        private static object IsNaN(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => UnaryBool(arguments, "math.isnan", span, context, static x => double.IsNaN(x));

        private static object Pow(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.pow", span, context);
            if (x == 0.0 && double.IsFinite(y) && y < 0.0)
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

        private static object Fmod(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = ExpectBinaryReal(arguments, "math.fmod", span, context);
            if (y == 0.0 || double.IsInfinity(x))
            {
                throw new LythonRuntimeException("ValueError", "math domain error", span);
            }

            return x % y;
        }

        private static object CopySign(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
            => BinaryFloat(arguments, "math.copysign", span, context, static (x, y) => Math.Abs(x) * (Math.CopySign(1.0, y)));

        private static object IsClose(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
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
            var total = ReadProdStart(arguments, span);
            foreach (var item in PyIteration.Cursor.Create(arguments[0], span, context))
            {
                total = MultiplyNumeric(total, item, span, context);
                context.CheckExecution(span);
            }

            return RuntimeValue(total);
        }

        private static async ValueTask<object> ProdAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // Async twin of the guarded body above: awaits delayed host reads
            // through the async boundary with the same per-pair enforcement.
            var total = ReadProdStart(arguments, span);
            await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(arguments[0], span, context).ConfigureAwait(false))
            {
                total = MultiplyNumeric(total, item, span, context);
                context.CheckExecution(span);
            }

            return RuntimeValue(total);
        }

        private static object Fsum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.fsum(iterable) expects one iterable.", span);
            }

            // Shewchuk partials stay small in practice (one per live binary
            // exponent), but adversarial inputs grow both the scan and the table:
            // count every consumed input and fund partial slots before they grow.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var partials = new List<double>();
            long partialsCharged = 0;
            var infinitySign = 0;
            var sawNaN = false;
            var seen = 0;
            foreach (var item in PyIteration.Cursor.Create(arguments[0], span, context))
            {
                seen++;
                context.CheckExecution(span);
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

                if (partials.Count == partials.Capacity)
                {
                    var predicted = partials.Capacity == 0 ? 4L : (long)partials.Capacity * 2L;
                    if (predicted > partialsCharged)
                    {
                        scratch.Grow(checked(8L * (predicted - partialsCharged)), span);
                        partialsCharged = predicted;
                    }
                }

                partials.Add(x);
                if (partials.Capacity > partialsCharged)
                {
                    scratch.Grow(8L * (partials.Capacity - partialsCharged), span);
                    partialsCharged = partials.Capacity;
                }
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

        private static async ValueTask<object> FsumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "math.fsum(iterable) expects one iterable.", span);
            }

            // Async twin of the guarded body above: drains through the async
            // boundary with the same per-input enforcement.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var partials = new List<double>();
            long partialsCharged = 0;
            var infinitySign = 0;
            var sawNaN = false;
            var seen = 0;
            await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(arguments[0], span, context).ConfigureAwait(false))
            {
                seen++;
                context.CheckExecution(span);
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

                if (partials.Count == partials.Capacity)
                {
                    var predicted = partials.Capacity == 0 ? 4L : (long)partials.Capacity * 2L;
                    if (predicted > partialsCharged)
                    {
                        scratch.Grow(checked(8L * (predicted - partialsCharged)), span);
                        partialsCharged = predicted;
                    }
                }

                partials.Add(x);
                if (partials.Capacity > partialsCharged)
                {
                    scratch.Grow(8L * (partials.Capacity - partialsCharged), span);
                    partialsCharged = partials.Capacity;
                }
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

        private static double ExpectUnaryReal(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x) expects one numeric argument.", span);
            }

            return ExpectReal(arguments[0], owner, span);
        }

        private static BinaryRealArguments ExpectBinaryReal(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two numeric arguments.", span);
            }

            return new BinaryRealArguments(ExpectReal(arguments[0], owner, span), ExpectReal(arguments[1], owner, span));
        }

        private static object UnaryFloat(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double> func)
        {
            var value = ExpectUnaryReal(arguments, owner, span, context);
            return func(value);
        }

        private static object UnaryBool(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, bool> func)
        {
            var value = ExpectUnaryReal(arguments, owner, span, context);
            return func(value);
        }

        private static object BinaryFloat(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double, double> func)
        {
            var (x, y) = ExpectBinaryReal(arguments, owner, span, context);
            return func(x, y);
        }

        private static object UnaryCheckedFloat(ReadOnlySpan<object> arguments, string owner, LythonSourceSpan span, ExecutionContext context, Func<double, double> func)
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

        private static object ReadProdStart(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 2)
                throw new LythonRuntimeException("TypeError", "math.prod(iterable[, start]) expects one iterable and an optional numeric start.", span);
            if (arguments.Length == 1) return BigInteger.One;
            var value = arguments[1];
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", "math.prod expects real numbers.", span);
            }

            // An empty integer product returns its original start (including
            // bool). Python's float accumulator still returns a fresh float.
            return number.IsFloat ? number.Floating : value;
        }

        private static object MultiplyNumeric(object lhs, object rhs, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyNumberOps.TryAsNumber(lhs, out var left) || !PyNumberOps.TryAsNumber(rhs, out var right))
            {
                throw new LythonRuntimeException("TypeError", "math.prod(...) expects an iterable of real numbers.", span);
            }

            try
            {
                return !left.IsFloat && !right.IsFloat
                    ? EvaluateIntegerBinary(Frontend.BinaryOperatorSyntax.Multiply, left.Integer, right.Integer, context, span)
                    : PyNumberOps.ToDoubleChecked(left) * PyNumberOps.ToDoubleChecked(right);
            }
            catch (OverflowException ex)
            {
                throw new LythonRuntimeException("OverflowError", ex.Message, span);
            }
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

            if (value is PyDecimal decimalValue)
            {
                var decimalResult = specialMethod switch
                {
                    "__floor__" => decimal.Floor(decimalValue.Value),
                    "__ceil__" => decimal.Ceiling(decimalValue.Value),
                    _ => decimal.Truncate(decimalValue.Value),
                };
                return OwnHeapInteger(new BigInteger(decimalResult), context.MemoryGovernor, span);
            }

            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            if (!number.IsFloat)
            {
                return value is BigInteger ? value : number.Integer;
            }

            return OwnHeapInteger(FloatToInteger(number.Floating, span, func), context.MemoryGovernor, span);
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
