using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static async ValueTask<object> CounterMostCommonAsync(PyCounter counter, object[] arguments,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var requested = arguments.Length == 0 || arguments[0] is PyNone ? null : arguments[0];
        var sliceAfterSort = false;
        var limit = int.MaxValue;
        if (requested is not null)
        {
            if (await CounterComparisonAsync(BinaryOperatorSyntax.Equal, requested, BigInteger.One,
                context, span, asynchronous).ConfigureAwait(false))
            {
                using var candidates = counter.GetEnumerator();
                if (!candidates.MoveNext()) return new PyList(Array.Empty<object>(), context.MemoryGovernor, span);
                var best = candidates.Current;
                while (candidates.MoveNext())
                {
                    context.CheckExecution(span);
                    var candidate = candidates.Current;
                    if (await CounterComparisonAsync(BinaryOperatorSyntax.Greater, candidate.Value, best.Value,
                        context, span, asynchronous).ConfigureAwait(false)) best = candidate;
                }
                var result = new PyList(1, context.MemoryGovernor, span);
                try
                {
                    result.Add(PyTuple.FromOwnedArray([best.Key, best.Value], context.MemoryGovernor, span));
                    return result;
                }
                catch { result.DiscardUnpublishedStorage(); throw; }
            }
            sliceAfterSort = await CounterComparisonAsync(BinaryOperatorSyntax.GreaterEqual, requested,
                new BigInteger(counter.Count), context, span, asynchronous).ConfigureAwait(false);
        }

        using var entries = counter.GetEnumerator();
        if (requested is not null && !sliceAfterSort)
        {
            var negative = asynchronous
                ? await EvaluateUnaryOperatorAsync(UnaryOperatorSyntax.Minus, requested, context, span).ConfigureAwait(false)
                : EvaluateUnaryOperator(UnaryOperatorSyntax.Minus, requested, context, span);
            var indexed = await CoerceTypeIndexAsync(negative, context, span, asynchronous).ConfigureAwait(false);
            var stop = RequireByteInteger(indexed ?? PyNone.Instance, context, span);
            limit = stop >= 0 ? 0 : (int)BigInteger.Min(-stop, int.MaxValue);
            if (limit == 0) return new PyList(Array.Empty<object>(), context.MemoryGovernor, span);
        }

        using var ordered = await RankCounterAsync(entries, span, context, asynchronous).ConfigureAwait(false);
        if (sliceAfterSort)
        {
            var bound = await CoerceTypeIndexAsync(requested, context, span, asynchronous).ConfigureAwait(false);
            limit = PyIndexing.NormalizeSliceBounds(ordered.Count, null, bound, null, span).Count;
        }
        var count = Math.Min(limit, ordered.Count);
        var output = new PyList(count, context.MemoryGovernor, span);
        try
        {
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                var entry = ordered[i];
                output.Add(PyTuple.FromOwnedArray([entry.Value, entry.Key], context.MemoryGovernor, span));
            }
            return output;
        }
        catch
        {
            foreach (var tuple in output) context.MemoryGovernor.RefundUnpublishedValue(tuple);
            output.DiscardUnpublishedStorage();
            throw;
        }
    }

    private static async ValueTask<PyStableSort.Buffer> RankCounterAsync(IEnumerator<KeyValuePair<object, object>> entries,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var ordered = new PyStableSort.Buffer(context.MemoryGovernor, span);
        try
        {
            while (entries.MoveNext())
            {
                context.CheckExecution(span);
                var pair = entries.Current;
                ordered.Add(new PyStableSort.Entry(pair.Key, pair.Value), span);
            }
            if (asynchronous)
                await ordered.SortAsync(true, (left, right) => CounterComparisonAsync(BinaryOperatorSyntax.Less,
                    left, right, context, span, true), context, span).ConfigureAwait(false);
            else
                ordered.Sort(true, (left, right) => CounterComparisonAsync(BinaryOperatorSyntax.Less,
                    left, right, context, span, false).GetAwaiter().GetResult(), context, span);
            return ordered;
        }
        catch { ordered.Dispose(); throw; }
    }

    private static async ValueTask<bool> CounterComparisonAsync(BinaryOperatorSyntax operation, object left,
        object right, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var result = asynchronous
            ? await EvaluateBinaryOperatorAsync(operation, left, right, context, span).ConfigureAwait(false)
            : EvaluateBinaryOperator(operation, left, right, context, span);
        return asynchronous
            ? await IsTruthyAsync(result, context, span).ConfigureAwait(false)
            : IsTruthy(result, context, span);
    }

    internal static PyString RenderCounter(PyCounter counter, PyRenderingContext rendering)
    {
        if (counter.Count == 0) return PyString.FromString("Counter()", rendering.Context.MemoryGovernor);
        var span = PyStructuralGuard.AmbientSpan ?? counter.AllocationSpan ?? new LythonSourceSpan(0, 0, 0, 0);
        PyStableSort.Buffer ordered;
        try
        {
            using var entries = counter.GetEnumerator();
            ordered = RankCounterAsync(entries, span, rendering.Context, false).GetAwaiter().GetResult();
        }
        catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("TypeError"), error.Identity))
        {
            rendering.Context.CheckExecution(span);
            return RenderPairs(counter.Items);
        }
        using (ordered) return RenderPairs(RankedPairs());

        IEnumerable<KeyValuePair<object, object>> RankedPairs()
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var entry = ordered[i];
                yield return new KeyValuePair<object, object>(entry.Value, entry.Key);
            }
        }
        PyString RenderPairs(IEnumerable<KeyValuePair<object, object>> pairs)
            => PyRendering.JoinRenderedSequence("Counter(",
                [PyRendering.JoinRenderedDictionary(pairs, rendering, interpolated: false)], ")", rendering);
    }
}
