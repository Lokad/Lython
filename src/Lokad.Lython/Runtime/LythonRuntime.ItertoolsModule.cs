using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ItertoolsModule : PyModule
    {
        public static readonly ItertoolsModule Instance = new();

        private ItertoolsModule() : base("itertools")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "chain" => ChainFactory.Instance,
                "count" => new ItertoolsCallable("itertools.count", Count),
                "repeat" => new ItertoolsCallable("itertools.repeat", Repeat),
                "cycle" => new ItertoolsCallable("itertools.cycle", Cycle),
                "islice" => new ItertoolsCallable("itertools.islice", Islice),
                "product" => new ItertoolsCallable("itertools.product", Product, ProductAsync),
                "zip_longest" => new ItertoolsCallable("itertools.zip_longest", ZipLongest),
                "combinations" => new ItertoolsCallable("itertools.combinations", Combinations, CombinationsAsync),
                "combinations_with_replacement" => new ItertoolsCallable("itertools.combinations_with_replacement", CombinationsWithReplacement, CombinationsWithReplacementAsync),
                "permutations" => new ItertoolsCallable("itertools.permutations", Permutations, PermutationsAsync),
                "accumulate" => new ItertoolsCallable("itertools.accumulate", Accumulate),
                "compress" => new ItertoolsCallable("itertools.compress", Compress),
                "filterfalse" => new ItertoolsCallable("itertools.filterfalse", FilterFalse),
                "dropwhile" => new ItertoolsCallable("itertools.dropwhile", DropWhile),
                "takewhile" => new ItertoolsCallable("itertools.takewhile", TakeWhile),
                "starmap" => new ItertoolsCallable("itertools.starmap", Starmap),
                "pairwise" => new ItertoolsCallable("itertools.pairwise", Pairwise),
                "groupby" => new ItertoolsCallable("itertools.groupby", GroupBy),
                "tee" => new ItertoolsCallable("itertools.tee", Tee),
                "batched" => new ItertoolsCallable("itertools.batched", Batched),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal sealed class ChainFactory : ICallable
    {
        public static readonly ChainFactory Instance = new();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positionalCount = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is not null)
                {
                    throw new LythonRuntimeException("TypeError", "itertools.chain(...) does not accept keyword arguments.", span);
                }

                positionalCount++;
            }

            var iterables = new object[positionalCount];
            for (var i = 0; i < positionalCount; i++)
            {
                iterables[i] = arguments[i].Value;
            }

            return new PyChainIterator(iterables, span);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "from_iterable" => new ItertoolsCallable("itertools.chain.from_iterable", FromIterable),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object FromIterable(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var positional = PositionalOnly(arguments, "itertools.chain.from_iterable", span);
            if (positional.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "itertools.chain.from_iterable(iterable) expects one iterable argument.", span);
            }

            return new PyChainIterator(positional[0], span);
        }
    }

    private sealed class ItertoolsCallable : ICallable
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        public ItertoolsCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation) : this(name, implementation, null) { }

        public ItertoolsCallable(
            string name,
            Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
        {
            _name = name;
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _asyncImplementation is null
                ? _implementation(arguments, span, context)
                : await _asyncImplementation(arguments, span, context).ConfigureAwait(false);
        }
    }

    private static object Count(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.count", ["start", "step"], requiredCount: 0, maxPositionalCount: 2, span);
        var start = bound.Assigned[0] ? ExpectNumber(bound.Values[0], "itertools.count(..., start=...) expects a number.", span) : BigInteger.Zero;
        var step = bound.Assigned[1] ? ExpectNumber(bound.Values[1], "itertools.count(..., step=...) expects a number.", span) : BigInteger.One;
        return new PyCountIterator(start, step, context, span);
    }

    private static object Repeat(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var bound = BindArguments(arguments, "itertools.repeat", ["object", "times"], requiredCount: 1, maxPositionalCount: 2, span);
        long? times = null;
        if (bound.Assigned[1])
        {
            var count = ExpectLong(bound.Values[1], "itertools.repeat(..., times=...) expects an integer.", span);
            times = count < 0 ? 0 : count;
        }

        return new PyRepeatIterator(bound.Values[0], times);
    }

    private static object Cycle(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.cycle", span);
        if (positional.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "itertools.cycle(iterable) expects one iterable argument.", span);
        }

        return new PyCycleIterator(positional[0], context.MemoryGovernor, context, span);
    }

    private static object Islice(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var positional = PositionalOnly(arguments, "itertools.islice", span);
        if (positional.Length is < 2 or > 4)
        {
            throw new LythonRuntimeException("TypeError", "itertools.islice(iterable, stop) or itertools.islice(iterable, start, stop[, step]) is required.", span);
        }

        long start;
        long? stop;
        long step;
        if (positional.Length == 2)
        {
            start = 0;
            stop = ReferenceEquals(positional[1], PyNone.Instance)
                ? null
                : ExpectNonNegativeLong(positional[1], "itertools.islice() stop must be a non-negative integer or None.", span);
            step = 1;
        }
        else
        {
            start = ReferenceEquals(positional[1], PyNone.Instance)
                ? 0
                : ExpectNonNegativeLong(positional[1], "itertools.islice() start must be a non-negative integer or None.", span);
            stop = ReferenceEquals(positional[2], PyNone.Instance)
                ? null
                : ExpectNonNegativeLong(positional[2], "itertools.islice() stop must be a non-negative integer or None.", span);
            step = positional.Length == 4 && !ReferenceEquals(positional[3], PyNone.Instance)
                ? ExpectPositiveLong(positional[3], "itertools.islice() step must be a positive integer or None.", span)
                : 1;
        }

        return new PyIsliceIterator(positional[0], start, stop, step, span);
    }

    private static object Product(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var positionalCount = 0;
        var repeat = 1;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is null)
            {
                positionalCount++;
                continue;
            }

            if (!string.Equals(argument.Name, "repeat", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
            }

            repeat = checked((int)ExpectNonNegativeLong(argument.Value, "itertools.product(..., repeat=...) expects repeat to be a non-negative integer.", span));
        }

        var pools = new object[positionalCount][];
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is not null)
            {
                continue;
            }

            pools[positionalIndex++] = MaterializeSequence(argument.Value, span);
        }

        var repeated = new IReadOnlyList<object>[pools.Length * repeat];
        var repeatedIndex = 0;
        for (var i = 0; i < repeat; i++)
        {
            for (var j = 0; j < pools.Length; j++)
            {
                repeated[repeatedIndex++] = pools[j];
            }
        }

        return new PyProductIterator(repeated, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> ProductAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var positionalCount = 0;
        var repeat = 1;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is null)
            {
                positionalCount++;
                continue;
            }

            if (!string.Equals(argument.Name, "repeat", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
            }

            repeat = checked((int)ExpectNonNegativeLong(argument.Value, "itertools.product(..., repeat=...) expects repeat to be a non-negative integer.", span));
        }

        var pools = new object[positionalCount][];
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is not null)
            {
                continue;
            }

            pools[positionalIndex++] = await MaterializeItSequenceAsync(argument.Value, span).ConfigureAwait(false);
        }

        var repeated = new IReadOnlyList<object>[pools.Length * repeat];
        var repeatedIndex = 0;
        for (var i = 0; i < repeat; i++)
        {
            for (var j = 0; j < pools.Length; j++)
            {
                repeated[repeatedIndex++] = pools[j];
            }
        }

        return new PyProductIterator(repeated, context.MemoryGovernor, span);
    }

    private static object ZipLongest(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var iterables = new object[arguments.Length];
        var iterableCount = 0;
        object fillValue = PyNone.Instance;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is null)
            {
                iterables[iterableCount++] = argument.Value;
                continue;
            }

            if (!string.Equals(argument.Name, "fillvalue", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.zip_longest(...) only supports the keyword argument fillvalue=.", span);
            }

            fillValue = argument.Value;
        }

        return new PyZipLongestIterator(iterableCount == iterables.Length ? iterables : iterables[..iterableCount], fillValue, span, context.MemoryGovernor, span);
    }

    private static object Combinations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.combinations", ["iterable", "r"], requiredCount: 2, maxPositionalCount: 2, span);
        var pool = MaterializeSequence(bound.Values[0], span);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> CombinationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.combinations", ["iterable", "r"], requiredCount: 2, maxPositionalCount: 2, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span).ConfigureAwait(false);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object CombinationsWithReplacement(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.combinations_with_replacement", ["iterable", "r"], requiredCount: 2, maxPositionalCount: 2, span);
        var pool = MaterializeSequence(bound.Values[0], span);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsWithReplacementIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> CombinationsWithReplacementAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.combinations_with_replacement", ["iterable", "r"], requiredCount: 2, maxPositionalCount: 2, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span).ConfigureAwait(false);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsWithReplacementIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object Permutations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.permutations", ["iterable", "r"], requiredCount: 1, maxPositionalCount: 2, span);
        var pool = MaterializeSequence(bound.Values[0], span);
        var r = !bound.Assigned[1] || bound.Values[1] is PyNone
            ? pool.Length
            : ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyPermutationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> PermutationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.permutations", ["iterable", "r"], requiredCount: 1, maxPositionalCount: 2, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span).ConfigureAwait(false);
        var r = !bound.Assigned[1] || bound.Values[1] is PyNone
            ? pool.Length
            : ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyPermutationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object Accumulate(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.accumulate", ["iterable", "func", "initial"], requiredCount: 1, maxPositionalCount: 2, span);
        LythonRuntime.ICallable? function = null;
        if (bound.Assigned[1] && bound.Values[1] is not PyNone)
        {
            function = bound.Values[1] as ICallable ??
                throw new LythonRuntimeException("TypeError", "itertools.accumulate(..., func=...) expects a callable or None.", span);
        }

        var hasInitial = bound.Assigned[2] && bound.Values[2] is not PyNone;
        return new PyAccumulateIterator(bound.Values[0], function, bound.Values[2], hasInitial, context, span);
    }

    private static object Compress(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var bound = BindArguments(arguments, "itertools.compress", ["data", "selectors"], requiredCount: 2, maxPositionalCount: 2, span);
        return new PyCompressIterator(bound.Values[0], bound.Values[1], span);
    }

    private static object FilterFalse(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.filterfalse", span);
        if (positional.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "itertools.filterfalse(function, iterable) expects two positional arguments.", span);
        }

        var predicate = positional[0] is PyNone
            ? null
            : positional[0] as ICallable ?? throw new LythonRuntimeException("TypeError", "itertools.filterfalse(function, iterable) expects a callable or None.", span);
        return new PyPredicateIterator(predicate, positional[1], PyPredicateIteratorMode.FilterFalse, context, span);
    }

    private static object DropWhile(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (predicate, iterable) = BindPredicateIterator(arguments, "itertools.dropwhile", span);
        return new PyPredicateIterator(predicate, iterable, PyPredicateIteratorMode.DropWhile, context, span);
    }

    private static object TakeWhile(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (predicate, iterable) = BindPredicateIterator(arguments, "itertools.takewhile", span);
        return new PyPredicateIterator(predicate, iterable, PyPredicateIteratorMode.TakeWhile, context, span);
    }

    private static object Starmap(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.starmap", span);
        if (positional.Length != 2 || positional[0] is not ICallable function)
        {
            throw new LythonRuntimeException("TypeError", "itertools.starmap(function, iterable) expects a callable and an iterable.", span);
        }

        return new PyStarmapIterator(function, positional[1], context, span);
    }

    private static object Pairwise(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.pairwise", span);
        if (positional.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "itertools.pairwise(iterable) expects one iterable argument.", span);
        }

        return new PyPairwiseIterator(positional[0], context.MemoryGovernor, span);
    }

    private static object GroupBy(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.groupby", ["iterable", "key"], requiredCount: 1, maxPositionalCount: 2, span);
        LythonRuntime.ICallable? keyFunction = null;
        if (bound.Assigned[1] && bound.Values[1] is not PyNone)
        {
            keyFunction = bound.Values[1] as ICallable ??
                throw new LythonRuntimeException("TypeError", "itertools.groupby(..., key=...) expects a callable or None.", span);
        }

        return new PyGroupByIterator(bound.Values[0], keyFunction, context.MemoryGovernor, context, span);
    }

    private static object Tee(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.tee", span);
        if (positional.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "itertools.tee(iterable, n=2) expects one or two positional arguments.", span);
        }

        var count = positional.Length == 2
            ? ExpectItNonNegativeInt(positional[1], "n must be >= 0", span)
            : 2;
        var shared = new PyTeeSharedState(positional[0], count, context.MemoryGovernor, context, span);
        var iterators = new object[count];
        for (var i = 0; i < count; i++)
        {
            iterators[i] = new PyTeeIterator(shared, i);
        }

        return PyTuple.FromOwnedArray(iterators, context.MemoryGovernor, span);
    }

    private static object Batched(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, "itertools.batched", ["iterable", "n", "strict"], requiredCount: 2, maxPositionalCount: 2, span);
        var size = ExpectItPositiveInt(bound.Values[1], "n must be at least one", span);
        var strict = bound.Assigned[2] && IsTruthy(bound.Values[2]);
        return new PyBatchedIterator(bound.Values[0], size, strict, context.MemoryGovernor, span);
    }

    private static object[] PositionalOnly(CallArgumentValue[] arguments, string owner, LythonSourceSpan span)
    {
        var positional = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) does not accept keyword arguments in this form.", span);
            }

            positional[i] = arguments[i].Value;
        }

        return positional;
    }

    private static (ICallable Predicate, object Iterable) BindPredicateIterator(CallArgumentValue[] arguments, string owner, LythonSourceSpan span)
    {
        var positional = PositionalOnly(arguments, owner, span);
        if (positional.Length != 2 || positional[0] is not ICallable predicate)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(predicate, iterable) expects a callable and an iterable.", span);
        }

        return (predicate, positional[1]);
    }

    private static BoundCallArguments BindArguments(
        CallArgumentValue[] arguments,
        string owner,
        string[] parameterNames,
        int requiredCount,
        int maxPositionalCount,
        LythonSourceSpan span)
    {
        var values = new object[parameterNames.Length];
        var assigned = new bool[parameterNames.Length];
        Array.Fill(values, PyNone.Instance);
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positionalIndex >= maxPositionalCount || positionalIndex >= parameterNames.Length)
                {
                    throw new LythonRuntimeException("TypeError", $"{owner}(...) received too many positional arguments.", span);
                }

                values[positionalIndex] = argument.Value;
                assigned[positionalIndex] = true;
                positionalIndex++;
                continue;
            }

            var index = Array.IndexOf(parameterNames, argument.Name);
            if (index < 0)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) received an unexpected keyword argument '{argument.Name}'.", span);
            }

            if (assigned[index])
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) got multiple values for argument '{argument.Name}'.", span);
            }

            values[index] = argument.Value;
            assigned[index] = true;
        }

        for (var i = 0; i < requiredCount; i++)
        {
            if (!assigned[i])
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) missing required argument '{parameterNames[i]}'.", span);
            }
        }

        return new BoundCallArguments(values, assigned);
    }

    private static object[] MaterializeSequence(object value, LythonSourceSpan span)
    {
        var sequence = ToSequence(value, span);
        if (sequence is object[] direct)
        {
            var copy = new object[direct.Length];
            Array.Copy(direct, copy, direct.Length);
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = RuntimeValue(copy[i]);
            }

            return copy;
        }

        var list = new List<object>();
        foreach (var item in sequence)
        {
            list.Add(RuntimeValue(item));
        }

        return [.. list];
    }

    private static async ValueTask<object[]> MaterializeItSequenceAsync(object value, LythonSourceSpan span)
    {
        var list = new List<object>();
        await foreach (var item in ToSequenceAsync(value, span).ConfigureAwait(false))
        {
            list.Add(RuntimeValue(item));
        }

        return [.. list];
    }

    private readonly record struct BoundCallArguments(object[] Values, bool[] Assigned);

    private static long ExpectNonNegativeLong(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) || integer < 0 || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (long)integer;
    }

    private static long ExpectPositiveLong(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) || integer <= 0 || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (long)integer;
    }

    private static object ExpectNumber(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsNumber(value, out var number))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return number.IsFloat ? number.Floating : number.Integer;
    }

    private static long ExpectLong(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) || integer > long.MaxValue || integer < long.MinValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (long)integer;
    }

    private static int ExpectItNonNegativeInt(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        if (integer < BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", message, span);
        }

        if (integer > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", message, span);
        }

        return (int)integer;
    }

    private static int ExpectItPositiveInt(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        if (integer <= BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", message, span);
        }

        if (integer > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", message, span);
        }

        return (int)integer;
    }
}
