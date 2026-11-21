namespace Lokad.Lython.Frontend;

internal readonly record struct SourceToken(Token Token, int Start, int Length, int LineIndex);

// Positions refer to the original normalized source, including literal bodies
// and joined physical lines. Newline characters belong to their preceding line.
internal sealed class TokenizedSource(string buffer, IReadOnlyList<SourceToken> tokens,
    List<int> newlines, bool hasInvalidTokens, LythonDiagnostic? lexicalDiagnostic = null,
    IReadOnlyList<int>? gluedNumbers = null)
{
    public string Buffer { get; } = buffer;
    public IReadOnlyList<SourceToken> Tokens { get; } = tokens;
    public bool HasInvalidTokens { get; } = hasInvalidTokens || lexicalDiagnostic is not null;
    public LythonDiagnostic? LexicalDiagnostic { get; } = lexicalDiagnostic;
    public IReadOnlyList<int> GluedNumbers { get; } = gluedNumbers ?? [];
    public int Count => Tokens.Count;

    public string GetString(int index) => GetSpan(index).ToString();

    public ReadOnlySpan<char> GetSpan(int index)
    {
        var token = Tokens[index];
        return Buffer.AsSpan(token.Start, token.Length);
    }

    public LythonSourceSpan SpanOf(SourceToken token, int length)
        => new(token.Start, length, token.LineIndex + 1, Column(token.Start, token.LineIndex));

    public void LineOfPosition(int position, out int line, out int column)
    {
        var index = FindLineIndex(newlines, position);
        line = index + 1;
        column = Column(position, index);
    }

    internal static int FindLineIndex(List<int> newlines, int position)
    {
        var index = newlines.BinarySearch(position);
        return index < 0 ? ~index : index;
    }

    private int Column(int position, int lineIndex)
        => position - (lineIndex == 0 ? -1 : newlines[lineIndex - 1]);
}
