using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Native file writes share the executable engine's guarded operation. Other
// streams use ordinary attribute dispatch, preserving guest lookup and effects
// without introducing a CLR stream or an ambient capability.
internal static class PyTextStream
{
    internal static PyString ReadAll(object stream, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var method = ResolveMember(stream, "read", span, context);
        var value = LythonRuntime.InvokeCallableTarget(method, span, span, context, static () => []);
        return RequireText(value, span);
    }

    internal static async ValueTask<PyString> ReadAllAsync(object stream, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var method = await ResolveMemberAsync(stream, "read", span, context).ConfigureAwait(false);
        var value = await LythonRuntime.InvokeCallableTargetAsync(method, span, span, context,
            static () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false);
        return RequireText(value, span);
    }

    internal static void Write(object stream, object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (stream is LythonRuntime.ExecutionContext.TextFileHandle or LythonRuntime.ExecutionContext.BinaryFileHandle)
        {
            _ = LythonRuntime.InvokeFileWrite(stream, value, span, context);
            return;
        }
        // Custom encoders may yield arbitrary objects: the writer, as in Python,
        // decides whether a chunk is acceptable. Standard text writers reject
        // non-strings; the writer's return value is ignored.
        var method = ResolveMember(stream, "write", span, context);
        _ = LythonRuntime.InvokeCallableTarget(method, span, span, context, () => [CallArgumentValue.Positional(value)]);
    }

    internal static async ValueTask WriteAsync(object stream, object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (stream is LythonRuntime.ExecutionContext.TextFileHandle or LythonRuntime.ExecutionContext.BinaryFileHandle)
        {
            _ = LythonRuntime.InvokeFileWrite(stream, value, span, context);
            return;
        }
        var method = await ResolveMemberAsync(stream, "write", span, context).ConfigureAwait(false);
        _ = await LythonRuntime.InvokeCallableTargetAsync(method, span, span, context,
            () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional(value)])).ConfigureAwait(false);
    }

    internal static object ResolveMember(object target, string name, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => LythonRuntime.TryResolveRuntimeMember(target, name, context, span, out var value)
            ? value : throw PyMemberAccess.CreateMissingMemberError(target, name, span, context);

    internal static async ValueTask<object> ResolveMemberAsync(object target, string name, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var result = await LythonRuntime.TryResolveRuntimeMemberAsync(target, name, context, span).ConfigureAwait(false);
        return result.Found ? result.Value : throw PyMemberAccess.CreateMissingMemberError(target, name, span, context);
    }

    private static PyString RequireText(object value, LythonSourceSpan span)
        => PyStringOps.TryAsString(value, out var text) ? text
            : throw new LythonRuntimeException("TypeError", "text stream read() must return a string.", span);
}
