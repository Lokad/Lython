using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        private static object CreateRandom(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(arguments, span, LythonKnownCallableSignatures.RandomClass, PythonCallableKind.Builtin);
            var state = new PyRandomState();
            if (bound.Length >= 1 && bound[0] is not PyNone)
            {
                state.Seed(ParseSeed(bound[0], span));
            }

            // Own the generator shell beside the tiny counter state.
            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
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
                    SharedStateTag,
                    new BigInteger(state.Snapshot())
                ],
                context.MemoryGovernor,
                span);
        }

        private static object SetState(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.setstate(state) expects one state object.", span);
            }

            state.Restore(ParseState(arguments[0], span, context));
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
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.choice(seq) expects one sequence argument.", span);
            }

            var items = MaterializePopulation(arguments[0], "random.choice", span, context);
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

            var population = MaterializePopulation(arguments[0], "random.choices", span, context);
            if (population.Count == 0)
            {
                throw new LythonRuntimeException("IndexError", "Cannot choose from an empty sequence.", span);
            }

            var weights = arguments.Length >= 2 && arguments[1] is not PyNone ? ReadWeights(arguments[1], population.Count, "random.choices(..., weights=...)", span, context) : null;
            var cumulative = arguments.Length >= 3 && arguments[2] is not PyNone ? ReadCumulativeWeights(arguments[2], population.Count, "random.choices(..., cum_weights=...)", span, context) : null;
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

            var population = MaterializePopulation(arguments[0], "random.sample", span, context);
            var count = ExpectNonNegativeInt(arguments[1], "random.sample(population, k) expects k to be a non-negative integer.", span);
            if (arguments.Length >= 3 && arguments[2] is not PyNone)
            {
                return SampleCountedPositions(population, arguments[2], count, state, span, context);
            }

            if (count > population.Count)
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative.", span);
            }

            ShuffleMaterialized(state, population);
            var result = new object[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = population[i];
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
    }
}
