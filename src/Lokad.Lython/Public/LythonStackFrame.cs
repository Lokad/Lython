namespace Lokad.Lython;

/// <summary>Describes one Python frame captured in a runtime failure.</summary>
/// <param name="FunctionName">The Python-visible function name.</param>
/// <param name="Span">The call-site span when known.</param>
/// <param name="SourcePath">The source path supplied by the host when known.</param>
public sealed record LythonStackFrame(
    string FunctionName,
    LythonSourceSpan? Span,
    string? SourcePath)
{
    /// <summary>Creates a frame without source-path metadata.</summary>
    public LythonStackFrame(string functionName, LythonSourceSpan? span)
        : this(functionName, span, null)
    {
    }
}
