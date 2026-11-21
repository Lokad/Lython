using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class TokenSourceCoordinatesTests
{
    [Theory]
    [InlineData("if True:\n    value = '''α\nβ'''\n    result = (1 + \\\n        2)\n\nx = 3\n")]
    [InlineData("value = (\n    1\n    # An unfinished group ends at its last token.\n")]
    [InlineData("text = f\"\"\"before\n{len('inner\\\ncontinued')}\n{('one', # comment\n 'two')[0]}\nend\"\"\"\nlast = 1\n")]
    [InlineData("text = f'''first\n{(missing}'''\nlast = 1\n")]
    public void TokenCoordinatesMatchPhysicalSourcePositions(string source)
    {
        var tokens = PythonTokenization.Read(source);
        var expectedLine = 1;
        var expectedColumn = 1;
        for (var position = 0; position <= source.Length; position++)
        {
            tokens.LineOfPosition(position, out var actualLine, out var actualColumn);
            Assert.Equal((expectedLine, expectedColumn), (actualLine, actualColumn));
            if (position == source.Length) break;
            if (source[position] == '\n')
            {
                expectedLine++;
                expectedColumn = 1;
            }
            else expectedColumn++;
        }

        foreach (var token in tokens.Tokens)
        {
            tokens.LineOfPosition(token.Start, out var line, out var column);
            Assert.Equal(new LythonSourceSpan(token.Start, token.Length, line, column), tokens.SpanOf(token, token.Length));
        }
    }
}
