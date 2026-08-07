namespace Lokad.Lython.Runtime;

internal sealed class LythonRuntimeException : Exception
{
    public LythonRuntimeException(string exceptionType, string message, LythonSourceSpan? span) : this(exceptionType, message, span, null, null) { }

    public LythonRuntimeException(string exceptionType, string message, LythonSourceSpan? span, Exception? innerException) : this(exceptionType, message, span, innerException, null) { }

    public LythonRuntimeException(
        string exceptionType,
        string message,
        LythonSourceSpan? span,
        Exception? innerException,
        object? payload)
        : base(message, innerException)
    {
        ExceptionType = exceptionType;
        Span = span;
        Payload = payload;
    }

    public string ExceptionType { get; }

    public LythonSourceSpan? Span { get; }

    public object? Payload { get; }

    public string? SourcePath { get; private set; }

    public List<LythonStackFrame> Frames { get; } = [];

    public void SetSourcePathIfMissing(string? sourcePath)
    {
        if (SourcePath is null && sourcePath is not null)
        {
            SourcePath = sourcePath;
        }
    }

    public void AddFrame(string functionName, LythonSourceSpan span)
        => AddFrame(functionName, span, null);

    public void AddFrame(string functionName, LythonSourceSpan span, string? sourcePath)
    {
        Frames.Insert(0, new LythonStackFrame(functionName, span, sourcePath));
    }
}
