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
                "repeat" => new ItertoolsCallable("itertools.repeat", Repeat, RepeatAsync),
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
                "tee" => new ItertoolsCallable("itertools.tee", Tee, TeeAsync),
                "batched" => new ItertoolsCallable("itertools.batched", Batched, BatchedAsync),
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
            context.CheckExecution(span);
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

            var chainResult = new PyChainIterator(iterables, span, context);
            return chainResult;
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

            var chainFromIterableResult = new PyChainIterator(positional[0], span, context);
            return chainFromIterableResult;
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
            context.CheckExecution(span);
            return _implementation(arguments, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
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
        var countResult = new PyCountIterator(start, step, context, span);
        return countResult;
    }

    private static object Repeat(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => RepeatCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> RepeatAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => RepeatCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> RepeatCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsRepeat, span);
        long? times = bound.Assigned[1]
            ? Math.Max(0, await CoerceItertoolsIndexAsync(bound.Values[1], span, context, asynchronous).ConfigureAwait(false))
            : null;
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        return new PyRepeatIterator(bound.Values[0], times);
    }

    private static object Cycle(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.cycle", span);
        if (positional.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "itertools.cycle(iterable) expects one iterable argument.", span);
        }

        var cycleResult = new PyCycleIterator(positional[0], context.MemoryGovernor, context, span);
        return cycleResult;
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

        var isliceResult = new PyIsliceIterator(positional[0], start, stop, step, span, context);
        return isliceResult;
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
            _ when Numbers.PyNumberOps.TryAsInteger(coerced, out var integerValue) => integerValue,
            _ => throw new LythonRuntimeException("TypeError", "islice bound is not an integer.", span),
        };

        if (integer < minimum || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "islice bound is out of range.", span);
        }

        return (long)integer;
    }

    private static async ValueTask<(int Count, long Repeat)> BindProductAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var count = 0;
        long repeat = 1;
        foreach (var argument in arguments)
        {
            context.CheckExecution(span);
            if (argument.IsPositional) count++;
            else if (argument.KeywordName == "repeat")
            {
                repeat = await CoerceItertoolsIndexAsync(argument.Value, span, context, asynchronous).ConfigureAwait(false);
                if (repeat < 0) throw new LythonRuntimeException("ValueError", "repeat argument cannot be negative", span);
            }
            else throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
        }

        // CPython checks ssize_t multiplication before consuming any input.
        if (count != 0 && repeat > long.MaxValue / count)
            throw new LythonRuntimeException("OverflowError", "repeat argument too large", span);
        if (count * repeat > Array.MaxLength)
            throw RuntimeErrors.Memory("itertools.product(...) repeat count is too large.", span);
        return (count, repeat);
    }

    private static object Product(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (count, repeat) = BindProductAsync(arguments, span, context, false).GetAwaiter().GetResult();
        if (repeat == 0 || count == 0) return CreateProduct(Array.Empty<PyList>(), 0, span, context);
        using var table = context.MemoryGovernor.ReserveTemporary(PyCombinatoricOwnership.PoolArrayBytes(count), span);
        var pools = new PyList[count];
        try
        {
            var index = 0;
            foreach (var argument in arguments)
                if (argument.IsPositional) pools[index++] = MaterializeSequence(argument.Value, span, context);
            return CreateProduct(pools, repeat, span, context);
        }
        catch
        {
            foreach (var pool in pools) pool?.DiscardUnpublishedStorage();
            throw;
        }
    }

    private static async ValueTask<object> ProductAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (count, repeat) = await BindProductAsync(arguments, span, context, true).ConfigureAwait(false);
        if (repeat == 0 || count == 0) return CreateProduct(Array.Empty<PyList>(), 0, span, context);
        using var table = context.MemoryGovernor.ReserveTemporary(PyCombinatoricOwnership.PoolArrayBytes(count), span);
        var pools = new PyList[count];
        try
        {
            var index = 0;
            foreach (var argument in arguments)
                if (argument.IsPositional) pools[index++] = await MaterializeItSequenceAsync(argument.Value, span, context).ConfigureAwait(false);
            return CreateProduct(pools, repeat, span, context);
        }
        catch
        {
            foreach (var pool in pools) pool?.DiscardUnpublishedStorage();
            throw;
        }
    }

    private static PyProductIterator CreateProduct(IReadOnlyList<PyList> pools, long repeat, LythonSourceSpan span, ExecutionContext context)
    {
        var length = checked((int)(pools.Count * repeat));
        var bytes = PyCombinatoricOwnership.PoolArrayBytes(length);
        using var table = context.MemoryGovernor.ReserveTemporary(bytes, span);
        var repeated = new IReadOnlyList<object>[length];
        for (var index = 0; index < length; index++)
        {
            context.CheckExecution(span);
            repeated[index] = pools[index % pools.Count];
        }
        var result = new PyProductIterator(repeated, context.MemoryGovernor, span, context);
        table.Commit(bytes);
        return result;
    }

    private static object ZipLongest(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        using var table = context.MemoryGovernor.ReserveTemporary(PyCombinatoricOwnership.PoolArrayBytes(arguments.Length), span);
        var iterables = new object[arguments.Length];
        var iterableCount = 0;
        object fillValue = PyNone.Instance;
        for (var i = 0; i < arguments.Length; i++)
        {
            context.CheckExecution(span);
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

        var zipLongestResult = new PyZipLongestIterator(iterableCount == iterables.Length ? iterables : iterables[..iterableCount], fillValue, span, context.MemoryGovernor, context);
        return zipLongestResult;
    }

    private static object Combinations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => CreateCombinationsAsync(arguments, span, context, false, false).GetAwaiter().GetResult();
    private static ValueTask<object> CombinationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => CreateCombinationsAsync(arguments, span, context, false, true);
    private static object CombinationsWithReplacement(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => CreateCombinationsAsync(arguments, span, context, true, false).GetAwaiter().GetResult();
    private static ValueTask<object> CombinationsWithReplacementAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => CreateCombinationsAsync(arguments, span, context, true, true);

    private static async ValueTask<object> CreateCombinationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool withReplacement, bool asynchronous)
    {
        var signature = withReplacement ? LythonKnownCallableSignatures.ItertoolsCombinationsWithReplacement
            : LythonKnownCallableSignatures.ItertoolsCombinations;
        var bound = BindArguments(arguments, signature, span);
        // Clinic converts r before tuple(iterable), but checks negativity after it.
        var requested = await CoerceItertoolsIndexAsync(bound.Values[1], span, context, asynchronous).ConfigureAwait(false);
        var pool = asynchronous
            ? await MaterializeItSequenceAsync(bound.Values[0], span, context).ConfigureAwait(false)
            : MaterializeSequence(bound.Values[0], span, context);
        try
        {
            var r = CombinatoricLength(requested, span);
            return withReplacement
                ? new PyCombinationsWithReplacementIterator(pool, r, context.MemoryGovernor, span, context)
                : new PyCombinationsIterator(pool, r, context.MemoryGovernor, span, context);
        }
        catch { pool.DiscardUnpublishedStorage(); throw; }
    }

    private static object Permutations(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => PermutationsCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> PermutationsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => PermutationsCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> PermutationsCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsPermutations, span);
        var pool = asynchronous
            ? await MaterializeItSequenceAsync(bound.Values[0], span, context).ConfigureAwait(false)
            : MaterializeSequence(bound.Values[0], span, context);
        try
        {
            var r = pool.Count;
            if (bound.Assigned[1] && bound.Values[1] is not PyNone)
            {
                // CPython permutations deliberately accepts an int, not __index__,
                // and validates it only after pooling the input.
                if (!Numbers.PyNumberOps.TryAsInteger(bound.Values[1], out var integer))
                    throw new LythonRuntimeException("TypeError", "Expected int as r", span);
                r = CombinatoricLength(ItertoolsSsize(integer, span), span);
            }
            return new PyPermutationsIterator(pool, r, context.MemoryGovernor, span, context);
        }
        catch { pool.DiscardUnpublishedStorage(); throw; }
    }

    private static int CombinatoricLength(long value, LythonSourceSpan span)
    {
        if (value < 0) throw new LythonRuntimeException("ValueError", "r must be non-negative", span);
        if (value > Array.MaxLength) throw RuntimeErrors.Memory("iterator index table exceeds managed array capacity", span);
        return (int)value;
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
        var accumulateResult = new PyAccumulateIterator(bound.Values[0], function, bound.Values[2], hasInitial, context, span);
        return accumulateResult;
    }

    private static object Compress(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsCompress, span);
        var compressResult = new PyCompressIterator(bound.Values[0], bound.Values[1], span, context);
        return compressResult;
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
        var filterFalseResult = new PyPredicateIterator(predicate, positional[1], PyPredicateIteratorMode.FilterFalse, context, span);
        return filterFalseResult;
    }

    private static object DropWhile(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (predicate, iterable) = BindPredicateIterator(arguments, "itertools.dropwhile", span);
        var dropWhileResult = new PyPredicateIterator(predicate, iterable, PyPredicateIteratorMode.DropWhile, context, span);
        return dropWhileResult;
    }

    private static object TakeWhile(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var (predicate, iterable) = BindPredicateIterator(arguments, "itertools.takewhile", span);
        var takeWhileResult = new PyPredicateIterator(predicate, iterable, PyPredicateIteratorMode.TakeWhile, context, span);
        return takeWhileResult;
    }

    private static object Starmap(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.starmap", span);
        if (positional.Length != 2 || positional[0] is not ICallable function)
        {
            throw new LythonRuntimeException("TypeError", "itertools.starmap(function, iterable) expects a callable and an iterable.", span);
        }

        var starmapResult = new PyStarmapIterator(function, positional[1], context, span);
        return starmapResult;
    }

    private static object Pairwise(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = PositionalOnly(arguments, "itertools.pairwise", span);
        if (positional.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "itertools.pairwise(iterable) expects one iterable argument.", span);
        }

        var pairwiseResult = new PyPairwiseIterator(positional[0], context.MemoryGovernor, span, context);
        return pairwiseResult;
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

        var groupByResult = new PyGroupByIterator(bound.Values[0], keyFunction, context.MemoryGovernor, context, span);
        return groupByResult;
    }

    private static object Tee(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TeeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> TeeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TeeCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> TeeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length is < 1 or > 2)
            throw new LythonRuntimeException("TypeError", "itertools.tee(iterable, n=2) expects one or two positional arguments.", span);
        if (arguments[0].IsKeyword || arguments.Length == 2 && arguments[1].IsKeyword)
            throw new LythonRuntimeException("TypeError", "itertools.tee(...) does not accept keyword arguments.", span);
        var requested = arguments.Length == 2
            ? await CoerceItertoolsIndexAsync(arguments[1].Value, span, context, asynchronous).ConfigureAwait(false) : 2;
        if (requested < 0) throw new LythonRuntimeException("ValueError", "n must be >= 0", span);
        if (requested > Array.MaxLength) throw RuntimeErrors.Memory("tee output exceeds managed array capacity", span);
        if (requested == 0) return PyTuple.Empty;
        var count = (int)requested;
        var iteratorBytes = checked(128L * count);
        using var output = context.MemoryGovernor.ReserveTemporary(checked(iteratorBytes + PyTuple.EstimateApproximateBytes(count)), span);
        var shared = new PyTeeSharedState(arguments[0].Value, count, context.MemoryGovernor, context, span);
        var iterators = new object[count];
        for (var i = 0; i < count; i++)
        {
            context.CheckExecution(span);
            iterators[i] = new PyTeeIterator(shared, i);
        }
        output.Commit(iteratorBytes);
        return PyTuple.FromReservedOwnedArray(iterators, context.MemoryGovernor, span, output);
    }

    private static object Batched(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => BatchedCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> BatchedAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        => BatchedCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> BatchedCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        var bound = BindArguments(arguments, LythonKnownCallableSignatures.ItertoolsBatched, span);
        var size = await CoerceItertoolsIndexAsync(bound.Values[1], span, context, asynchronous).ConfigureAwait(false);
        var strict = bound.Assigned[2] && (asynchronous
            ? await IsTruthyAsync(bound.Values[2], context, span).ConfigureAwait(false) : IsTruthy(bound.Values[2], context, span));
        if (size < 1) throw new LythonRuntimeException("ValueError", "n must be at least one", span);
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

    // Private governed pools retain capacity directly. No ungoverned staging
    // list or final array copy; the iterator is the only user of this storage.
    private static PyList MaterializeSequence(object value, LythonSourceSpan span, ExecutionContext context)
        => new(ToSequence(value, span, context).Select(RuntimeValue), context.MemoryGovernor, span);

    private static async ValueTask<PyList> MaterializeItSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context)
    {
        var pool = new PyList(Array.Empty<object>(), context.MemoryGovernor, span);
        try
        {
            await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
                pool.Add(RuntimeValue(item));
            return pool;
        }
        catch
        {
            pool.DiscardUnpublishedStorage();
            throw;
        }
    }

    private static object ExpectNumber(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsNumber(value, out var number))
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return number.IsFloat ? number.Floating : number.Integer;
    }

    private static async ValueTask<long> CoerceItertoolsIndexAsync(object value, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        var converted = await CoerceTypeIndexAsync(value, context, span, asynchronous).ConfigureAwait(false);
        if (converted is null || !Numbers.PyNumberOps.TryAsInteger(converted, out var integer))
            throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context)
                + "' object cannot be interpreted as an integer", span);
        return ItertoolsSsize(integer, span);
    }

    private static long ItertoolsSsize(BigInteger value, LythonSourceSpan span)
    {
        if (value < long.MinValue || value > long.MaxValue)
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        return (long)value;
    }
}
