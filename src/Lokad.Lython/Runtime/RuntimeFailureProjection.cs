namespace Lokad.Lython.Runtime;

internal static class RuntimeFailureProjection
{
    public static LythonRuntimeFailure ToPublicFailure(LythonRuntimeException exception, ProjectionBudget? budget, LythonRuntime.ExecutionContext? context = null, string? renderedMessage = null)
    {
        // Admit the frame array before allocation. Existing frame records alias
        // engine state; diagnostic strings use the same supplied result owner.
        ReserveOrThrow(budget, checked(32L + (16L * Math.Max(1, exception.Frames.Count))));
        LythonStackFrame[] frames;
        if (exception.Frames.Count == 0)
        {
            frames = [new LythonStackFrame("<module>", exception.Span, exception.SourcePath)];
        }
        else
        {
            frames = new LythonStackFrame[exception.Frames.Count];
            for (var index = 0; index < frames.Length; index++)
            {
                frames[index] = exception.Frames[^(index + 1)];
            }
        }

        var message = renderedMessage ?? RenderFailureMessage(exception, context);
        ReserveOrThrow(budget, checked(32L + (2L * message.Length)));
        return new LythonRuntimeFailure(
            exception.ExceptionType,
            message,
            exception.Span,
            frames,
            exception.SourcePath) { Attribution = exception.Attribution };
    }

    // Mapping misses carry their key as the payload; like CPython and the
    // caught str() path, the projected message renders the key through repr.
    // Constructed exceptions defer authoritative args to this diagnostic boundary.
    // Other payload-free or unrenderable errors keep the stored message; the
    // caller preserves a first admission refusal encountered while rendering.
    internal static string RenderFailureMessage(LythonRuntimeException exception, LythonRuntime.ExecutionContext? context)
    {
        if (context is null) return exception.Message;
        try
        {
            if (exception.OriginalPythonException is { Args: not null } pythonException)
                return OwnMessage(PyRendering.ToInterpolatedPyString(pythonException, new PyRenderingContext(context)), context);
            if (exception.ExceptionType == "KeyError" && exception.Payload is not null)
                return OwnMessage(PyRendering.ToReprPyString(exception.Payload, new PyRenderingContext(context)), context);
        }
        catch (Exception) { }
        return exception.Message;
    }

    internal static async ValueTask<string> RenderFailureMessageAsync(LythonRuntimeException exception, LythonRuntime.ExecutionContext? context)
    {
        if (context is null) return exception.Message;
        try
        {
            if (exception.OriginalPythonException is { Args: not null } pythonException)
                return OwnMessage(await PyRendering.ToInterpolatedPyStringAsync(pythonException, new PyRenderingContext(context)).ConfigureAwait(false), context);
            if (exception.ExceptionType == "KeyError" && exception.Payload is not null)
                return OwnMessage(await PyRendering.ToReprPyStringAsync(exception.Payload, new PyRenderingContext(context)).ConfigureAwait(false), context);
        }
        catch (Exception) { }
        return exception.Message;
    }

    private static string OwnMessage(Text.PyString message, LythonRuntime.ExecutionContext context)
    {
        // Fund conversion before the CLR string exists, including UTF-8 overlap.
        // Final projection admits the surviving alias before this run owner ends.
        // UTF-8 length bounds UTF-16 width without another full text scan.
        var bytes = checked(32L + 2L * message.Utf8Bytes.Length);
        var governor = context.MemoryGovernor;
        governor.Reserve(bytes, null);
        try
        {
            var text = message.AsString();
            governor.Commit(bytes);
            return text;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    // Reserve is atomic, so this needs no precheck of its own: a denial
    // throws without moving the counter.
    private static void ReserveOrThrow(ProjectionBudget? budget, long bytes)
        => budget?.Reserve(bytes);
}
