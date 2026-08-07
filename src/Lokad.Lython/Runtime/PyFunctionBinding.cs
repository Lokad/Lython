using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyFunctionBinding
{
    public static LythonRuntime.ExecutionContext EnterInvocationFrame(
        LythonRuntime.ExecutionContext closure,
        ScopeDirectiveFacts scopeFacts,
        FunctionBindingPlan bindingPlan,
        IReadOnlyDictionary<string, object> boundArguments,
        bool mirrorBoundArguments,
        PyType? ownerType,
        LythonSourceSpan span)
    {
        var frame = new LythonRuntime.ExecutionContext(closure, scopeFacts);
        if (mirrorBoundArguments)
        {
            foreach (var pair in boundArguments)
            {
                frame.Variables[pair.Key] = pair.Value;
            }
        }

        if (ownerType is not null &&
            bindingPlan.Parameters.Count > 0 &&
            boundArguments.TryGetValue(bindingPlan.Parameters[0].Name, out var receiver) &&
            (receiver is PyInstance instance && instance.Type.IsSubtypeOf(ownerType) ||
             receiver is PyType type && type.IsSubtypeOf(ownerType)))
        {
            frame.BindImplicitSuper(ownerType, receiver);
        }

        frame.EnterFunctionCall(span);
        return frame;
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
