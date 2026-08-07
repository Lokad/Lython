using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal static class LythonFrontend
{
    public static FrontendResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NormalizeSourceText(source);

        if (string.IsNullOrWhiteSpace(source))
        {
            return new FrontendResult(new ScriptSyntax(Array.Empty<StatementSyntax>()), Array.Empty<LythonDiagnostic>());
        }

        if (TryCreateLeadingIndentationDiagnostic(source, out var indentationDiagnostic))
        {
            return new FrontendResult(null, [indentationDiagnostic]);
        }

        var tokenReader = new ReflectionTokenReader<Token>();
        var tokens = tokenReader.ReadAllTokens(source);

        if (tokens.HasInvalidTokens)
        {
            var invalid = tokens.Tokens.First(t => t.Token == Token.Error);
            return new FrontendResult(
                null,
                [CreateLexicalDiagnostic(source, tokens, invalid.Start, invalid.Length)]);
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

    private static string NormalizeSourceText(string source)
    {
        if (source.Length > 0 && source[0] == '\uFEFF')
        {
            source = source[1..];
        }

        return source.Contains('\r', StringComparison.Ordinal)
            ? source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : source;
    }

    private static LythonSourceSpan SpanOf(LexerResult<Token> tokens, int start, int length)
    {
        tokens.LineOfPosition(start, out var line, out var column);
        return new LythonSourceSpan(start, length, line, column);
    }

    private static LythonDiagnostic CreateLexicalDiagnostic(LexerResult<Token> tokens, string message, int start, int length)
    {
        return new LythonDiagnostic(
            Code: "LA0001",
            Message: message,
            Severity: LythonDiagnosticSeverity.Error,
            Span: SpanOf(tokens, start, Math.Max(length, 1)));
    }

    private static LythonDiagnostic CreateLexicalDiagnostic(string source, LexerResult<Token> tokens, int start, int length)
    {
        if (TryClassifyStringLexicalError(source, start, out var message, out var spanStart, out var spanLength))
        {
            return CreateLexicalDiagnostic(tokens, message, spanStart, spanLength);
        }

        var invalidCharacter = start >= 0 && start < source.Length
            ? FormatInvalidCharacter(source[start])
            : "end-of-script";

        return CreateLexicalDiagnostic(tokens, $"Invalid character '{invalidCharacter}'.", start, length);
    }

    private static bool TryClassifyStringLexicalError(
        string source,
        int start,
        out string message,
        out int spanStart,
        out int spanLength)
    {
        message = string.Empty;
        spanStart = start;
        spanLength = 1;

        var quoteStart = start;
        if (quoteStart < source.Length &&
            source[quoteStart] is 'r' or 'R' &&
            quoteStart + 1 < source.Length &&
            IsQuote(source[quoteStart + 1]))
        {
            quoteStart++;
        }

        if (quoteStart < 0 || quoteStart >= source.Length || !IsQuote(source[quoteStart]))
        {
            return false;
        }

        var quote = source[quoteStart];
        while (quoteStart > 0 &&
            quoteStart > start - 2 &&
            source[quoteStart - 1] == quote)
        {
            quoteStart--;
        }

        var isTriple = quoteStart + 2 < source.Length &&
            source[quoteStart + 1] == quote &&
            source[quoteStart + 2] == quote;
        var contentStart = quoteStart + (isTriple ? 3 : 1);

        spanStart = quoteStart;
        if (isTriple)
        {
            if (source.IndexOf(new string(quote, 3), contentStart, StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            message = "Unterminated triple-quoted string literal.";
            spanLength = Math.Max(source.Length - quoteStart, 3);
            return true;
        }

        var lineEnd = contentStart;
        while (lineEnd < source.Length && source[lineEnd] is not '\r' and not '\n')
        {
            lineEnd++;
        }

        for (var i = contentStart; i < lineEnd; i++)
        {
            if (source[i] == quote)
            {
                return false;
            }

            if (source[i] != '\\')
            {
                continue;
            }

            if (i + 1 >= lineEnd)
            {
                message = lineEnd >= source.Length
                    ? "Unfinished string escape at end of file."
                    : "Unfinished string escape at end of line.";
                spanStart = i;
                spanLength = 1;
                return true;
            }

            i++;
        }

        message = quote == '"'
            ? "Unterminated double-quoted string literal."
            : "Unterminated single-quoted string literal.";
        spanLength = Math.Max(lineEnd - quoteStart, 1);
        return true;
    }

    private static bool IsQuote(char c) => c is '"' or '\'';

    private static string FormatInvalidCharacter(char c)
        => c switch
        {
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            _ => c.ToString()
        };

    private static bool TryCreateLeadingIndentationDiagnostic(string source, [MaybeNullWhen(false)] out LythonDiagnostic diagnostic)
    {
        var line = 1;
        var lineStart = 0;

        while (lineStart < source.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < source.Length && source[lineEnd] is not '\r' and not '\n')
            {
                lineEnd++;
            }

            var firstNonWhitespace = lineStart;
            while (firstNonWhitespace < lineEnd &&
                (source[firstNonWhitespace] == ' ' || source[firstNonWhitespace] == '\t'))
            {
                firstNonWhitespace++;
            }

            if (firstNonWhitespace < lineEnd && source[firstNonWhitespace] != '#')
            {
                if (firstNonWhitespace > lineStart)
                {
                    diagnostic = new LythonDiagnostic(
                        Code: "LA1000",
                        Message: "Unexpected indentation.",
                        Severity: LythonDiagnosticSeverity.Error,
                        Span: new LythonSourceSpan(lineStart, firstNonWhitespace - lineStart, line, 1));
                    return true;
                }

                break;
            }

            if (lineEnd < source.Length && source[lineEnd] == '\r' && lineEnd + 1 < source.Length && source[lineEnd + 1] == '\n')
            {
                lineStart = lineEnd + 2;
            }
            else
            {
                lineStart = lineEnd + 1;
            }

            line++;
        }

        diagnostic = null;
        return false;
    }
}
