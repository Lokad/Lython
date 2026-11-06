using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Runtime;

internal static partial class PyContainment
{
    internal static bool IsSetViewOperand(object value)
        => value is PySet or LythonRuntime.DictKeysView or LythonRuntime.DictItemsView or ChainMapKeysView or ChainMapItemsView;

    internal static bool IsSetView(object value)
        => value is LythonRuntime.DictKeysView or LythonRuntime.DictItemsView or ChainMapKeysView or ChainMapItemsView;

    internal static bool CompareSetViews(object left, object right, string method, Context? context, LythonSourceSpan span)
        => CompareSetViewsAsync(left, right, method, context, span, asynchronous: false).GetAwaiter().GetResult();

    internal static async ValueTask<bool> CompareSetViewsAsync(
        object left, object right, string method, Context? context, LythonSourceSpan span, bool asynchronous)
    {
        if (!IsSetViewOperand(left) || !IsSetViewOperand(right)) return method == "__ne__";
        // Native dict/set comparisons decline collections.abc views. Their
        // reflected equality iterates the ChainMap view, so preserve that order.
        if (method is "__eq__" or "__ne__" && left is not (ChainMapKeysView or ChainMapItemsView) &&
            right is ChainMapKeysView or ChainMapItemsView)
            (left, right) = (right, left);
        using var guard = PyStructuralGuard.EnterPair(left, right, span, context);
        int leftCount, rightCount;
        using (var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span))
        {
            leftCount = left is PySet leftSet ? leftSet.Count : ((IReadOnlyCollection<object>)left).Count;
            rightCount = right is PySet rightSet ? rightSet.Count : ((IReadOnlyCollection<object>)right).Count;
        }
        context?.CheckExecution(span);
        var eligible = method switch
        {
            "__eq__" or "__ne__" => leftCount == rightCount,
            "__lt__" => leftCount < rightCount,
            "__le__" => leftCount <= rightCount,
            "__gt__" => leftCount > rightCount,
            "__ge__" => leftCount >= rightCount,
            _ => throw new InvalidOperationException("Unknown set-view comparison: " + method),
        };
        if (!eligible) return method == "__ne__";
        if (method is "__gt__" or "__ge__") (left, right) = (right, left);

        var governor = context?.MemoryGovernor ?? left switch
        {
            LythonRuntime.DictKeysView keys => keys.Source.OwnerMemoryGovernor,
            LythonRuntime.DictItemsView items => items.Source.OwnerMemoryGovernor,
            PySet set => set.OwnerMemoryGovernor,
            _ => null,
        };
        var retainedBytes = left switch
        {
            LythonRuntime.DictKeysView keys => keys.Source.CommittedStorageBytes,
            LythonRuntime.DictItemsView items => items.Source.CommittedStorageBytes,
            PySet set => set.CommittedStorageBytes,
            _ => 0, // ChainMap enumeration owns its admitted merged-key snapshot.
        };
        using var retained = governor?.ReserveTemporary(retainedBytes, span);
        var contained = true;
        if (left is LythonRuntime.DictItemsView source && right is LythonRuntime.DictItemsView target)
        {
            // Stored pairs already contain both values; no guest tuple or temporary
            // set is needed to compare ordinary items views.
            foreach (var pair in source.Source)
            {
                context?.CheckExecution(span);
                var found = await LookupViewKeyAsync(target.Source, pair.Key, context, span, asynchronous).ConfigureAwait(false);
                var matches = found.Found && (asynchronous && context is not null
                    ? await LythonRuntime.ElementEqualsAsync(found.Value, pair.Value, context, span).ConfigureAwait(false)
                    : context is null ? LythonRuntime.ElementEqualsAmbient(found.Value, pair.Value)
                    : LythonRuntime.ElementEquals(found.Value, pair.Value, context, span));
                context?.CheckExecution(span);
                if (!matches) { contained = false; break; }
            }
        }
        else
        {
            IEnumerator<object> cursor;
            using (var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span))
                cursor = ((IEnumerable<object>)left).GetEnumerator();
            using (cursor)
            {
                while (true)
                {
                    context?.CheckExecution(span);
                    // ChainMap enumerators can reach synchronous key/value hooks.
                    // Re-establish provenance only while advancing, never over await.
                    using (var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span))
                        if (!cursor.MoveNext()) break;
                    var matches = context is null ? Contains(right, cursor.Current, span)
                        : asynchronous ? await ContainsWithProtocolsAsync(right, cursor.Current, context, span).ConfigureAwait(false)
                        : ContainsWithProtocols(right, cursor.Current, context, span);
                    context?.CheckExecution(span);
                    if (!matches) { contained = false; break; }
                }
            }
        }
        return method == "__ne__" ? !contained : contained;
    }

    private static ValueTask<(bool Found, object Value)> LookupViewKeyAsync(
        PyDict dict, object key, Context? context, LythonSourceSpan span, bool asynchronous)
    {
        LythonRuntime.ValidateDictionaryKey(key, span);
        if (asynchronous && context is not null)
            return dict.TryGetKeyAsync(key, context, span);
        var found = context is null ? dict.TryGetValue(key, out var value) : dict.TryGetValue(key, out value, context, span);
        return new((found, value ?? PyNone.Instance));
    }

    private static bool ContainsInDictItems(LythonRuntime.DictItemsView view, object candidate, LythonSourceSpan span)
    {
        if (!PyTupleLike.TryGetItems(candidate, out var pair) || pair.Count != 2) return false;
        var key = LythonRuntime.ValidateDictionaryKey(pair[0], span);
        var matches = view.Source.TryGetValue(key, out var value) && LythonRuntime.ElementEqualsAmbient(value, pair[1]);
        if (!PyStructuralGuard.GuestDispatchSuppressed) PyStructuralGuard.AmbientContext?.CheckExecution(span);
        return matches;
    }

    private static async ValueTask<bool> ContainsInDictKeysAsync(
        LythonRuntime.DictKeysView view, object candidate, Context context, LythonSourceSpan span)
    {
        var found = await LookupViewKeyAsync(view.Source, candidate, context, span, true).ConfigureAwait(false);
        context.CheckExecution(span);
        return found.Found;
    }

    private static async ValueTask<bool> ContainsInDictItemsAsync(
        LythonRuntime.DictItemsView view, object candidate, Context context, LythonSourceSpan span)
    {
        if (!PyTupleLike.TryGetItems(candidate, out var pair) || pair.Count != 2) return false;
        var found = await LookupViewKeyAsync(view.Source, pair[0], context, span, true).ConfigureAwait(false);
        var matches = found.Found && await LythonRuntime.ElementEqualsAsync(found.Value, pair[1], context, span).ConfigureAwait(false);
        context.CheckExecution(span);
        return matches;
    }
}
