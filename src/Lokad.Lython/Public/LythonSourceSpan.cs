namespace Lokad.Lython;

public sealed record LythonSourceSpan(
    int Start,
    int Length,
    int Line,
    int Column);
