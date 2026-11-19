using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class TokenSourceCoordinatesTests
{
    [Theory]
    [InlineData("if True:\n    value = '''α\nβ'''\n    result = (1 + \\\n        2)\n\nx = 3\n")]
    [InlineData("value = (\n    1\n    # An unfinished group ends at its last token.\n")]
    public void TokenCoordinatesMatchPhysicalSourcePositions(string source)
    {
        var tokens = PythonTokenization.Read(source);
        foreach (var token in tokens.Tokens)
        {
            tokens.LineOfPosition(token.Start, out var line, out var column);
            Assert.Equal(new LythonSourceSpan(token.Start, token.Length, line, column), tokens.SpanOf(token));
        }
    }
}
