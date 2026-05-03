namespace Lokad.Lython.Runtime;

internal static class RuntimeFailureProjection
{
    public static LythonRuntimeFailure ToPublicFailure(LythonRuntimeException exception)
    {
        var frames = exception.Frames.Count == 0
            ? [new LythonStackFrame("<module>", exception.Span, exception.SourcePath)]
            : exception.Frames.ToArray();

        return new LythonRuntimeFailure(
            exception.ExceptionType,
            exception.Message,
            exception.Span,
            frames,
            exception.SourcePath);
    }
}

