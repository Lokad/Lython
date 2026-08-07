namespace Lokad.Lython.Runtime;

internal static class RuntimeFailureProjection
{
    public static LythonRuntimeFailure ToPublicFailure(LythonRuntimeException exception)
    {
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

        return new LythonRuntimeFailure(
            exception.ExceptionType,
            exception.Message,
            exception.Span,
            frames,
            exception.SourcePath);
    }
}

