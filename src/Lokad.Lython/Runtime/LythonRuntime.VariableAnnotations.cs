using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void InitializeModuleAnnotations(LythonSourceSpan? span, ExecutionContext context)
    {
        if (span is null || context.Variables.ContainsKey("__annotations__")) return;
        var annotations = new PyDict(context.MemoryGovernor, span);
        if (context.GlobalNamespace is not null) StoreGlobalNamespaceName(context, "__annotations__", annotations);
        else context.Variables["__annotations__"] = annotations;
    }

    private static object ResolveVariableAnnotationMapping(ExecutionContext context, LythonSourceSpan span)
    {
        // Annotation stores use LOAD_NAME semantics, even when the class body
        // declares __annotations__ global/nonlocal. The implicit lookup checks
        // the class mapping, then module globals; it does not capture a closure.
        return context.IsClassBody
            ? context.Variables.TryGetValue("__annotations__", out var localAnnotations) ? localAnnotations
                : ResolveName("__annotations__", span, GetGlobalContext(context))
            : ResolveName("__annotations__", span, context);
    }

    private static async ValueTask StoreAnnotationEntryAsync(object annotations, string name, object annotation,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var key = PyString.FromString(name, context.MemoryGovernor, span);
        context.CheckExecution(span);
        if (annotations is PyDict dictionary && !dictionary.ContainsKey(key))
            { _ = dictionary.Count + 1; context.CheckExecution(span); }
        if (asynchronous)
            await SetHeaderSubscriptAsync(annotations, key, annotation, span, context).ConfigureAwait(false);
        else SetSubscriptValue(annotations, key, annotation, span, context);
    }
}
