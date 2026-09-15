using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyFunctionBinding
{
    public static LythonRuntime.ExecutionContext EnterInvocationFrame(
        LythonRuntime.ExecutionContext closure,
        ScopeDirectiveFacts scopeFacts,
        FunctionBindingPlan bindingPlan,
        BoundCallArguments boundArguments,
        bool mirrorBoundArguments,
        PyType? ownerType,
        LythonSourceSpan span)
    {
        var frame = new LythonRuntime.ExecutionContext(closure, scopeFacts);
        frame.FunctionName = bindingPlan.CallableName;
        if (mirrorBoundArguments)
        {
            var layoutNames = bindingPlan.LayoutParameterNames;
            for (var i = 0; i < layoutNames.Count; i++)
            {
                frame.Variables[layoutNames[i]] = boundArguments.Values[i];
            }
        }

        if (ownerType is not null &&
            bindingPlan.Parameters.Count > 0 &&
            TryGetReceiver(boundArguments, bindingPlan, out var receiver) &&
            (receiver is PyInstance instance && instance.Type.IsSubtypeOf(ownerType) ||
             receiver is PyType type && type.IsSubtypeOf(ownerType)))
        {
            frame.BindImplicitSuper(ownerType, receiver);
        }

        frame.EnterFunctionCall(span);
        return frame;
    }

    private static bool TryGetReceiver(BoundCallArguments boundArguments, FunctionBindingPlan bindingPlan, [MaybeNullWhen(false)] out object receiver)
    {
        receiver = null;
        if (!bindingPlan.LayoutParameterIndex.TryGetValue(bindingPlan.Parameters[0].Name, out var index))
        {
            return false;
        }

        receiver = boundArguments.Values[index];
        return true;
    }

    /// <summary>
    /// Joins enclosing function names for nested qualnames (o.[locals.]i).
    /// Class bodies and modules contribute no segment; methods compose the
    /// defining class at read time instead. Returns null at module scope.
    /// </summary>
    internal static string? EnclosingFunctionPath(LythonRuntime.ExecutionContext context)
    {
        string? path = null;
        for (var current = context; current is not null; current = current.ParentContext)
        {
            if (current.FunctionName is { } name)
            {
                path = path is null ? name : name + ".<locals>." + path;
            }
        }

        return path;
    }

    public static void AnnotateException(
        LythonRuntimeException exception,
        LythonRuntime.ExecutionContext frame,
        string callableName,
        LythonSourceSpan span)
    {
        exception.SetSourcePathIfMissing(frame.SourcePath);
        exception.AddFrame(callableName, span, frame.SourcePath);
    }

}
