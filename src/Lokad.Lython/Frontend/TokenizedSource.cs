namespace Lokad.Lython.Frontend;

internal readonly record struct SourceToken(Token Token, int Start, int Length);

// Positions refer to the original normalized source, including literal bodies
// and joined physical lines. Newline characters belong to their preceding line.
internal sealed class TokenizedSource(string buffer, IReadOnlyList<SourceToken> tokens,
    List<int> newlines, bool hasInvalidTokens, LythonDiagnostic? lexicalDiagnostic = null)
{
    public string Buffer { get; } = buffer;
    public IReadOnlyList<SourceToken> Tokens { get; } = tokens;
    public bool HasInvalidTokens { get; } = hasInvalidTokens || lexicalDiagnostic is not null;
    public LythonDiagnostic? LexicalDiagnostic { get; } = lexicalDiagnostic;
    public int Count => Tokens.Count;

    public string GetString(int index) => GetSpan(index).ToString();

    public ReadOnlySpan<char> GetSpan(int index)
    {
        var token = Tokens[index];
        return Buffer.AsSpan(token.Start, token.Length);
    }

    public void LineOfPosition(int position, out int line, out int column)
    {
        var index = newlines.BinarySearch(position);
        if (index < 0) index = ~index;
        line = index + 1;
        column = position - (index == 0 ? -1 : newlines[index - 1]);
    }
}
