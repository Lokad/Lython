using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        private static object Uniform(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.uniform(a, b) expects two real arguments.", span);
            }

            var a = ExpectReal(arguments[0], "random.uniform(a, b)", span);
            var b = ExpectReal(arguments[1], "random.uniform(a, b)", span);
            return a + (b - a) * state.NextDouble();
        }

        private static object Triangular(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 3)
            {
                throw new LythonRuntimeException("TypeError", "random.triangular(low=0.0, high=1.0, mode=None) expects zero to three arguments.", span);
            }

            var low = arguments.Length >= 1 && arguments[0] is not PyNone ? ExpectReal(arguments[0], "random.triangular(..., low=...)", span) : 0.0;
            var high = arguments.Length >= 2 && arguments[1] is not PyNone ? ExpectReal(arguments[1], "random.triangular(..., high=...)", span) : 1.0;
            if (low == high)
            {
                return low;
            }

            var mode = arguments.Length >= 3 && arguments[2] is not PyNone
                ? ExpectReal(arguments[2], "random.triangular(..., mode=...)", span)
                : (low + high) / 2.0;
            var min = Math.Min(low, high);
            var max = Math.Max(low, high);
            if (mode < min || mode > max)
            {
                throw new LythonRuntimeException("ValueError", "random.triangular(..., mode=...) must lie between low and high.", span);
            }

            var u = state.NextDouble();
            var c = (mode - low) / (high - low);
            if (u > c)
            {
                u = 1.0 - u;
                (low, high) = (high, low);
                c = 1.0 - c;
            }

            return low + (high - low) * Math.Sqrt(u * c);
        }

        private static object BetaVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.betavariate(alpha, beta) expects two positive real arguments.", span);
            }

            var alpha = ExpectPositiveReal(arguments[0], "random.betavariate(..., alpha=...)", span);
            var beta = ExpectPositiveReal(arguments[1], "random.betavariate(..., beta=...)", span);
            var y = SampleGamma(state, alpha, 1.0);
            var z = SampleGamma(state, beta, 1.0);
            return y / (y + z);
        }

        private static object ExpVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "random.expovariate(lambd=1.0) expects zero or one real argument.", span);
            }

            var lambd = arguments.Length == 1 && arguments[0] is not PyNone ? ExpectReal(arguments[0], "random.expovariate(..., lambd=...)", span) : 1.0;
            if (lambd == 0.0)
            {
                throw new LythonRuntimeException("ValueError", "random.expovariate(..., lambd=...) must be non-zero.", span);
            }

            return -Math.Log(NonZeroRandom(state)) / lambd;
        }

        private static object GammaVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.gammavariate(alpha, beta) expects two positive real arguments.", span);
            }

            return SampleGamma(
                state,
                ExpectPositiveReal(arguments[0], "random.gammavariate(..., alpha=...)", span),
                ExpectPositiveReal(arguments[1], "random.gammavariate(..., beta=...)", span));
        }

        private static object NormalVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context, string owner)
        {
            _ = context;
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(mu=0.0, sigma=1.0) expects zero to two real arguments.", span);
            }

            var mu = arguments.Length >= 1 && arguments[0] is not PyNone ? ExpectReal(arguments[0], owner + "(..., mu=...)", span) : 0.0;
            var sigma = arguments.Length >= 2 && arguments[1] is not PyNone ? ExpectReal(arguments[1], owner + "(..., sigma=...)", span) : 1.0;
            return mu + sigma * StandardNormal(state);
        }

        private static object LogNormVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.lognormvariate(mu, sigma) expects two real arguments.", span);
            }

            return Math.Exp(ExpectReal(arguments[0], "random.lognormvariate(..., mu=...)", span) +
                            ExpectReal(arguments[1], "random.lognormvariate(..., sigma=...)", span) * StandardNormal(state));
        }

        private static object ParetoVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.paretovariate(alpha) expects one positive real argument.", span);
            }

            var alpha = ExpectPositiveReal(arguments[0], "random.paretovariate(alpha)", span);
            return 1.0 / Math.Pow(NonZeroRandom(state), 1.0 / alpha);
        }

        private static object VonMisesVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.vonmisesvariate(mu, kappa) expects two real arguments.", span);
            }

            var mu = ExpectReal(arguments[0], "random.vonmisesvariate(..., mu=...)", span);
            var kappa = ExpectReal(arguments[1], "random.vonmisesvariate(..., kappa=...)", span);
            if (kappa < 0.0)
            {
                throw new LythonRuntimeException("ValueError", "random.vonmisesvariate(..., kappa=...) expects kappa >= 0.", span);
            }

            if (kappa <= 1e-6)
            {
                return ModTwoPi(mu + TwoPi * state.NextDouble());
            }

            var s = 0.5 / kappa;
            var r = s + Math.Sqrt(1.0 + s * s);
            double f;
            while (true)
            {
                var u1 = state.NextDouble();
                var z = Math.Cos(Math.PI * u1);
                var d = z / (r + z);
                var u2 = state.NextDouble();
                if (u2 < 1.0 - d * d || u2 <= (1.0 - d) * Math.Exp(d))
                {
                    var q = 1.0 / r;
                    f = (q + z) / (1.0 + q * z);
                    break;
                }
            }

            var sign = state.NextDouble() > 0.5 ? 1.0 : -1.0;
            return ModTwoPi(mu + sign * Math.Acos(f));
        }

        private static object WeibullVariate(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.weibullvariate(alpha, beta) expects two positive real arguments.", span);
            }

            var alpha = ExpectPositiveReal(arguments[0], "random.weibullvariate(..., alpha=...)", span);
            var beta = ExpectPositiveReal(arguments[1], "random.weibullvariate(..., beta=...)", span);
            return alpha * Math.Pow(-Math.Log(NonZeroRandom(state)), 1.0 / beta);
        }

        private static ulong ParseState(object value, LythonSourceSpan span)
        {
            var items = MaterializeSequence(value, span);
            if (items.Count != 2 ||
                !PyStringOps.TryAsString(items[0], out var tag) ||
                tag.AsString() != StateTag ||
                items[1] is not BigInteger stateValue ||
                stateValue < BigInteger.Zero ||
                stateValue > MaxUInt64)
            {
                throw new LythonRuntimeException("ValueError", "random.setstate(state) expects a state object returned by random.getstate().", span);
            }

            return (ulong)stateValue;
        }

        private static ulong ParseSeed(object value, LythonSourceSpan span)
        {
            return value switch
            {
                null or PyNone => 0x5A17_1C0D_DA7A_2026UL,
                bool boolean => boolean ? 1UL : 0UL,
                BigInteger integer when integer.Sign >= 0 => FoldBytes(integer.ToByteArray(isUnsigned: true, isBigEndian: false)),
                BigInteger integer => FoldBytes(integer.ToByteArray(isUnsigned: false, isBigEndian: false)),
                double floating when double.IsFinite(floating) => BitConverter.DoubleToUInt64Bits(floating),
                PyBytes bytes => FoldBytes(bytes.Bytes),
                _ when PyStringOps.TryAsString(value, out var text) => FoldBytes(text.Utf8Bytes.Span),
                _ => throw new LythonRuntimeException("TypeError", "random.seed(a) only supports None, bool, int, float, str, and bytes in Lython.", span)
            };
        }

        private static ulong FoldBytes(ReadOnlySpan<byte> bytes)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            for (var i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash;
        }

        private static (BigInteger Start, BigInteger Stop, BigInteger Step) ParseRangeArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length is < 1 or > 3)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(start, stop[, step]) expects one to three integer arguments.", span);
            }

            var startArg = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
            var stopArg = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
            var stepArg = arguments.Length >= 3 ? arguments[2] : PyNone.Instance;
            if (startArg is PyNone && stopArg is PyNone)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(start, stop[, step]) expects one to three integer arguments.", span);
            }

            BigInteger start;
            BigInteger stop;
            if (stopArg is PyNone)
            {
                start = BigInteger.Zero;
                stop = ExpectInteger(startArg, $"{owner}(...) expects integer arguments.", span);
            }
            else
            {
                start = startArg is PyNone ? BigInteger.Zero : ExpectInteger(startArg, $"{owner}(...) expects integer arguments.", span);
                stop = ExpectInteger(stopArg, $"{owner}(...) expects integer arguments.", span);
            }

            var step = stepArg is PyNone ? BigInteger.One : ExpectInteger(stepArg, $"{owner}(...) expects integer arguments.", span);
            return (start, stop, step);
        }
    }
}
