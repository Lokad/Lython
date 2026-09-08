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
        object? KeyArgument,
        OptionalValue<object> Default);

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

        // Materialize before resolving the key so one-time iterable effects precede
        // the key error, matching CPython. R13: an invalid key only fails when it
        // would actually be called, so empty input with a bad key succeeds.
        var materialized = new PyList(ToSequence(arguments[0], span, context), context.MemoryGovernor, span);
        context.ObserveCollectionCount(materialized.Count, span);

        var keyArgument = arguments.Length >= 2 ? arguments[1] : null;

        var reverse = false;
        if (arguments.Length >= 3)
        {
            reverse = IsTruthy(arguments[2]);
        }

        using var items = SortItems(
            materialized,
            keyArgument,
            reverse,
            span,
            context,
            "sorted(..., key=...) expects a callable or None.");

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

        var values = await MaterializeSequenceAsync(arguments[0], span, context).ConfigureAwait(false);
        var keyArgument = arguments.Length >= 2 ? arguments[1] : null;

        var reverse = false;
        if (arguments.Length >= 3)
        {
            reverse = IsTruthy(arguments[2]);
        }

        using var items = await SortItemsAsync(values, keyArgument, reverse, span, context, "sorted(..., key=...) expects a callable or None.").ConfigureAwait(false);

        var result = new PyList(items, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object MinMax(CallArgumentValue[] arguments, ExtremumOperation operation, LythonSourceSpan span, ExecutionContext context)
    {
        var operationName = operation == ExtremumOperation.Minimum ? "min" : "max";
        var (positional, keyArgument, defaultValue) = BindMinMaxArguments(arguments, operationName, span);
        using var enumerator = (positional.Count == 1 ? ToSequence(positional[0], span, context) : positional).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            throw new LythonRuntimeException("ValueError", $"{operationName}() arg is an empty sequence", span);
        }

        var best = enumerator.Current;
        var keyCallable = keyArgument as ICallable;
        if (keyArgument is not null && keyCallable is null)
        {
            throw new LythonRuntimeException("TypeError", $"{operationName}() key must be callable or None", span);
        }

        var bestKey = keyCallable is null
            ? best
            : CallableInvocation.InvokeUnary(keyCallable, best, span, context);
        while (enumerator.MoveNext())
        {
            var candidate = enumerator.Current;
            var candidateKey = keyCallable is null
                ? candidate
                : CallableInvocation.InvokeUnary(keyCallable, candidate, span, context);
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
        var (positional, keyArgument, defaultValue) = BindMinMaxArguments(arguments, operationName, span);
        if (positional.Count > 1)
        {
            return await MinMaxValuesAsync(positional, keyArgument, operation, span, context).ConfigureAwait(false);
        }

        await using var cursor = PyIteration.Cursor.Create(positional[0], span, context);
        var (hasValue, best) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }

            throw new LythonRuntimeException("ValueError", $"{operationName}() arg is an empty sequence", span);
        }

        var keyCallable = keyArgument as ICallable;
        if (keyArgument is not null && keyCallable is null)
        {
            throw new LythonRuntimeException("TypeError", $"{operationName}() key must be callable or None", span);
        }

        var bestKey = keyCallable is null
            ? best
            : await CallableInvocation.InvokeUnaryAsync(keyCallable, best, span, context).ConfigureAwait(false);
        while (true)
        {
            var (hasCandidate, candidate) = await cursor.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasCandidate)
            {
                break;
            }

            var candidateKey = keyCallable is null
                ? candidate
                : await CallableInvocation.InvokeUnaryAsync(keyCallable, candidate, span, context).ConfigureAwait(false);
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
        object? keyArgument,
        ExtremumOperation operation,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var keyCallable = keyArgument as ICallable;
        if (keyArgument is not null && keyCallable is null)
        {
            throw new LythonRuntimeException("TypeError", "min()/max() key must be callable or None", span);
        }

        var best = values[0];
        var bestKey = keyCallable is null
            ? best
            : await CallableInvocation.InvokeUnaryAsync(keyCallable, best, span, context).ConfigureAwait(false);
        for (var i = 1; i < values.Count; i++)
        {
            var candidate = values[i];
            var candidateKey = keyCallable is null
                ? candidate
                : await CallableInvocation.InvokeUnaryAsync(keyCallable, candidate, span, context).ConfigureAwait(false);
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
        object? key = null;
        var sawKey = false;
        var defaultValue = OptionalValue<object>.Missing;
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
                // R13: delay invalid-key errors until the key would actually be
                // called, so empty input with a bad key returns default/raises
                // ValueError instead of TypeError.
                key = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                continue;
            }

            if (argument.KeywordName == "default")
            {
                if (defaultValue.HasValue)
                {
                    throw new LythonRuntimeException("TypeError", $"{name}() got multiple values for keyword argument 'default'", span);
                }

                defaultValue = OptionalValue<object>.Present(argument.Value);
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"{name}() got an unexpected keyword argument '{argument.KeywordName}'", span);
        }

        if (positional.Count == 0)
        {
            throw new LythonRuntimeException("TypeError", $"{name} expected at least 1 argument, got 0", span);
        }

        if (positional.Count > 1 && defaultValue.HasValue)
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
            var result = await CallableInvocation.InvokeBinaryAsync(
                    leftKey.Comparer,
                    leftKey.Value,
                    rightKey.Value,
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
        object? keyArgument,
        bool reverse,
        LythonSourceSpan span,
        ExecutionContext context,
        string keyErrorMessage)
    {
        var entries = new PyStableSort.Buffer(context.MemoryGovernor, span);
        try
        {
            var keyCallable = keyArgument as ICallable;
            var keyInvalid = keyArgument is not null &&
                !ReferenceEquals(keyArgument, PyNone.Instance) &&
                keyCallable is null;
            foreach (var item in values)
            {
                // R13: an invalid key fails only when it would actually be called.
                if (keyInvalid)
                {
                    throw new LythonRuntimeException("TypeError", keyErrorMessage, span);
                }

                entries.Add(
                    new PyStableSort.Entry(
                        item,
                        keyCallable is null
                            ? item
                            : CallableInvocation.InvokeUnary(keyCallable, item, span, context)),
                    span);
                context.ObserveCollectionCount(entries.Count, span);
                if ((entries.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
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
        object? keyArgument,
        bool reverse,
        LythonSourceSpan span,
        ExecutionContext context,
        string keyErrorMessage)
    {
        var entries = new PyStableSort.Buffer(context.MemoryGovernor, span);
        try
        {
            var keyCallable = keyArgument as ICallable;
            var keyInvalid = keyArgument is not null &&
                !ReferenceEquals(keyArgument, PyNone.Instance) &&
                keyCallable is null;
            foreach (var item in values)
            {
                // R13: an invalid key fails only when it would actually be called.
                if (keyInvalid)
                {
                    throw new LythonRuntimeException("TypeError", keyErrorMessage, span);
                }

                entries.Add(
                    new PyStableSort.Entry(
                        item,
                        keyCallable is null
                            ? item
                            : await CallableInvocation.InvokeUnaryAsync(keyCallable, item, span, context).ConfigureAwait(false)),
                    span);
                context.ObserveCollectionCount(entries.Count, span);
                if ((entries.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
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

    private static async ValueTask<List<object>> MaterializeSequenceAsync(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (value is PyGeneratorExpression generator)
        {
            return await generator.MaterializeAsync().ConfigureAwait(false);
        }

        return await PyIteration.MaterializeAsync(value, span, context).ConfigureAwait(false);
    }
}
