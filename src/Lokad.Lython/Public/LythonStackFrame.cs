namespace Lokad.Lython;

public sealed record LythonStackFrame(
    string FunctionName,
    LythonSourceSpan? Span,
    string? SourcePath)
{
    public LythonStackFrame(string functionName, LythonSourceSpan? span)
        : this(functionName, span, null)
    {
    }
}
