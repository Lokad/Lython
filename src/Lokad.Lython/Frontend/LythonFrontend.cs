using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal static class LythonFrontend
{
    public static FrontendResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source))
        {
            return new FrontendResult(new ScriptSyntax(Array.Empty<StatementSyntax>()), Array.Empty<LythonDiagnostic>());
        }

        var tokenReader = new ReflectionTokenReader<Token>();
        var tokens = tokenReader.ReadAllTokens(source);

        if (tokens.HasInvalidTokens)
        {
            var invalid = tokens.Tokens.First(t => t.Token == Token.Error);
            return new FrontendResult(
                null,
                [new LythonDiagnostic(
                    Code: "LA0001",
                    Message: $"Invalid character '{source[invalid.Start]}'.",
                    Severity: LythonDiagnosticSeverity.Error,
                    Span: SpanOf(tokens, invalid.Start, invalid.Length))]);
        }

        var parsed = new Parser(tokens).Parse();
        if (parsed.Script is null)
        {
            return parsed;
        }

        var diagnostics = parsed.Diagnostics
            .Concat(AnnotationDiagnostics.Analyze(parsed.Script))
            .Concat(StaticAnalyzer.Analyze(parsed.Script))
            .ToArray();
        return new FrontendResult(parsed.Script, diagnostics);
    }

    private static LythonSourceSpan SpanOf(LexerResult<Token> tokens, int start, int length)
    {
        tokens.LineOfPosition(start, out var line, out var column);
        return new LythonSourceSpan(start, length, line, column);
    }
}
