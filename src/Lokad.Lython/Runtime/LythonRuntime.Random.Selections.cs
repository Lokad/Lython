using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
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
            var (start, stop, step, emptyMessage) = ParseRangeArguments(arguments, "random.randrange", span, context);
            var count = ComputeRangeCount(start, stop, step, emptyMessage, span);
            var offset = new BigInteger(state.NextBelow(ToBound(count, "random.randrange", span)));
            return start + (offset * step);
        }

        private static object RandInt(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "random.randint(a, b) expects two integer arguments.", span);
            }

            // Like CPython, randint(a, b) draws from randrange(a, b + 1).
            var start = CoerceRandomIndex(arguments[0], context, span);
            var stop = CoerceRandomIndex(arguments[1], context, span) + BigInteger.One;
            var count = ComputeRangeCount(start, stop, BigInteger.One, $"empty range in randrange({start}, {stop})", span);
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

            var rawCount = arguments.Length == 4 ? CoerceRandomIndex(arguments[3], context, span) : BigInteger.One;
            if (rawCount > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", "random.choices(..., k=...) expects k to be a non-negative integer.", span);
            }

            // Like CPython (repeat yields empty), negative counts draw nothing.
            var count = rawCount < BigInteger.Zero ? 0 : (int)rawCount;
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
            var rawCount = arguments[1] is int smallCount ? new BigInteger(smallCount) : arguments[1];
            // Like CPython, k flows through dispatched comparisons first and
            // the selection multiplies by k, so each failure keeps its own text.
            // With counts, k validates against the expanded total inside.
            if (!IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.LessEqual, BigInteger.Zero, rawCount, context, span), context, span))
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative", span);
            }

            if (arguments.Length >= 3 && arguments[2] is not PyNone)
            {
                return SampleCountedPositions(population, arguments[2], rawCount, state, span, context);
            }

            if (!IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.LessEqual, rawCount, new BigInteger(population.Count), context, span), context, span))
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative", span);
            }

            var count = CoerceSampleCount(rawCount, span);
            ShuffleMaterialized(state, population);
            var result = new object[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = population[i];
            }

            return new PyList(result, context.MemoryGovernor, span);
        }

        // After the bound checks, k sizes like the multiplied selection, so
        // surviving non-integers report the multiply shape.
        private static int CoerceSampleCount(object rawCount, LythonSourceSpan span)
            => rawCount switch
            {
                bool flag => flag ? 1 : 0,
                int small => small,
                BigInteger big => (int)big,
                _ => throw RuntimeErrors.MultiplySequenceError(rawCount, span),
            };

        private static object GetRandBits(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.getrandbits(k) expects one integer argument.", span);
            }

            var rawCount = CoerceRandomIndex(arguments[0], context, span);
            if (rawCount < BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "number of bits must be non-negative", span);
            }

            if (rawCount > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", "random.getrandbits(k) expects k to be a non-negative integer.", span);
            }

            return state.GetRandBits((int)rawCount);
        }

        private static object RandBytes(PyRandomState state, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "random.randbytes(n) expects one integer argument.", span);
            }

            // Like CPython (getrandbits(n * 8).to_bytes(n)): the width
            // multiplies first, so hook and operand failures keep their texts.
            var width = CoerceRandomIndex(EvaluateBinaryOperator(BinaryOperatorSyntax.Multiply, arguments[0], new BigInteger(8), context, span), context, span);
            if (width < BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "number of bits must be non-negative", span);
            }

            if (width > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", "random.randbytes(n) expects n to be a non-negative integer.", span);
            }

            return CreateBytes(state.GetRandBytes((int)(width / 8)), context, span);
        }
    }
}
