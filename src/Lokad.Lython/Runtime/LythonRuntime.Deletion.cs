namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static async ValueTask ExecuteResolvedSubscriptDeletionAsync(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyList && index is PySlice slice)
        {
            await ExecuteSliceDeletionAsync(target, slice.StartBound, slice.StopBound, slice.StepBound, span, context).ConfigureAwait(false);
            return;
        }
        if (target is not PyInstance instance)
        {
            ExecuteResolvedSubscriptDeletion(target, index, span, context);
            return;
        }
        if (!instance.Type.TryLookupInMro("__delitem__", 0, out var raw, out _))
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
        var member = await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false);
        if (member is not ICallable callable)
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
        _ = await callable.InvokeAsync([CallArgumentValue.Positional(index)], span, context).ConfigureAwait(false);
    }

    private static ValueTask ExecuteSliceDeletionAsync(object target, object? start, object? end, object? step, LythonSourceSpan span, ExecutionContext context)
        => ExecuteSliceDeletionCoreAsync(target, start, end, step, span, context, true);
}
