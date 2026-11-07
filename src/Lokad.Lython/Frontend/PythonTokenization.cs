using System.Globalization;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static class PythonTokenization
{
    public static TokenizedSource Read(string source, CancellationToken cancellationToken = default)
        => new Reader(source, cancellationToken).Read();

    internal static void CheckCancellation(CancellationToken token, int position)
    {
        // String escapes can advance by two or three code units in one step.
        if ((position & 1023) < 4 && token.IsCancellationRequested) throw RuntimeErrors.Cancelled(null);
    }

    private sealed class Reader(string source, CancellationToken cancellationToken)
    {
        private readonly List<SourceToken> _tokens = [];
        private readonly List<int> _newlines = [];
        private readonly List<(long Column, int Alternate)> _indents = [(0, 0)];
        private int _position;
        private int _depth;
        private bool _invalid;
        private LythonDiagnostic? _lexicalDiagnostic;

        public TokenizedSource Read()
        {
            CheckCancellation(cancellationToken, 0);
            var limit = source.Length;
            while (limit > 0 && source[limit - 1] is ' ' or '\t' or '\r' or '\f')
            { CheckCancellation(cancellationToken, limit); limit--; }
            while (_position < limit)
            {
                CheckCancellation(cancellationToken, _position);
                var start = _position;
                var c = source[start];
                if (c is ' ' or '\t' or '\r' or '\f') { _position++; continue; }
                if (c == '#') { SkipComment(); continue; }
                if (c == '\n')
                {
                    _newlines.Add(_position++);
                    if (_depth == 0)
                    {
                        AddEndOfLine(start);
                        if (!ReadIndentation(limit)) break;
                    }
                    continue;
                }
                if (c == '\\' && start + 1 < limit && source[start + 1] == '\n')
                { _newlines.Add(start + 1); _position += 2; continue; }
                if (c is '\'' or '"')
                {
                    ReadString(start, start, formatted: false);
                    continue;
                }
                if (IsNameStart(c))
                {
                    var end = start + 1;
                    while (end < limit && IsNamePart(source[end]))
                    { CheckCancellation(cancellationToken, end); end++; }
                    var name = source.AsSpan(start, end - start);
                    var formatted = name is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
                    if (end < limit && source[end] is '\'' or '"')
                    {
                        if (name is "r" or "R") { ReadString(start, end, formatted: false); continue; }
                        if (formatted)
                        {
                            Add(Token.Identifier, start, end - start);
                            _position = end;
                            ReadString(end, end, formatted: true);
                            continue;
                        }
                    }
                    Add(Keyword(name), start, end - start);
                    _position = end;
                    continue;
                }
                if (IsDigit(c) || c == '.' && start + 1 < limit && IsDigit(source[start + 1]))
                { ReadNumber(); continue; }
                var token = Symbol(c, start + 1 < limit ? source[start + 1] : '\0',
                    start + 2 < limit ? source[start + 2] : '\0', out var length);
                if (token is Token.OpenParen or Token.OpenBracket or Token.OpenBrace)
                {
                    // Reuse lexical depth before any recursive parsing. Strings
                    // and f-string fields are handled by the shared scanner.
                    if (++_depth > LythonEngine.MaxSyntaxNesting)
                    {
                        _lexicalDiagnostic = new("LA0003",
                            $"Syntax nesting exceeds the maximum of {LythonEngine.MaxSyntaxNesting} levels.",
                            LythonDiagnosticSeverity.Error, null);
                        break;
                    }
                }
                else if (token is Token.CloseParen or Token.CloseBracket or Token.CloseBrace && _depth > 0) _depth--;
                Add(token, start, length);
                _position += length;
            }
            var terminal = _depth > 0 && _tokens.Count > 0
                ? _tokens[^1].Start + _tokens[^1].Length : _position;
            if (_depth == 0)
            {
                if (_tokens.Count > 0 && _tokens[^1].Token is not (Token.Eol or Token.Dedent)) Add(Token.Eol, terminal, 0);
                while (_indents.Count > 1) { _indents.RemoveAt(_indents.Count - 1); Add(Token.Dedent, terminal, 0); }
            }
            Add(Token.End, terminal, 0);
            // Trailing whitespace cannot contain a newline, but literals can.
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(null);
            return new(source, _tokens, _newlines, _invalid, _lexicalDiagnostic);
        }

        private void Add(Token token, int start, int length)
        {
            _tokens.Add(new(token, start, length));
            _invalid |= token == Token.Error;
        }

        private void AddEndOfLine(int position)
        {
            if (_tokens.Count > 0 && _tokens[^1].Token is not (Token.Indent or Token.Dedent)) Add(Token.Eol, position, 0);
        }

        private void SkipComment()
        {
            while (_position < source.Length && source[_position] != '\n')
            { CheckCancellation(cancellationToken, _position); _position++; }
        }

        private bool ReadIndentation(int limit)
        {
            long column = 0;
            var alternate = 0;
            while (_position < limit)
            {
                CheckCancellation(cancellationToken, _position);
                switch (source[_position])
                {
                    case ' ': column++; alternate++; break;
                    case '\t': column = (column / 8 + 1) * 8; alternate++; break;
                    case '\f': column = alternate = 0; break;
                    case '\r': break;
                    case '\n': _newlines.Add(_position); column = alternate = 0; break;
                    case '#': SkipComment(); continue;
                    default: goto End;
                }
                _position++;
            }
        End:
            // Blank/comment-only lines never establish indentation, including
            // a final comment without a physical newline.
            if (_position >= limit) return true;
            var current = _indents[^1];
            if (column > current.Column)
            {
                if (alternate <= current.Alternate) return InvalidIndentation(tabs: true);
                _indents.Add((column, alternate));
                Add(Token.Indent, _position, 0);
                return true;
            }
            while (_indents[^1].Column > column)
            { _indents.RemoveAt(_indents.Count - 1); Add(Token.Dedent, _position, 0); }
            if (_indents[^1].Column != column) return InvalidIndentation(tabs: false);
            return alternate == _indents[^1].Alternate || InvalidIndentation(tabs: true);
        }

        private bool InvalidIndentation(bool tabs)
        {
            var location = new TokenizedSource(source, _tokens, _newlines, false);
            location.LineOfPosition(_position, out var line, out var column);
            _lexicalDiagnostic = new("LA0001", tabs
                ? "Inconsistent use of tabs and spaces in indentation."
                : "Unindent does not match any outer indentation level.", LythonDiagnosticSeverity.Error,
                new LythonSourceSpan(_position, 1, line, column));
            return false;
        }

        private void ReadString(int start, int quote, bool formatted)
        {
            if (!FormattedStringLexicalScanner.TryFindStringEnd(source, quote, formatted, out var end, out _, cancellationToken: cancellationToken))
            {
                // Malformed f-strings still expose the ordinary literal token
                // where possible so the parser retains its Python diagnostics.
                if (!formatted || !FormattedStringLexicalScanner.TryFindStringEnd(source, quote, false, out end, out _, cancellationToken: cancellationToken))
                { Add(Token.Error, start, 1); _position = start + 1; return; }
            }
            Add(Token.String, start, end - start);
            for (var i = quote; i < end; i++)
            {
                CheckCancellation(cancellationToken, i);
                if (source[i] == '\n') _newlines.Add(i);
            }
            _position = end;
        }

        private int Digits(int start, int radix = 10)
        {
            if (start >= source.Length || Digit(source[start]) >= radix) return start;
            var end = start + 1;
            while (end < source.Length)
            {
                CheckCancellation(cancellationToken, end);
                if (Digit(source[end]) < radix) end++;
                else if (source[end] == '_' && end + 1 < source.Length && Digit(source[end + 1]) < radix) end += 2;
                else break;
            }
            return end;
        }

        private int Exponent(int start)
        {
            if (start >= source.Length || source[start] is not ('e' or 'E')) return start;
            var digit = start + 1;
            if (digit < source.Length && source[digit] is '+' or '-') digit++;
            var end = Digits(digit);
            return end == digit ? start : end;
        }

        private void ReadNumber()
        {
            var start = _position;
            var end = start;
            var token = Token.Integer;
            var decimalEnd = Digits(start);
            if (decimalEnd > start)
            {
                end = decimalEnd;
                if (source[start] == '0')
                {
                    end = start + 1;
                    while (end < source.Length)
                    {
                        CheckCancellation(cancellationToken, end);
                        if (source[end] == '0') end++;
                        else if (source[end] == '_' && end + 1 < source.Length && source[end + 1] == '0') end += 2;
                        else break;
                    }
                    if (start + 1 < source.Length && source[start + 1] is 'x' or 'X' or 'o' or 'O' or 'b' or 'B')
                    {
                        var radix = source[start + 1] is 'x' or 'X' ? 16 : source[start + 1] is 'o' or 'O' ? 8 : 2;
                        var first = start + 2;
                        if (first < source.Length && source[first] == '_') first++;
                        var prefixed = Digits(first, radix);
                        if (prefixed > first) end = prefixed;
                    }
                }
            }
            var mantissa = decimalEnd;
            var hasPoint = mantissa < source.Length && source[mantissa] == '.';
            if (hasPoint)
            {
                var fraction = Digits(mantissa + 1);
                if (decimalEnd > start || fraction > mantissa + 1) mantissa = fraction;
                else hasPoint = false;
            }
            var realEnd = Exponent(mantissa);
            if (hasPoint || realEnd > mantissa)
            {
                if (realEnd > end) { end = realEnd; token = Token.Float; }
            }
            if (realEnd < source.Length && source[realEnd] is 'j' or 'J' && realEnd > start && realEnd + 1 > end)
            { end = realEnd + 1; token = Token.Imaginary; }
            if (decimalEnd > start)
            {
                var raw = start;
                var doubleUnderscore = false;
                var nonzero = false;
                while (raw < source.Length && (IsDigit(source[raw]) || source[raw] == '_'))
                {
                    CheckCancellation(cancellationToken, raw);
                    nonzero |= source[raw] is >= '1' and <= '9';
                    doubleUnderscore |= raw > start && source[raw] == '_' && source[raw - 1] == '_';
                    raw++;
                }
                if (raw >= end && (doubleUnderscore || source[raw - 1] == '_' || source[start] == '0' && nonzero))
                { end = raw; token = Token.MalformedNumber; }
            }
            Add(token, start, end - start);
            _position = end;
        }
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
    private static int Digit(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : int.MaxValue;
    private static bool IsNameStart(char c) => c == '_' || char.IsLetter(c) || char.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber;
    private static bool IsNamePart(char c) => IsNameStart(c) || char.IsDigit(c) || char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation;

    private static Token Keyword(ReadOnlySpan<char> name) => name switch
    {
        "import" => Token.Import, "def" => Token.Def, "return" => Token.Return, "raise" => Token.Raise,
        "try" => Token.Try, "except" => Token.Except, "finally" => Token.Finally, "as" => Token.As,
        "elif" => Token.Elif, "with" => Token.With, "yield" => Token.Yield, "async" => Token.Async,
        "await" => Token.Await, "lambda" => Token.Lambda, "global" => Token.Global, "nonlocal" => Token.Nonlocal,
        "assert" => Token.Assert, "del" => Token.Del, "from" => Token.From, "match" => Token.Match,
        "case" => Token.Case, "class" => Token.Class, "and" => Token.And, "or" => Token.Or,
        "not" => Token.Not, "is" => Token.Is, "if" => Token.If, "else" => Token.Else,
        "for" => Token.For, "in" => Token.In, "while" => Token.While, "break" => Token.Break,
        "continue" => Token.Continue, "pass" => Token.Pass, "True" => Token.True, "False" => Token.False,
        "None" => Token.None, _ => Token.Identifier,
    };

    private static Token Symbol(char c, char next, char third, out int length)
    {
        length = 1;
        var doubled = (c, next) switch
        {
            ('*', '*') => Token.StarStar, ('/', '/') => Token.SlashSlash,
            ('<', '<') => Token.LessLess, ('>', '>') => Token.GreaterGreater,
            _ => Token.Error
        };
        if (doubled != Token.Error)
        {
            length = third == '=' ? 3 : 2;
            return third != '=' ? doubled : doubled switch
            { Token.StarStar => Token.StarStarEqual, Token.SlashSlash => Token.SlashSlashEqual,
                Token.LessLess => Token.LessLessEqual, _ => Token.GreaterGreaterEqual };
        }
        if (next == '=')
        {
            var compound = c switch
            {
                '=' => Token.EqualEqual, '!' => Token.BangEqual, '<' => Token.LessEqual, '>' => Token.GreaterEqual,
                ':' => Token.ColonEqual, '+' => Token.PlusEqual, '-' => Token.MinusEqual, '*' => Token.StarEqual,
                '/' => Token.SlashEqual, '%' => Token.PercentEqual, '&' => Token.AmpersandEqual,
                '|' => Token.PipeEqual, '^' => Token.CaretEqual, '@' => Token.AtEqual, _ => Token.Error
            };
            if (compound != Token.Error) { length = 2; return compound; }
        }
        if (c == '-' && next == '>') { length = 2; return Token.Arrow; }
        return c switch
        {
            '+' => Token.Plus, '-' => Token.Minus, '*' => Token.Star, '/' => Token.Slash, '%' => Token.Percent,
            '&' => Token.Ampersand, '|' => Token.Pipe, '^' => Token.Caret, '~' => Token.Tilde, '@' => Token.At,
            '<' => Token.Less, '>' => Token.Greater, '=' => Token.Assign, '.' => Token.Dot, ',' => Token.Comma,
            ';' => Token.Semicolon, ':' => Token.Colon, '(' => Token.OpenParen, ')' => Token.CloseParen,
            '[' => Token.OpenBracket, ']' => Token.CloseBracket, '{' => Token.OpenBrace, '}' => Token.CloseBrace,
            _ => Token.Error
        };
    }
}
