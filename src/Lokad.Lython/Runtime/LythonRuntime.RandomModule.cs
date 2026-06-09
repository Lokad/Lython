using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class RandomModule : PyModule
    {
        private const double TwoPi = Math.PI * 2.0;
        private const string StateTag = "lython.random.state";
        private static readonly BigInteger MaxUInt64 = new(ulong.MaxValue);

        public static readonly PyBuiltinRuntimeType RandomType = new("random.Random", CreateRandom);

        private readonly PyRandomState _state;

        public RandomModule(PyRandomState state) : base("random")
        {
            _state = state;
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "Random" => RandomType,
                "SystemRandom" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSystemRandom, UnsupportedSystemRandom),
                "BPF" => new BigInteger(53),
                "RECIP_BPF" => 1.0 / (1UL << 53),
                "seed" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSeed, (arguments, span, context) => Seed(_state, arguments, span, context)),
                "random" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandom, (arguments, span, context) => Random(_state, arguments, span, context)),
                "getstate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGetState, (arguments, span, context) => GetState(_state, arguments, span, context)),
                "setstate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSetState, (arguments, span, context) => SetState(_state, arguments, span, context)),
                "randrange" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandRange, (arguments, span, context) => RandRange(_state, arguments, span, context)),
                "randint" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandInt, (arguments, span, context) => RandInt(_state, arguments, span, context)),
                "choice" => new BuiltinCallable(LythonKnownCallableSignatures.RandomChoice, (arguments, span, context) => Choice(_state, arguments, span, context)),
                "choices" => new BuiltinCallable(LythonKnownCallableSignatures.RandomChoices, (arguments, span, context) => Choices(_state, arguments, span, context)),
                "shuffle" => new BuiltinCallable(LythonKnownCallableSignatures.RandomShuffle, (arguments, span, context) => Shuffle(_state, arguments, span, context)),
                "sample" => new BuiltinCallable(LythonKnownCallableSignatures.RandomSample, (arguments, span, context) => Sample(_state, arguments, span, context)),
                "getrandbits" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGetRandBits, (arguments, span, context) => GetRandBits(_state, arguments, span, context)),
                "randbytes" => new BuiltinCallable(LythonKnownCallableSignatures.RandomRandBytes, (arguments, span, context) => RandBytes(_state, arguments, span, context)),
                "uniform" => new BuiltinCallable(LythonKnownCallableSignatures.RandomUniform, (arguments, span, context) => Uniform(_state, arguments, span, context)),
                "triangular" => new BuiltinCallable(LythonKnownCallableSignatures.RandomTriangular, (arguments, span, context) => Triangular(_state, arguments, span, context)),
                "betavariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomBetaVariate, (arguments, span, context) => BetaVariate(_state, arguments, span, context)),
                "expovariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomExpVariate, (arguments, span, context) => ExpVariate(_state, arguments, span, context)),
                "gammavariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGammaVariate, (arguments, span, context) => GammaVariate(_state, arguments, span, context)),
                "gauss" => new BuiltinCallable(LythonKnownCallableSignatures.RandomGauss, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.gauss")),
                "normalvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomNormalVariate, (arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.normalvariate")),
                "lognormvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomLogNormVariate, (arguments, span, context) => LogNormVariate(_state, arguments, span, context)),
                "paretovariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomParetoVariate, (arguments, span, context) => ParetoVariate(_state, arguments, span, context)),
                "vonmisesvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomVonMisesVariate, (arguments, span, context) => VonMisesVariate(_state, arguments, span, context)),
                "weibullvariate" => new BuiltinCallable(LythonKnownCallableSignatures.RandomWeibullVariate, (arguments, span, context) => WeibullVariate(_state, arguments, span, context)),
                _ => null!,
            };

            return value is not null;
        }

        private static object CreateRandom(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(arguments, span, LythonKnownCallableSignatures.RandomClass, "Builtin");
            var state = new PyRandomState();
            if (bound.Length >= 1 && bound[0] is not PyNone)
            {
                state.Seed(ParseSeed(bound[0], span));
            }

            _ = context;
            return new PyRandom(state);
        }

        private static object UnsupportedSystemRandom(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", "random.SystemRandom is unsupported by Lython because system entropy is not exposed.", span);
        }

        private static object Seed(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "random.seed(a=None, version=2) expects zero to two arguments.", span);
            }

            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                var version = ExpectInteger(arguments[1], "random.seed(..., version=...) expects version 1 or 2.", span);
                if (version != BigInteger.One && version != new BigInteger(2))
                {
                    throw new LythonRuntimeException("ValueError", "random.seed(..., version=...) expects version 1 or 2.", span);
                }
            }

            state.Seed(ParseSeed(arguments.Length == 0 || arguments[0] is PyNone ? PyNone.Instance : arguments[0], span));
            return PyNone.Instance;
        }

        private static object Random(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "random.random() expects no arguments.", span);
            }

            return state.NextDouble();
        }

        private static object GetState(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "random.getstate() expects no arguments.", span);
            }

            return new PyTuple(
                [
                    PyString.FromString(StateTag),
                    new BigInteger(state.Snapshot())
                ],
                context.MemoryGovernor,
                span);
        }

        private static object SetState(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.setstate(state) expects one state object.", span);
            }

            state.Restore(ParseState(arguments[0], span));
            return PyNone.Instance;
        }

        private static object RandRange(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var (start, stop, step) = ParseRangeArguments(arguments, "random.randrange", span);
            var count = ComputeRangeCount(start, stop, step, "random.randrange", span);
            var offset = new BigInteger(state.NextBelow(ToBound(count, "random.randrange", span)));
            return start + (offset * step);
        }

        private static object RandInt(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.randint(a, b) expects two integer arguments.", span);
            }

            var start = ExpectInteger(arguments[0], "random.randint(a, b) expects integer bounds.", span);
            var stopInclusive = ExpectInteger(arguments[1], "random.randint(a, b) expects integer bounds.", span);
            if (stopInclusive < start)
            {
                throw new LythonRuntimeException("ValueError", "random.randint(a, b) requires b >= a.", span);
            }

            var count = stopInclusive - start + BigInteger.One;
            var offset = new BigInteger(state.NextBelow(ToBound(count, "random.randint", span)));
            return start + offset;
        }

        private static object Choice(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.choice(seq) expects one sequence argument.", span);
            }

            var items = MaterializeSequence(arguments[0], span);
            if (items.Count == 0)
            {
                throw new LythonRuntimeException("IndexError", "Cannot choose from an empty sequence.", span);
            }

            return items[(int)state.NextBelow((ulong)items.Count)];
        }

        private static object Choices(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 4)
            {
                throw new LythonRuntimeException("TypeError", "random.choices(population[, weights][, cum_weights][, k]) expects one to four arguments.", span);
            }

            var population = MaterializeSequence(arguments[0], span);
            if (population.Count == 0)
            {
                throw new LythonRuntimeException("IndexError", "Cannot choose from an empty sequence.", span);
            }

            var weights = arguments.Length >= 2 && arguments[1] is not PyNone ? ReadWeights(arguments[1], population.Count, "random.choices(..., weights=...)", span) : null;
            var cumulative = arguments.Length >= 3 && arguments[2] is not PyNone ? ReadCumulativeWeights(arguments[2], population.Count, "random.choices(..., cum_weights=...)", span) : null;
            if (weights is not null && cumulative is not null)
            {
                throw new LythonRuntimeException("TypeError", "random.choices(...) does not accept both weights and cum_weights.", span);
            }

            var count = arguments.Length == 4 ? ExpectNonNegativeInt(arguments[3], "random.choices(..., k=...) expects k to be a non-negative integer.", span) : 1;
            var result = new PyList([], context.MemoryGovernor, span);
            for (var i = 0; i < count; i++)
            {
                result.Add(population[ChooseWeightedIndex(state, population.Count, weights, cumulative, span)]);
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private static object Shuffle(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.shuffle(x) expects one mutable sequence argument.", span);
            }

            if (arguments[0] is not IMutablePyIndexableValue mutable)
            {
                throw new LythonRuntimeException("TypeError", "random.shuffle(x) expects a mutable sequence.", span);
            }

            for (var i = mutable.Length - 1; i > 0; i--)
            {
                var j = (int)state.NextBelow((ulong)(i + 1));
                var left = mutable.GetIndex(i);
                var right = mutable.GetIndex(j);
                mutable.SetIndex(i, right);
                mutable.SetIndex(j, left);
            }

            return PyNone.Instance;
        }

        private static object Sample(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "random.sample(population, k, *, counts=None) expects two arguments plus optional counts.", span);
            }

            var population = MaterializeSequence(arguments[0], span);
            var count = ExpectNonNegativeInt(arguments[1], "random.sample(population, k) expects k to be a non-negative integer.", span);
            var items = arguments.Length >= 3 && arguments[2] is not PyNone
                ? ExpandPopulationCounts(population, arguments[2], span, context)
                : population;
            if (count > items.Count)
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative.", span);
            }

            ShuffleMaterialized(state, items);
            var result = new object[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = items[i];
            }

            return new PyList(result, context.MemoryGovernor, span);
        }

        private static object GetRandBits(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.getrandbits(k) expects one integer argument.", span);
            }

            var count = ExpectNonNegativeInt(arguments[0], "random.getrandbits(k) expects k to be a non-negative integer.", span);
            return state.GetRandBits(count);
        }

        private static object RandBytes(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.randbytes(n) expects one integer argument.", span);
            }

            var count = ExpectNonNegativeInt(arguments[0], "random.randbytes(n) expects n to be a non-negative integer.", span);
            return CreateBytes(state.GetRandBytes(count), context, span);
        }

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

        private static BigInteger ComputeRangeCount(BigInteger start, BigInteger stop, BigInteger step, string owner, LythonSourceSpan span)
        {
            if (step == BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) arg 3 must not be zero.", span);
            }

            if (step > BigInteger.Zero)
            {
                if (stop <= start)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner}(...) empty range for randrange().", span);
                }

                return ((stop - start - BigInteger.One) / step) + BigInteger.One;
            }

            if (stop >= start)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) empty range for randrange().", span);
            }

            var magnitude = BigInteger.Abs(step);
            return ((start - stop - BigInteger.One) / magnitude) + BigInteger.One;
        }

        private static ulong ToBound(BigInteger count, string owner, LythonSourceSpan span)
        {
            if (count <= 0 || count > ulong.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", $"{owner}(...) range is too large.", span);
            }

            return (ulong)count;
        }

        private static int ExpectNonNegativeInt(object value, string message, LythonSourceSpan span)
        {
            var integer = ExpectInteger(value, message, span);
            if (integer < 0 || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return (int)integer;
        }

        private static BigInteger ExpectInteger(object value, string message, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return integer;
        }

        private static double ExpectReal(object value, string owner, LythonSourceSpan span)
        {
            if (!TryAsReal(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            return real;
        }

        private static double ExpectPositiveReal(object value, string owner, LythonSourceSpan span)
        {
            var real = ExpectReal(value, owner, span);
            if (real <= 0.0 || double.IsNaN(real) || double.IsInfinity(real))
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects a positive finite number.", span);
            }

            return real;
        }

        private static bool TryAsReal(object value, out double real)
        {
            if (PyNumberOps.TryAsNumber(value, out var number))
            {
                real = number.ToDouble();
                return true;
            }

            if (value is PyDecimal decimalValue)
            {
                real = (double)decimalValue.Value;
                return true;
            }

            real = default;
            return false;
        }

        private static double[] ReadWeights(object value, int expectedCount, string owner, LythonSourceSpan span)
        {
            var values = MaterializeSequence(value, span);
            if (values.Count != expectedCount)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects one weight per population item.", span);
            }

            var result = new double[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                var weight = ExpectReal(values[i], owner, span);
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects finite non-negative weights.", span);
                }

                result[i] = weight;
            }

            return result;
        }

        private static double[] ReadCumulativeWeights(object value, int expectedCount, string owner, LythonSourceSpan span)
        {
            var values = ReadWeights(value, expectedCount, owner, span);
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects monotonically increasing cumulative weights.", span);
                }
            }

            return values;
        }

        private static List<object> ExpandPopulationCounts(IReadOnlyList<object> population, object countsValue, LythonSourceSpan span, ExecutionContext context)
        {
            var counts = MaterializeSequence(countsValue, span);
            if (counts.Count != population.Count)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            var total = BigInteger.Zero;
            var parsed = new int[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                var count = ExpectInteger(counts[i], "random.sample(..., counts=...) expects integer counts.", span);
                if (count < BigInteger.Zero || count > int.MaxValue)
                {
                    throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects non-negative counts.", span);
                }

                parsed[i] = (int)count;
                total += count;
            }

            if (total > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "random.sample(..., counts=...) population is too large.", span);
            }

            context.ObserveCollectionCount((int)total, span);
            var expanded = new List<object>((int)total);
            for (var i = 0; i < population.Count; i++)
            {
                for (var j = 0; j < parsed[i]; j++)
                {
                    expanded.Add(population[i]);
                }
            }

            return expanded;
        }

        private static List<object> MaterializeSequence(object value, LythonSourceSpan span)
        {
            var result = new List<object>();
            foreach (var item in ToSequence(value, span))
            {
                result.Add(RuntimeValue(item));
            }

            return result;
        }

        private static int ChooseWeightedIndex(PyRandomState state, int populationLength, double[]? weights, double[]? cumulative, LythonSourceSpan span)
        {
            if (weights is null && cumulative is null)
            {
                return (int)state.NextBelow((ulong)populationLength);
            }

            if (weights is not null)
            {
                var total = weights.Sum();
                if (total <= 0)
                {
                    throw new LythonRuntimeException("ValueError", "random.choices(..., weights=...) total of weights must be greater than zero.", span);
                }

                var threshold = state.NextDouble() * total;
                double running = 0;
                for (var i = 0; i < weights.Length; i++)
                {
                    running += weights[i];
                    if (threshold < running)
                    {
                        return i;
                    }
                }

                return weights.Length - 1;
            }

            var cumulativeWeights = cumulative!;
            var maximum = cumulativeWeights[^1];
            if (maximum <= 0)
            {
                throw new LythonRuntimeException("ValueError", "random.choices(..., cum_weights=...) total of weights must be greater than zero.", span);
            }

            var thresholdCum = state.NextDouble() * maximum;
            for (var i = 0; i < cumulativeWeights.Length; i++)
            {
                if (thresholdCum < cumulativeWeights[i])
                {
                    return i;
                }
            }

            return cumulativeWeights.Length - 1;
        }

        private static void ShuffleMaterialized(PyRandomState state, IList<object> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = (int)state.NextBelow((ulong)(i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private static double SampleGamma(PyRandomState state, double alpha, double beta)
        {
            if (alpha < 1.0)
            {
                return SampleGamma(state, alpha + 1.0, beta) * Math.Pow(NonZeroRandom(state), 1.0 / alpha);
            }

            var d = alpha - 1.0 / 3.0;
            var c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                var x = StandardNormal(state);
                var v = 1.0 + c * x;
                if (v <= 0.0)
                {
                    continue;
                }

                v *= v * v;
                var u = state.NextDouble();
                if (u < 1.0 - 0.0331 * x * x * x * x ||
                    Math.Log(u) < 0.5 * x * x + d * (1.0 - v + Math.Log(v)))
                {
                    return beta * d * v;
                }
            }
        }

        private static double StandardNormal(PyRandomState state)
        {
            var u1 = NonZeroRandom(state);
            var u2 = state.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(TwoPi * u2);
        }

        private static double NonZeroRandom(PyRandomState state)
        {
            double value;
            do
            {
                value = state.NextDouble();
            }
            while (value <= 0.0);

            return value;
        }

        private static double ModTwoPi(double value)
        {
            var result = value % TwoPi;
            return result < 0.0 ? result + TwoPi : result;
        }

        internal sealed class PyRandom : IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
        {
            private readonly PyRandomState _state;

            public PyRandom(PyRandomState state)
            {
                _state = state;
            }

            public bool TryGetMember(string name, out object value)
            {
                value = name switch
                {
                    "seed" => new BoundCallable((arguments, span, context) => Seed(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSeed),
                    "random" => new BoundCallable((arguments, span, context) => Random(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandom),
                    "getstate" => new BoundCallable((arguments, span, context) => GetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetState),
                    "setstate" => new BoundCallable((arguments, span, context) => SetState(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSetState),
                    "randrange" => new BoundCallable((arguments, span, context) => RandRange(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandRange),
                    "randint" => new BoundCallable((arguments, span, context) => RandInt(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandInt),
                    "choice" => new BoundCallable((arguments, span, context) => Choice(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoice),
                    "choices" => new BoundCallable((arguments, span, context) => Choices(_state, arguments, span, context), LythonKnownCallableSignatures.RandomChoices),
                    "shuffle" => new BoundCallable((arguments, span, context) => Shuffle(_state, arguments, span, context), LythonKnownCallableSignatures.RandomShuffle),
                    "sample" => new BoundCallable((arguments, span, context) => Sample(_state, arguments, span, context), LythonKnownCallableSignatures.RandomSample),
                    "getrandbits" => new BoundCallable((arguments, span, context) => GetRandBits(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGetRandBits),
                    "randbytes" => new BoundCallable((arguments, span, context) => RandBytes(_state, arguments, span, context), LythonKnownCallableSignatures.RandomRandBytes),
                    "uniform" => new BoundCallable((arguments, span, context) => Uniform(_state, arguments, span, context), LythonKnownCallableSignatures.RandomUniform),
                    "triangular" => new BoundCallable((arguments, span, context) => Triangular(_state, arguments, span, context), LythonKnownCallableSignatures.RandomTriangular),
                    "betavariate" => new BoundCallable((arguments, span, context) => BetaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomBetaVariate),
                    "expovariate" => new BoundCallable((arguments, span, context) => ExpVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomExpVariate),
                    "gammavariate" => new BoundCallable((arguments, span, context) => GammaVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomGammaVariate),
                    "gauss" => new BoundCallable((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.gauss"), LythonKnownCallableSignatures.RandomGauss),
                    "normalvariate" => new BoundCallable((arguments, span, context) => NormalVariate(_state, arguments, span, context, "random.Random.normalvariate"), LythonKnownCallableSignatures.RandomNormalVariate),
                    "lognormvariate" => new BoundCallable((arguments, span, context) => LogNormVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomLogNormVariate),
                    "paretovariate" => new BoundCallable((arguments, span, context) => ParetoVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomParetoVariate),
                    "vonmisesvariate" => new BoundCallable((arguments, span, context) => VonMisesVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomVonMisesVariate),
                    "weibullvariate" => new BoundCallable((arguments, span, context) => WeibullVariate(_state, arguments, span, context), LythonKnownCallableSignatures.RandomWeibullVariate),
                    _ => null!,
                };

                return value is not null;
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<random.Random object>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }
    }
}
