using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class RandomModule : PyModule
    {
        private readonly PyRandomState _state;

        public RandomModule(PyRandomState state) : base("random")
        {
            _state = state;
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "seed" => new BuiltinCallable("random.seed", Seed, ["a"], requiredCount: 0),
                "random" => new BuiltinCallable("random.random", Random),
                "randrange" => new BuiltinCallable("random.randrange", RandRange),
                "randint" => new BuiltinCallable("random.randint", RandInt, ["a", "b"]),
                "choice" => new BuiltinCallable("random.choice", Choice, ["seq"]),
                "choices" => new BuiltinCallable("random.choices", Choices, ["population", "weights", "cum_weights", "k"], requiredCount: 1),
                "shuffle" => new BuiltinCallable("random.shuffle", Shuffle, ["x"]),
                "sample" => new BuiltinCallable("random.sample", Sample, ["population", "k"]),
                "getrandbits" => new BuiltinCallable("random.getrandbits", GetRandBits, ["k"]),
                _ => null!,
            };

            return value is not null;
        }

        private object Seed(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "random.seed([a]) expects zero or one argument.", span);
            }

            _state.Seed(ParseSeed(arguments.Length == 0 ? PyNone.Instance : arguments[0], span));
            return PyNone.Instance;
        }

        private object Random(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "random.random() expects no arguments.", span);
            }

            return _state.NextDouble();
        }

        private object RandRange(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var (start, stop, step) = ParseRangeArguments(arguments, "random.randrange", span);
            var count = ComputeRangeCount(start, stop, step, "random.randrange", span);
            var offset = new BigInteger(_state.NextBelow(ToBound(count, "random.randrange", span)));
            return start + (offset * step);
        }

        private object RandInt(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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
            var offset = new BigInteger(_state.NextBelow(ToBound(count, "random.randint", span)));
            return start + offset;
        }

        private object Choice(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

            return items[(int)_state.NextBelow((ulong)items.Count)];
        }

        private object Choices(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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
            var cumulative = arguments.Length >= 3 && arguments[2] is not PyNone ? ReadWeights(arguments[2], population.Count, "random.choices(..., cum_weights=...)", span) : null;
            if (weights is not null && cumulative is not null)
            {
                throw new LythonRuntimeException("TypeError", "random.choices(...) does not accept both weights and cum_weights.", span);
            }

            var count = arguments.Length == 4 ? ExpectNonNegativeInt(arguments[3], "random.choices(..., k=...) expects k to be a non-negative integer.", span) : 1;
            var result = new PyList([], context.MemoryGovernor, span);
            for (var i = 0; i < count; i++)
            {
                result.Add(population[ChooseWeightedIndex(population.Count, weights, cumulative, span)]);
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private object Shuffle(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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
                var j = (int)_state.NextBelow((ulong)(i + 1));
                var left = mutable.GetIndex(i);
                var right = mutable.GetIndex(j);
                mutable.SetIndex(i, right);
                mutable.SetIndex(j, left);
            }

            return PyNone.Instance;
        }

        private object Sample(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.sample(population, k) expects two arguments.", span);
            }

            var items = MaterializeSequence(arguments[0], span);
            var count = ExpectNonNegativeInt(arguments[1], "random.sample(population, k) expects k to be a non-negative integer.", span);
            if (count > items.Count)
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative.", span);
            }

            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = (int)_state.NextBelow((ulong)(i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }

            var result = new object[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = items[i];
            }

            return new PyList(result, context.MemoryGovernor, span);
        }

        private object GetRandBits(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.getrandbits(k) expects one integer argument.", span);
            }

            var count = ExpectNonNegativeInt(arguments[0], "random.getrandbits(k) expects k to be a non-negative integer.", span);
            return _state.GetRandBits(count);
        }

        private static ulong ParseSeed(object value, LythonSourceSpan span)
        {
            return value switch
            {
                null or PyNone => 0x5A17_1C0D_DA7A_2026UL,
                bool boolean => boolean ? 1UL : 0UL,
                BigInteger integer => FoldBytes(integer.ToByteArray(isUnsigned: true, isBigEndian: false)),
                double floating => BitConverter.DoubleToUInt64Bits(floating),
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
            return arguments.Length switch
            {
                1 => (BigInteger.Zero, ExpectInteger(arguments[0], $"{owner}(...) expects integer arguments.", span), BigInteger.One),
                2 => (ExpectInteger(arguments[0], $"{owner}(...) expects integer arguments.", span), ExpectInteger(arguments[1], $"{owner}(...) expects integer arguments.", span), BigInteger.One),
                3 => (ExpectInteger(arguments[0], $"{owner}(...) expects integer arguments.", span), ExpectInteger(arguments[1], $"{owner}(...) expects integer arguments.", span), ExpectInteger(arguments[2], $"{owner}(...) expects integer arguments.", span)),
                _ => throw new LythonRuntimeException("TypeError", $"{owner}(start, stop[, step]) expects one to three integer arguments.", span)
            };
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
            if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return integer;
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
                if (!Numbers.PyNumberOps.TryAsNumber(values[i], out var number))
                {
                    throw new LythonRuntimeException("TypeError", $"{owner} expects numeric weights.", span);
                }

                var weight = number.ToDouble();
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects finite non-negative weights.", span);
                }

                result[i] = weight;
            }

            return result;
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

        private int ChooseWeightedIndex(int populationLength, double[]? weights, double[]? cumulative, LythonSourceSpan span)
        {
            if (weights is null && cumulative is null)
            {
                return (int)_state.NextBelow((ulong)populationLength);
            }

            if (weights is not null)
            {
                var total = weights.Sum();
                if (total <= 0)
                {
                    throw new LythonRuntimeException("ValueError", "random.choices(..., weights=...) total of weights must be greater than zero.", span);
                }

                var threshold = _state.NextDouble() * total;
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

            var thresholdCum = _state.NextDouble() * maximum;
            for (var i = 0; i < cumulativeWeights.Length; i++)
            {
                if (thresholdCum < cumulativeWeights[i])
                {
                    return i;
                }
            }

            return cumulativeWeights.Length - 1;
        }
    }
}
