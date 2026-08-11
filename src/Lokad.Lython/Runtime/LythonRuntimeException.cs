namespace Lokad.Lython.Runtime;

internal sealed class LythonRuntimeException : Exception
{
    public LythonRuntimeException(string exceptionType, string message, LythonSourceSpan? span)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(exceptionType), message, span, null, null) { }

    public LythonRuntimeException(string exceptionType, string message, LythonSourceSpan? span, Exception? innerException)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(exceptionType), message, span, innerException, null) { }

    public LythonRuntimeException(
        string exceptionType,
        string message,
        LythonSourceSpan? span,
        Exception? innerException,
        object? payload)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(exceptionType), message, span, innerException, payload)
    {
    }

    public LythonRuntimeException(
        PythonExceptionIdentity identity,
        string message,
        LythonSourceSpan? span,
        Exception? innerException = null,
        object? payload = null)
        : base(message, innerException)
    {
        Identity = identity;
        Span = span;
        Payload = payload;
    }

    public PythonExceptionIdentity Identity { get; }

    public string ExceptionType => Identity.TypeName;

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
        Frames.Add(new LythonStackFrame(functionName, span, sourcePath));
    }
}
