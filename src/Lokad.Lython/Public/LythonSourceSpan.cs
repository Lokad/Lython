namespace Lokad.Lython;

/// <summary>Identifies a zero-based range and its one-based line and column in Python source.</summary>
/// <param name="Start">The zero-based UTF-16 offset in the source string.</param>
/// <param name="Length">The range length in UTF-16 code units.</param>
/// <param name="Line">The one-based source line.</param>
/// <param name="Column">The one-based source column.</param>
public sealed record LythonSourceSpan(
    int Start,
    int Length,
    int Line,
    int Column);
