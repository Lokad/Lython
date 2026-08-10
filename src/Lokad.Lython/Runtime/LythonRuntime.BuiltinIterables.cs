using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly record struct MinMaxArguments(
        IReadOnlyList<object> Positional,
        ICallable? Key,
        object? DefaultValue);

    private static object Len(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "len(value) expects one argument.", span);
        }

        return arguments[0] switch
        {
            IPySizedValue sized => new BigInteger(sized.Length),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            PyInstance instance => GetInstanceLength(instance, context, span),
            _ => throw new LythonRuntimeException("TypeError", "Object has no len().", span)
        };
    }

    private static BigInteger GetInstanceLength(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute("__len__", context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"object of type '{instance.Type.Name}' has no len()", span);
        }

        var value = callable.Invoke([], span, context);
        if (!PyNumberOps.TryAsInteger(value, out var length))
        {
            throw new LythonRuntimeException("TypeError", "__len__() should return an integer", span);
        }

        if (length < 0)
        {
            throw new LythonRuntimeException("ValueError", "__len__() should return >= 0", span);
        }

        return length;
    }

    private static object Sorted(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            reverse = IsTruthy(arguments[2]);
        }

        using var items = SortItems(
            ToSequence(arguments[0], span, context),
            keyCallable as ICallable,
            reverse,
            span,
            context);

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static async ValueTask<object> SortedAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "sorted(iterable[, key][, reverse]) expects one iterable and optional key/reverse arguments.", span);
        }

        var values = await MaterializeSequenceAsync(arguments[0], span).ConfigureAwait(false);
        var keyCallable = arguments.Length >= 2 ? arguments[1] : null;
        if (keyCallable is not null &&
            !ReferenceEquals(keyCallable, PyNone.Instance) &&
            keyCallable is not ICallable)
        {
            throw new LythonRuntimeException("TypeError", "sorted(..., key=...) expects a callable or None.", span);
        }

        var reverse = false;
        if (arguments.Length >= 3)
        {
            reverse = IsTruthy(arguments[2]);
        }

        using var items = await SortItemsAsync(values, keyCallable as ICallable, reverse, span, context).ConfigureAwait(false);

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<object> AnyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            if (IsTruthy(item))
            {
                return true;
            }
        }

        return false;
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<object> AllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all(iterable) expects one argument.", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            if (!IsTruthy(item))
            {
                return false;
            }
        }

        return true;
    }

    private static object MinMax(CallArgumentValue[] arguments, bool isMin, LythonSourceSpan span, ExecutionContext context)
    {
        var (positional, keyCallable, defaultValue) = BindMinMaxArguments(arguments, isMin ? "min" : "max", span);
        using var enumerator = (positional.Count == 1 ? ToSequence(positional[0], span) : positional).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            if (defaultValue is not null)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{(isMin ? "min" : "max")}() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        var bestKey = keyCallable is null
            ? best
            : keyCallable.Invoke([CallArgumentValue.Positional(best)], span, context);
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            var candidateKey = keyCallable is null
                ? candidate
                : keyCallable.Invoke([CallArgumentValue.Positional(candidate)], span, context);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static PyList CreateNameList(IEnumerable<string> names, ExecutionContext context, LythonSourceSpan span)
        => new(
            names.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name => PyString.FromString(name, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);

    private static IEnumerable<string> EnumerateCurrentLocalNames(ExecutionContext context)
    {
        foreach (var name in context.Variables.Keys)
        {
            if (!ExecutionState.BuiltinNames.Contains(name))
            {
                yield return name;
            }
        }

        if (context.CurrentExecutableFrame is not null)
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
            {
                yield return pair.Key;
            }
        }
    }

    private static readonly string[] StringDirNames = ["capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "index", "isalnum", "isalpha", "isascii", "isdigit", "islower", "isspace", "istitle", "isupper", "join", "lower", "lstrip", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "upper", "zfill"];
    private static readonly string[] BytesDirNames = ["decode", "hex"];
    private static readonly string[] ListDirNames = ["append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"];
    private static readonly string[] DictDirNames = ["clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"];
    private static readonly string[] SetDirNames = ["add", "clear", "copy", "difference", "difference_update", "discard", "intersection", "intersection_update", "isdisjoint", "issubset", "issuperset", "pop", "remove", "symmetric_difference", "symmetric_difference_update", "union", "update"];

    private static async ValueTask<object> MinMaxAsync(CallArgumentValue[] arguments, bool isMin, LythonSourceSpan span, ExecutionContext context)
    {
        var (positional, keyCallable, defaultValue) = BindMinMaxArguments(arguments, isMin ? "min" : "max", span);
        if (positional.Count > 1)
        {
            return await MinMaxValuesAsync(positional, keyCallable, isMin, span, context).ConfigureAwait(false);
        }

        await using var cursor = PyIteration.Cursor.Create(positional[0], span);
        var (hasValue, best) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            if (defaultValue is not null)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{(isMin ? "min" : "max")}() arg is an empty sequence", span);
        }

        var bestKey = keyCallable is null
            ? best
            : await keyCallable.InvokeAsync([CallArgumentValue.Positional(best)], span, context).ConfigureAwait(false);
        while (true)
        {
            var (hasCandidate, candidate) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasCandidate)
            {
                break;
            }

            var candidateKey = keyCallable is null
                ? candidate
                : await keyCallable.InvokeAsync([CallArgumentValue.Positional(candidate)], span, context).ConfigureAwait(false);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static async ValueTask<object> MinMaxValuesAsync(
        IReadOnlyList<object> values,
        ICallable? keyCallable,
        bool isMin,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var best = values[0];
        var bestKey = keyCallable is null
            ? best
            : await keyCallable.InvokeAsync([CallArgumentValue.Positional(best)], span, context).ConfigureAwait(false);
        for (var i = 1; i < values.Count; i++)
        {
            var candidate = values[i];
            var candidateKey = keyCallable is null
                ? candidate
                : await keyCallable.InvokeAsync([CallArgumentValue.Positional(candidate)], span, context).ConfigureAwait(false);
            var comparison = Compare(candidateKey, bestKey, span);
            if (isMin ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static MinMaxArguments BindMinMaxArguments(
        CallArgumentValue[] arguments,
        string name,
        LythonSourceSpan span)
    {
        var positional = new List<object>();
        ICallable? key = null;
        var sawKey = false;
        object? defaultValue = null;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                positional.Add(argument.Value);
                continue;
            }

            if (argument.KeywordName == "key")
            {
                if (sawKey)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() got multiple values for keyword argument 'key'", span);
                }

                sawKey = true;
                if (!ReferenceEquals(argument.Value, PyNone.Instance) && argument.Value is not ICallable)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() key must be callable or None", span);
                }

                key = argument.Value as ICallable;
                continue;
            }

            if (argument.KeywordName == "default")
            {
                if (defaultValue is not null)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() got multiple values for keyword argument 'default'", span);
                }

                defaultValue = argument.Value;
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"{name}() got an unexpected keyword argument '{argument.KeywordName}'", span);
        }

        if (positional.Count == 0)
        {
            throw new LythonRuntimeException("TypeError", $"{name} expected at least 1 argument, got 0", span);
        }

        if (positional.Count > 1 && defaultValue is not null)
        {
            throw new LythonRuntimeException("TypeError", $"Cannot specify a default for {name}() with multiple positional arguments", span);
        }

        return new MinMaxArguments(positional, key, defaultValue);
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        foreach (var item in ToSequence(arguments[0], span, context))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static async ValueTask<object> SumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        EnsureSummableValue(total, span);
        await foreach (var item in ToSequenceAsync(arguments[0], span).ConfigureAwait(false))
        {
            EnsureSummableValue(item, span);
            total = EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static void EnsureSummableValue(object value, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out _) || value is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() does not support string or bytes operands.", span);
        }
    }

    private static object DivMod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "divmod(a, b) expects two arguments.", span);
        }

        if (arguments[0] is PyTimedelta leftDelta && arguments[1] is PyTimedelta rightDelta)
        {
            return PyDateTimeOps.DivMod(leftDelta, rightDelta, context, span);
        }

        return new PyTuple(
            [
                EvaluateFloorDivide(arguments[0], arguments[1], span),
                EvaluateModulo(arguments[0], arguments[1], context, span)
            ],
            context.MemoryGovernor,
            span);
    }

    private static bool IsSortKeyLessThan(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            return leftKey.CompareTo(rightKey, span, context) < 0;
        }

        return EvaluateRichComparison(left, right, "__lt__", "__gt__", context, span, static value => value < 0);
    }

    private static async ValueTask<bool> IsSortKeyLessThanAsync(object left, object right, LythonSourceSpan span, ExecutionContext context)
    {
        if (left is PyCmpKey leftKey &&
            right is PyCmpKey rightKey &&
            ReferenceEquals(leftKey.Comparer, rightKey.Comparer))
        {
            var result = await leftKey.Comparer.InvokeAsync(
                    [CallArgumentValue.Positional(leftKey.Value), CallArgumentValue.Positional(rightKey.Value)],
                    span,
                    context)
                .ConfigureAwait(false);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign < 0;
        }

        return await EvaluateRichComparisonAsync(left, right, "__lt__", "__gt__", context, span, static value => value < 0).ConfigureAwait(false);
    }

    private static PyStableSort.Buffer SortItems(
        IEnumerable<object> values,
        ICallable? keyCallable,
        bool reverse,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var entries = new PyStableSort.Buffer(context.MemoryGovernor, span);
        try
        {
            foreach (var item in values)
            {
                entries.Add(
                    new PyStableSort.Entry(
                        item,
                        keyCallable is null
                            ? item
                            : keyCallable.Invoke([CallArgumentValue.Positional(item)], span, context)),
                    span);
            }

            entries.Sort(
                reverse,
                (left, right) => IsSortKeyLessThan(left, right, span, context));
            return entries;
        }
        catch
        {
            entries.Dispose();
            throw;
        }
    }

    private static async ValueTask<PyStableSort.Buffer> SortItemsAsync(
        IEnumerable<object> values,
        ICallable? keyCallable,
        bool reverse,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var entries = new PyStableSort.Buffer(context.MemoryGovernor, span);
        try
        {
            foreach (var item in values)
            {
                entries.Add(
                    new PyStableSort.Entry(
                        item,
                        keyCallable is null
                            ? item
                            : await keyCallable.InvokeAsync([CallArgumentValue.Positional(item)], span, context).ConfigureAwait(false)),
                    span);
            }

            await entries.SortAsync(
                    reverse,
                    (left, right) => IsSortKeyLessThanAsync(left, right, span, context))
                .ConfigureAwait(false);
            return entries;
        }
        catch
        {
            entries.Dispose();
            throw;
        }
    }

    private static async ValueTask<List<object>> MaterializeSequenceAsync(object value, LythonSourceSpan span)
    {
        if (value is PyGeneratorExpression generator)
        {
            return await generator.MaterializeAsync().ConfigureAwait(false);
        }

        return await PyIteration.MaterializeAsync(value, span).ConfigureAwait(false);
    }

    private static object Range(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        BigInteger start;
        BigInteger stop;
        BigInteger step;

        if (arguments.Length == 1 && arguments[0] is BigInteger stopOnly)
        {
            start = BigInteger.Zero;
            stop = stopOnly;
            step = BigInteger.One;
        }
        else if (arguments.Length == 2 && arguments[0] is BigInteger startArg && arguments[1] is BigInteger stopArg)
        {
            start = startArg;
            stop = stopArg;
            step = BigInteger.One;
        }
        else if (arguments.Length == 3 && arguments[0] is BigInteger startValue && arguments[1] is BigInteger stopValue && arguments[2] is BigInteger stepValue)
        {
            start = startValue;
            stop = stopValue;
            step = stepValue;
        }
        else
        {
            throw new LythonRuntimeException("TypeError", "range(stop), range(start, stop), or range(start, stop, step) expects integer arguments.", span);
        }

        if (step == BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "range() arg 3 must not be zero", span);
        }

        return new PyRange(start, stop, step);
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        var index = arguments.Length == 2 && arguments[1] is BigInteger start
            ? start
            : arguments.Length == 1
                ? BigInteger.Zero
                : throw new LythonRuntimeException("TypeError", "enumerate(iterable, start) expects an integer start.", span);

        return new PyEnumerateIterator(arguments[0], index, span);
    }

    private static object Zip(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        var iterables = new List<object>();
        var strict = false;
        var sawStrict = false;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                iterables.Add(argument.Value);
                continue;
            }

            if (argument.KeywordName != "strict" || sawStrict)
            {
                throw new LythonRuntimeException("TypeError", $"zip() got an unexpected keyword argument '{argument.KeywordName}'", span);
            }

            sawStrict = true;
            strict = IsTruthy(argument.Value);
        }

        return new PyZipIterator([.. iterables], strict, span);
    }

    private static object Zip(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, span);
        if (arguments.Length == 0)
        {
            return result;
        }

        var enumerators = new System.Collections.IEnumerator[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            enumerators[i] = ToSequence(arguments[i], span, context).GetEnumerator();
        }

        try
        {
            while (true)
            {
                var ready = true;
                foreach (var enumerator in enumerators)
                {
                    if (enumerator.MoveNext())
                    {
                        continue;
                    }

                    ready = false;
                    break;
                }

                if (!ready)
                {
                    return result;
                }

                result.Add(CreateTuple(enumerators.Length, i => RuntimeValue(enumerators[i].Current), context, span));
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }

    private static object Iter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 1)
        {
            return arguments[0] is IPyIteratorValue iterator
                ? iterator
                : new PyEnumerableIterator(arguments[0], span);
        }

        if (arguments.Length == 2)
        {
            if (arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "iter(callable, sentinel) expects the first argument to be callable.", span);
            }

            return new PyCallableSentinelIterator(callable, arguments[1], context, span);
        }

        throw new LythonRuntimeException("TypeError", "iter(object[, sentinel]) expects one or two arguments.", span);
    }

    private static object Reversed(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "reversed(sequence) expects one argument.", span);
        }

        var target = arguments[0];
        if (PyMemberAccess.TryResolve(target, "__reversed__", context, span, out var reversedMember))
        {
            if (reversedMember is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "__reversed__ must be callable.", span);
            }

            return RuntimeValue(callable.Invoke([], span, context));
        }

        if (target is IPyIndexableValue indexable)
        {
            return new PyReversedIterator(indexable.Length, indexable.GetIndex);
        }

        if (PyStringOps.TryAsString(target, out var text))
        {
            return new PyReversedIterator(text.Length, text.Index);
        }

        throw new LythonRuntimeException("TypeError", "reversed(sequence) expects a reversible sequence.", span);
    }

    private static object Map(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 2)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects a callable and at least one iterable.", span);
        }

        if (arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects function to be callable.", span);
        }

        var iterables = new object[arguments.Length - 1];
        for (var i = 1; i < arguments.Length; i++)
        {
            _ = PyIteration.Cursor.Create(arguments[i], span);
            iterables[i - 1] = arguments[i];
        }

        return new PyMapIterator(callable, iterables, context, span);
    }

    private static object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects two arguments.", span);
        }

        var function = arguments[0] switch
        {
            PyNone => null,
            ICallable callable => callable,
            _ => throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects function to be callable or None.", span)
        };

        return new PyFilterIterator(function, arguments[1], context, span);
    }

    private static object Slice(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return arguments.Length switch
        {
            1 => new PySlice(PyNone.Instance, arguments[0], PyNone.Instance),
            2 => new PySlice(arguments[0], arguments[1], PyNone.Instance),
            3 => new PySlice(arguments[0], arguments[1], arguments[2]),
            _ => throw new LythonRuntimeException("TypeError", "slice(stop) or slice(start, stop[, step]) expects one to three arguments.", span)
        };
    }

    private static object Next(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        if (arguments[0] is not IPyIteratorValue iterator)
        {
            throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span);
        }

        if (iterator.TryMoveNext(out var value))
        {
            return RuntimeValue(value);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    private static async ValueTask<object> NextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        var (hasValue, value) = arguments[0] switch
        {
            IPyAsyncIteratorValue asyncIterator => await asyncIterator.TryMoveNextAsync().ConfigureAwait(false),
            IPyIteratorValue iterator => iterator.TryMoveNext(out var item)
                ? PyIterationResult.Yield(item)
                : PyIterationResult.End,
            _ => throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span),
        };
        if (hasValue)
        {
            return RuntimeValue(value);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

}
