namespace Lokad.Lython.Runtime;

// Unsealed for PyNotIterableException (R17): protocol absence carries a
// distinct type so catchers never match English message text.
internal class LythonRuntimeException : Exception
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
        LythonSourceSpan? span)
        : this(identity, message, span, null, null)
    {
    }

    public LythonRuntimeException(
        PythonExceptionIdentity identity,
        string message,
        LythonSourceSpan? span,
        Exception? innerException,
        object? payload)
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

    internal LythonFailureAttribution? Attribution { get; set; }

    public PyException? OriginalPythonException { get; set; }

    // Sidecars are needed only before a native error becomes a guest value.
    // Thereafter the original Python object owns its live mutable chain.
    private PyException? _pythonCause, _pythonContext;
    private bool _suppressPythonContext;
    public PyException? PythonCause
    {
        get => OriginalPythonException is { } original ? original.Cause : _pythonCause;
        set { if (OriginalPythonException is { } original) original.Cause = value; else _pythonCause = value; }
    }
    public PyException? PythonContext
    {
        get => OriginalPythonException is { } original ? original.Context : _pythonContext;
        set { if (OriginalPythonException is { } original) original.Context = value; else _pythonContext = value; }
    }
    public bool SuppressPythonContext
    {
        get => OriginalPythonException is { } original ? original.SuppressContext : _suppressPythonContext;
        set { if (OriginalPythonException is { } original) original.SuppressContext = value; else _suppressPythonContext = value; }
    }

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
