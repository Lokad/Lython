using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly record struct PredicateIteratorBinding(ICallable Predicate, object Iterable);

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

    internal sealed class ChainFactory : ICallable, IPyRenderableValue
    {
        public static readonly ChainFactory Instance = new();

        // itertools.chain denotes a type like CPython.
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'itertools.chain'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positionalCount = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsKeyword)
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

            return new PyChainIterator(iterables, span, context);
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
            var positional = PositionalOnly(arguments, "itertools.chain.from_iterable", span);
            if (positional.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "itertools.chain.from_iterable(iterable) expects one iterable argument.", span);
            }

            return new PyChainIterator(positional[0], span, context);
        }
    }

    private sealed class ItertoolsCallable : ICallable, IPyRenderableValue
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

        // itertools factories denote types like CPython, except tee (a plain
        // builtin function) and chain.from_iterable (a builtin method of type).
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(BuiltinCallable.ShortCallableName(_name) switch
            {
                "tee" => "<built-in function tee>",
                "from_iterable" => "<built-in method from_iterable of type object>",
                _ => $"<class '{_name}'>",
            });
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static object Count(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCount, span);
        var start = bound.Assigned[0] ? ExpectNumber(bound.Values[0], "itertools.count(..., start=...) expects a number.", span) : BigInteger.Zero;
        var step = bound.Assigned[1] ? ExpectNumber(bound.Values[1], "itertools.count(..., step=...) expects a number.", span) : BigInteger.One;
        return new PyCountIterator(start, step, context, span);
    }

    private static object Repeat(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsRepeat, span);
        long? times = null;
        if (bound.Assigned[1])
        {
            var count = ExpectRepeatCount(bound.Values[1], context, span);
            times = count < 0 ? 0 : count;
        }

        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        return new PyRepeatIterator(bound.Values[0], times);
    }

    // Repeat counts coerce through __index__ like CPython; bad __index__
    // results propagate, out-of-range magnitudes report the ssize_t
    // overflow, and plain non-integers name the type.
    private static long ExpectRepeatCount(object value, ExecutionContext context, LythonSourceSpan span)
    {
        var coerced = CoerceIndexProtocol(value, context, span);
        BigInteger integer;
        if (coerced is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (coerced is int small)
        {
            integer = new BigInteger(small);
        }
        else if (coerced is not BigInteger big)
        {
            throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }
        else
        {
            integer = big;
        }

        if (integer > long.MaxValue || integer < long.MinValue)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        }

        return (long)integer;
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
            stop = CoerceIsliceStop(positional[1], context, span);
            step = 1;
        }
        else
        {
            start = CoerceIsliceStart(positional[1], context, span) ?? 0;
            stop = CoerceIsliceStop(positional[2], context, span);
            step = CoerceIsliceStep(positional.Length == 4 ? positional[3] : null, context, span) ?? 1;
        }

        return new PyIsliceIterator(positional[0], start, stop, step, span, context);
    }

    // islice() maps every bound failure (bad __index__, inner hook errors,
    // non-integers, negatives, huge magnitudes) to its per-position
    // ValueError like CPython.
    private static long? CoerceIsliceStop(object? value, ExecutionContext context, LythonSourceSpan span)
        => CoerceIsliceBound(value, "Stop argument for islice() must be None or an integer: 0 <= x <= sys.maxsize.", BigInteger.Zero, context, span);

    private static long? CoerceIsliceStart(object? value, ExecutionContext context, LythonSourceSpan span)
        => CoerceIsliceBound(value, "Indices for islice() must be None or an integer: 0 <= x <= sys.maxsize.", BigInteger.Zero, context, span);

    private static long? CoerceIsliceStep(object? value, ExecutionContext context, LythonSourceSpan span)
        => CoerceIsliceBound(value, "Step for islice() must be a positive integer or None.", BigInteger.One, context, span);

    private static long? CoerceIsliceBound(object? value, string message, BigInteger minimum, ExecutionContext context, LythonSourceSpan span)
    {
        if (value is null || ReferenceEquals(value, PyNone.Instance))
        {
            return null;
        }

        try
        {
            return ClampIsliceBound(CoerceIndexProtocol(value, context, span), minimum, span);
        }
        catch (LythonRuntimeException)
        {
            throw new LythonRuntimeException("ValueError", message, span);
        }
    }

    private static long ClampIsliceBound(object coerced, BigInteger minimum, LythonSourceSpan span)
    {
        BigInteger integer = coerced switch
        {
            BigInteger big => big,
            int small => new BigInteger(small),
            bool flag => flag ? BigInteger.One : BigInteger.Zero,
            _ => throw new LythonRuntimeException("TypeError", "islice bound is not an integer.", span),
        };

        if (integer < minimum || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "islice bound is out of range.", span);
        }

        return (long)integer;
    }

    // product(repeat=...) coerces through __index__ like CPython; a
    // non-callable hook raises not-callable, negatives report the repeat
    // error and out-of-range magnitudes the ssize_t overflow.
    private static long ExpectProductRepeat(object value, ExecutionContext context, LythonSourceSpan span)
    {
        BigInteger integer;
        if (value is PyInstance instance && instance.TryGetAttribute("__index__", context, span, out var member))
        {
            if (member is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(member, context) + "' object is not callable", span);
            }

            // The shared choke either returns an integer or raises the shaped
            // __index__ error; the callable check above rules out its
            // missing-hook passthrough.
            integer = (BigInteger)CoerceIndexProtocol(instance, context, span);
        }
        else if (value is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (value is int small)
        {
            integer = new BigInteger(small);
        }
        else if (!Numbers.PyNumberOps.TryAsInteger(value, out integer))
        {
            throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        if (integer < 0)
        {
            throw new LythonRuntimeException("ValueError", "repeat argument cannot be negative", span);
        }

        if (integer > long.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        }

        return (long)integer;
    }

    private static object Product(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positionalCount = 0;
        long repeat = 1L;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsPositional)
            {
                positionalCount++;
                continue;
            }

            if (!string.Equals(argument.KeywordName, "repeat", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
            }
            repeat = ExpectProductRepeat(argument.Value, context, span);
        }

        var pools = new object[positionalCount][];
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsKeyword)
            {
                continue;
            }

            pools[positionalIndex++] = MaterializeSequence(argument.Value, span, context);
        }

        var repeatedLength = (long)pools.Length * repeat;
        if (repeatedLength > int.MaxValue)
        {
            throw RuntimeErrors.Memory("itertools.product(...) repeat count is too large.", span);
        }

        // The repeated table shares pool references but is itself retained.
        context.MemoryGovernor.Reserve(64L + (16L * repeatedLength), span);
        context.MemoryGovernor.Commit(64L + (16L * repeatedLength));
        var repeated = new IReadOnlyList<object>[(int)repeatedLength];
        var repeatedIndex = 0;
        for (long i = 0; i < repeat && repeatedIndex < repeated.Length; i++)
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
        var positionalCount = 0;
        long repeat = 1L;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsPositional)
            {
                positionalCount++;
                continue;
            }

            if (!string.Equals(argument.KeywordName, "repeat", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
            }
            repeat = ExpectProductRepeat(argument.Value, context, span);
        }

        var pools = new object[positionalCount][];
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsKeyword)
            {
                continue;
            }

            pools[positionalIndex++] = await MaterializeItSequenceAsync(argument.Value, span, context).ConfigureAwait(false);
        }

        var repeatedLength = (long)pools.Length * repeat;
        if (repeatedLength > int.MaxValue)
        {
            throw RuntimeErrors.Memory("itertools.product(...) repeat count is too large.", span);
        }

        // The repeated table shares pool references but is itself retained.
        context.MemoryGovernor.Reserve(64L + (16L * repeatedLength), span);
        context.MemoryGovernor.Commit(64L + (16L * repeatedLength));
        var repeated = new IReadOnlyList<object>[(int)repeatedLength];
        var repeatedIndex = 0;
        for (long i = 0; i < repeat && repeatedIndex < repeated.Length; i++)
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
        var iterables = new object[arguments.Length];
        var iterableCount = 0;
        object fillValue = PyNone.Instance;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.IsPositional)
            {
                iterables[iterableCount++] = argument.Value;
                continue;
            }

            if (!string.Equals(argument.KeywordName, "fillvalue", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.zip_longest(...) only supports the keyword argument fillvalue=.", span);
            }

            fillValue = argument.Value;
        }

        return new PyZipLongestIterator(iterableCount == iterables.Length ? iterables : iterables[..iterableCount], fillValue, span, context.MemoryGovernor, span, context);
    }

    private static object Combinations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCombinations, span);
        var pool = MaterializeSequence(bound.Values[0], span, context);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> CombinationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCombinations, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span, context).ConfigureAwait(false);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object CombinationsWithReplacement(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCombinationsWithReplacement, span);
        var pool = MaterializeSequence(bound.Values[0], span, context);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsWithReplacementIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> CombinationsWithReplacementAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCombinationsWithReplacement, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span, context).ConfigureAwait(false);
        var r = ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyCombinationsWithReplacementIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object Permutations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsPermutations, span);
        var pool = MaterializeSequence(bound.Values[0], span, context);
        var r = !bound.Assigned[1] || bound.Values[1] is PyNone
            ? pool.Length
            : ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyPermutationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> PermutationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsPermutations, span);
        var pool = await MaterializeItSequenceAsync(bound.Values[0], span, context).ConfigureAwait(false);
        var r = !bound.Assigned[1] || bound.Values[1] is PyNone
            ? pool.Length
            : ExpectItNonNegativeInt(bound.Values[1], "r must be non-negative", span);
        return new PyPermutationsIterator(pool, r, context.MemoryGovernor, span);
    }

    private static object Accumulate(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsAccumulate, span);
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
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCompress, span);
        return new PyCompressIterator(bound.Values[0], bound.Values[1], span, context);
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

        return new PyPairwiseIterator(positional[0], context.MemoryGovernor, span, context);
    }

    private static object GroupBy(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsGroupBy, span);
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
        // Each tee output is a separately retained iterator object.
        var iteratorObjectsCharge = checked(128L * count);
        context.MemoryGovernor.Reserve(iteratorObjectsCharge, span);
        context.MemoryGovernor.Commit(iteratorObjectsCharge);
        var iterators = new object[count];
        for (var i = 0; i < count; i++)
        {
            iterators[i] = new PyTeeIterator(shared, i);
        }

        return PyTuple.FromOwnedArray(iterators, context.MemoryGovernor, span);
    }

    private static object Batched(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsBatched, span);
        var size = ExpectItPositiveInt(bound.Values[1], "n must be at least one", span);
        var strict = bound.Assigned[2] && IsTruthy(bound.Values[2]);
        return new PyBatchedIterator(bound.Values[0], size, strict, context.MemoryGovernor, span, context);
    }

    private static object[] PositionalOnly(CallArgumentValue[] arguments, string owner, LythonSourceSpan span)
    {
        var positional = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) does not accept keyword arguments in this form.", span);
            }

            positional[i] = arguments[i].Value;
        }

        return positional;
    }

    private static PredicateIteratorBinding BindPredicateIterator(CallArgumentValue[] arguments, string owner, LythonSourceSpan span)
    {
        var positional = PositionalOnly(arguments, owner, span);
        if (positional.Length != 2 || positional[0] is not ICallable predicate)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(predicate, iterable) expects a callable and an iterable.", span);
        }

        return new PredicateIteratorBinding(predicate, positional[1]);
    }

    private static BoundCallArguments BindArguments(
        CallArgumentValue[] arguments,
        LythonCallableSignature signature,
        LythonSourceSpan span)
        => CallBinder.BindNamedArgumentsWithPresence(
            arguments,
            span,
            signature,
            PythonCallableKind.Builtin);

    private static object[] MaterializeSequence(object value, LythonSourceSpan span, ExecutionContext context)
    {
        var sequence = ToSequence(value, span, context);
        object[] pool;
        if (sequence is object[] direct)
        {
            pool = new object[direct.Length];
            Array.Copy(direct, pool, direct.Length);
            for (var i = 0; i < pool.Length; i++)
            {
                pool[i] = RuntimeValue(pool[i]);
            }
        }
        else
        {
            var list = new List<object>();
            foreach (var item in sequence)
            {
                list.Add(RuntimeValue(item));
            }

            pool = [.. list];
        }

        // The iterator owns the pool for its lifetime; charge the array once here.
        // Shared pools are referenced, never recharged, downstream.
        context.MemoryGovernor.Reserve(64L + (16L * pool.Length), span);
        context.MemoryGovernor.Commit(64L + (16L * pool.Length));
        return pool;
    }

    private static async ValueTask<object[]> MaterializeItSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context)
    {
        var list = new List<object>();
        await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
        {
            list.Add(RuntimeValue(item));
        }

        object[] pool = [.. list];
        context.MemoryGovernor.Reserve(64L + (16L * pool.Length), span);
        context.MemoryGovernor.Commit(64L + (16L * pool.Length));
        return pool;
    }

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
