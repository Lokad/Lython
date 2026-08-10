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

    private enum ExtremumOperation
    {
        Minimum,
        Maximum,
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

    private static object MinMax(CallArgumentValue[] arguments, ExtremumOperation operation, LythonSourceSpan span, ExecutionContext context)
    {
        var operationName = operation == ExtremumOperation.Minimum ? "min" : "max";
        var (positional, keyCallable, defaultValue) = BindMinMaxArguments(arguments, operationName, span);
        using var enumerator = (positional.Count == 1 ? ToSequence(positional[0], span) : positional).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            if (defaultValue is not null)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{operationName}() arg is an empty sequence", span);
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
            if (operation == ExtremumOperation.Minimum ? comparison < 0 : comparison > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }

        return best;
    }

    private static async ValueTask<object> MinMaxAsync(CallArgumentValue[] arguments, ExtremumOperation operation, LythonSourceSpan span, ExecutionContext context)
    {
        var operationName = operation == ExtremumOperation.Minimum ? "min" : "max";
        var (positional, keyCallable, defaultValue) = BindMinMaxArguments(arguments, operationName, span);
        if (positional.Count > 1)
        {
            return await MinMaxValuesAsync(positional, keyCallable, operation, span, context).ConfigureAwait(false);
        }

        await using var cursor = PyIteration.Cursor.Create(positional[0], span);
        var (hasValue, best) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            if (defaultValue is not null)
            {
                return defaultValue;
            }

            throw new LythonRuntimeException("ValueError", $"{operationName}() arg is an empty sequence", span);
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
            if (operation == ExtremumOperation.Minimum ? comparison < 0 : comparison > 0)
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
        ExtremumOperation operation,
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
            if (operation == ExtremumOperation.Minimum ? comparison < 0 : comparison > 0)
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
}
