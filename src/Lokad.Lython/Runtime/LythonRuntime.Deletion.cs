namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static async ValueTask ExecuteResolvedSubscriptDeletionAsync(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is not PyInstance instance)
        {
            ExecuteResolvedSubscriptDeletion(target, index, span, context);
            return;
        }
        if (!instance.TryGetAttribute("__delitem__", context, span, out var member) || member is not ICallable callable)
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
        _ = await callable.InvokeAsync([CallArgumentValue.Positional(index)], span, context).ConfigureAwait(false);
    }

    private static ValueTask ExecuteSliceDeletionAsync(object target, object? start, object? end, object? step, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyInstance)
            return ExecuteResolvedSubscriptDeletionAsync(target, new PySlice(start ?? PyNone.Instance, end ?? PyNone.Instance, step ?? PyNone.Instance), span, context);
        ExecuteSliceDeletion(target, start, end, step, span, context);
        return ValueTask.CompletedTask;
    }
}
