using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ShlexModule : PyModule
    {
        private sealed class ShlexLexer : IPyDynamicAttributes, IPyIteratorValue, IPyRenderableValue
        {
            private const string BasicWordChars = "abcdfeghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            private const string PosixWordChars = "ßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞ";

            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _creationSpan;
            private readonly LinkedList<object> _pushback = [];
            private readonly Stack<string> _pushbackChars = [];
            private readonly Stack<SourceSnapshot> _sourceStack = [];
            private string _input;
            private int _index;
            // Quote and escape states retain their triggering symbol because both sets are mutable Python attributes.
            private LexerState _state = LexerState.Whitespace;
            private object _instream;
            private object _infile;
            private object _eof;
            private object _source = PyNone.Instance;

            public ShlexLexer(
                string input,
                object instream,
                object infile,
                bool posix,
                string punctuationChars,
                ExecutionContext context,
                LythonSourceSpan creationSpan)
            {
                _input = input;
                _instream = instream;
                _infile = infile;
                Posix = posix;
                PunctuationChars = punctuationChars;
                _context = context;
                _creationSpan = creationSpan;
                _eof = posix ? PyNone.Instance : PyString.Empty;
                WordChars = BasicWordChars + (posix ? PosixWordChars : string.Empty);
                if (punctuationChars.Length > 0)
                {
                    WordChars += "~-./*?=";
                    WordChars = string.Concat(WordChars.Where(ch => !punctuationChars.Contains(ch)));
                }
            }

            public bool Posix { get; private set; }

            public string Commenters { get; set; } = "#";

            public string WordChars { get; private set; }

            public string Whitespace { get; private set; } = " \t\r\n";

            public bool WhitespaceSplit { get; set; }

            public string Quotes { get; private set; } = "'\"";

            public string Escape { get; private set; } = "\\";

            public string EscapedQuotes { get; private set; } = "\"";

            public string PunctuationChars { get; }

            public int LineNumber { get; private set; } = 1;

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "get_token" => new BoundCallable((arguments, span, _) =>
                    {
                        RequireNoArguments(arguments, "shlex.get_token()", span);
                        return GetToken(span);
                    }, "shlex.get_token", []),
                    "read_token" => new BoundCallable((arguments, span, _) =>
                    {
                        RequireNoArguments(arguments, "shlex.read_token()", span);
                        return ReadToken(span);
                    }, "shlex.read_token", []),
                    "push_token" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "shlex.push_token(tok) expects one argument.", span);
                        }

                        _pushback.AddFirst(arguments[0]);
                        return PyNone.Instance;
                    }, "shlex.push_token", ["tok"]),
                    "sourcehook" => new BoundCallable((_, span, _) => throw SourceInclusionUnsupported(span), "shlex.sourcehook", ["newfile"]),
                    "push_source" => new BoundCallable(
                        (arguments, span, context) => PushSource(arguments, context, span),
                        async (arguments, span, context) => await PushSourceAsync(arguments, context, span).ConfigureAwait(false),
                        "shlex.push_source",
                        ["newstream", "newfile"],
                        1),
                    "pop_source" => new BoundCallable((arguments, span, _) =>
                    {
                        RequireNoArguments(arguments, "shlex.pop_source()", span);
                        PopSource(span);
                        return PyNone.Instance;
                    }, "shlex.pop_source", []),
                    "__next__" => new BoundCallable((arguments, span, _) =>
                    {
                        RequireNoArguments(arguments, "shlex.__next__()", span);
                        var token = GetToken(span);
                        if (AreEqual(token, _eof))
                        {
                            throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
                        }

                        return token;
                    }, "shlex.__next__", []),
                    "lineno" => new BigInteger(LineNumber),
                    "infile" => _infile,
                    "instream" => _instream,
                    "eof" => _eof,
                    "posix" => Posix,
                    "commenters" => PyString.FromString(Commenters),
                    "whitespace" => PyString.FromString(Whitespace),
                    "whitespace_split" => WhitespaceSplit,
                    "quotes" => PyString.FromString(Quotes),
                    "escape" => PyString.FromString(Escape),
                    "escapedquotes" => PyString.FromString(EscapedQuotes),
                    "wordchars" => PyString.FromString(WordChars),
                    "punctuation_chars" => PyString.FromString(PunctuationChars),
                    "source" => _source,
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                switch (name)
                {
                    case "lineno":
                        if (value is not BigInteger line || line < 0 || line > int.MaxValue)
                        {
                            throw new LythonRuntimeException("TypeError", "shlex.lineno must be a non-negative integer.", null);
                        }

                        LineNumber = (int)line;
                        return true;
                    case "infile":
                        _infile = value;
                        return true;
                    case "eof":
                        _eof = value;
                        return true;
                    case "posix":
                        Posix = IsTruthy(value);
                        return true;
                    case "commenters":
                        Commenters = RequireConfigurationString(name, value);
                        return true;
                    case "whitespace":
                        Whitespace = RequireConfigurationString(name, value);
                        return true;
                    case "whitespace_split":
                        WhitespaceSplit = IsTruthy(value);
                        return true;
                    case "quotes":
                        Quotes = RequireConfigurationString(name, value);
                        return true;
                    case "escape":
                        Escape = RequireConfigurationString(name, value);
                        return true;
                    case "escapedquotes":
                        EscapedQuotes = RequireConfigurationString(name, value);
                        return true;
                    case "wordchars":
                        WordChars = RequireConfigurationString(name, value);
                        return true;
                    case "source":
                        _source = value;
                        return true;
                    case "punctuation_chars":
                        throw new LythonRuntimeException(
                            "AttributeError",
                            "property 'punctuation_chars' of 'shlex' object has no setter",
                            null);
                    default:
                        return false;
                }
            }

            public IEnumerable<object> Iterate()
            {
                while (TryMoveNext(out var value))
                {
                    yield return value;
                }
            }

            public bool TryMoveNext([MaybeNullWhen(false)] out object value)
            {
                var token = GetToken(_creationSpan);
                if (AreEqual(token, _eof))
                {
                    value = PyNone.Instance;
                    return false;
                }

                value = token;
                return true;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<shlex.shlex object>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private object GetToken(LythonSourceSpan span)
            {
                _context.CheckExecutionBudget(span);
                if (_pushback.First is { } pushed)
                {
                    _pushback.RemoveFirst();
                    return pushed.Value;
                }

                while (true)
                {
                    var raw = ReadToken(span);
                    if (_source is not PyNone && AreEqual(raw, _source))
                    {
                        throw SourceInclusionUnsupported(span);
                    }

                    // EOF of a pushed source resumes its suspended parent; only the
                    // outermost EOF is observable through get_token()/iteration.
                    if (!AreEqual(raw, _eof) || _sourceStack.Count == 0)
                    {
                        return raw;
                    }

                    PopSource(span);
                }
            }

            private object ReadToken(LythonSourceSpan span)
            {
                var token = new TokenReadState();

                // Classification deliberately consults the public character sets on
                // every transition: unlike punctuation_chars, Python lets scripts
                // mutate whitespace, quotes, escapes, commenters, and wordchars.
                while (true)
                {
                    _context.CheckExecutionBudget(span);
                    var next = ReadSymbol();
                    var atEnd = next.Length == 0;

                    if (_state.Kind == LexerStateKind.End)
                    {
                        token.Text.Clear();
                        break;
                    }

                    var transition = _state.Kind switch
                    {
                        LexerStateKind.Whitespace => ReadWhitespaceState(next, atEnd, ref token),
                        LexerStateKind.Quote => ReadQuoteState(next, atEnd, ref token, span),
                        LexerStateKind.Escape => ReadEscapeState(next, atEnd, ref token, span),
                        LexerStateKind.Word or LexerStateKind.Punctuation => ReadWordState(next, atEnd, ref token, span),
                        _ => throw new InvalidOperationException($"Unknown shlex lexer state: {_state.Kind}")
                    };

                    if (transition == TokenTransition.Finish)
                    {
                        break;
                    }
                }

                var text = token.Text.ToString();
                if (Posix && !token.Quoted && text.Length == 0)
                {
                    return PyNone.Instance;
                }

                return CreateToken(text, span);
            }

            private TokenTransition ReadWhitespaceState(string next, bool atEnd, ref TokenReadState token)
            {
                if (atEnd)
                {
                    _state = LexerState.End;
                    return TokenTransition.Finish;
                }

                if (Contains(Whitespace, next))
                {
                    return token.HasValue(Posix) ? TokenTransition.Finish : TokenTransition.Continue;
                }

                if (Contains(Commenters, next))
                {
                    SkipComment();
                }
                else if (Posix && Contains(Escape, next))
                {
                    token.EscapedState = LexerState.Word;
                    _state = LexerState.Escape(next);
                }
                else if (Contains(WordChars, next))
                {
                    token.Text.Append(next);
                    _state = LexerState.Word;
                }
                else if (Contains(PunctuationChars, next))
                {
                    token.Text.Append(next);
                    _state = LexerState.Punctuation;
                }
                else if (Contains(Quotes, next))
                {
                    if (!Posix)
                    {
                        token.Text.Append(next);
                    }

                    _state = LexerState.Quote(next);
                }
                else if (WhitespaceSplit)
                {
                    token.Text.Append(next);
                    _state = LexerState.Word;
                }
                else
                {
                    token.Text.Append(next);
                    return TokenTransition.Finish;
                }

                return TokenTransition.Continue;
            }

            private TokenTransition ReadQuoteState(string next, bool atEnd, ref TokenReadState token, LythonSourceSpan span)
            {
                token.Quoted = true;
                if (atEnd)
                {
                    throw new LythonRuntimeException("ValueError", "No closing quotation", span);
                }

                if (next == _state.Symbol)
                {
                    if (!Posix)
                    {
                        token.Text.Append(next);
                        _state = LexerState.Whitespace;
                        return TokenTransition.Finish;
                    }

                    _state = LexerState.Word;
                }
                else if (Posix && Contains(Escape, next) && Contains(EscapedQuotes, _state.Symbol))
                {
                    token.EscapedState = _state;
                    _state = LexerState.Escape(next);
                }
                else
                {
                    token.Text.Append(next);
                }

                return TokenTransition.Continue;
            }

            private TokenTransition ReadEscapeState(string next, bool atEnd, ref TokenReadState token, LythonSourceSpan span)
            {
                if (atEnd)
                {
                    throw new LythonRuntimeException("ValueError", "No escaped character", span);
                }

                if (token.EscapedState.Kind == LexerStateKind.Quote &&
                    next != _state.Symbol &&
                    next != token.EscapedState.Symbol)
                {
                    token.Text.Append(_state.Symbol);
                }

                token.Text.Append(next);
                _state = token.EscapedState;
                return TokenTransition.Continue;
            }

            private TokenTransition ReadWordState(string next, bool atEnd, ref TokenReadState token, LythonSourceSpan span)
            {
                if (atEnd)
                {
                    _state = LexerState.End;
                    return TokenTransition.Finish;
                }

                if (Contains(Whitespace, next))
                {
                    _state = LexerState.Whitespace;
                    return token.HasValue(Posix) ? TokenTransition.Finish : TokenTransition.Continue;
                }

                if (Contains(Commenters, next))
                {
                    SkipComment();
                    if (Posix)
                    {
                        _state = LexerState.Whitespace;
                        return token.HasValue(Posix) ? TokenTransition.Finish : TokenTransition.Continue;
                    }

                    return TokenTransition.Continue;
                }

                if (_state.Kind == LexerStateKind.Punctuation)
                {
                    if (Contains(PunctuationChars, next))
                    {
                        token.Text.Append(next);
                        return TokenTransition.Continue;
                    }

                    if (!Contains(Whitespace, next))
                    {
                        _pushbackChars.Push(next);
                    }

                    _state = LexerState.Whitespace;
                    return TokenTransition.Finish;
                }

                if (Posix && Contains(Quotes, next))
                {
                    _state = LexerState.Quote(next);
                    return TokenTransition.Continue;
                }

                if (Posix && Contains(Escape, next))
                {
                    token.EscapedState = LexerState.Word;
                    _state = LexerState.Escape(next);
                    return TokenTransition.Continue;
                }

                if (Contains(WordChars, next) || Contains(Quotes, next) || WhitespaceSplit && !Contains(PunctuationChars, next))
                {
                    token.Text.Append(next);
                    return TokenTransition.Continue;
                }

                // Punctuation mode needs character pushback so the next token can
                // reconsider it under mutable sets; otherwise CPython pushes a token.
                if (PunctuationChars.Length > 0)
                {
                    _pushbackChars.Push(next);
                }
                else
                {
                    _pushback.AddFirst(CreateToken(next, span));
                }

                _state = LexerState.Whitespace;
                return token.HasValue(Posix) ? TokenTransition.Finish : TokenTransition.Continue;
            }

            private object PushSource(object[] arguments, ExecutionContext context, LythonSourceSpan span)
            {
                var source = ReadExplicitLexerSource(
                    arguments[0],
                    arguments.Length >= 2 ? arguments[1] : PyNone.Instance,
                    context,
                    span);
                InstallSource(source);
                return PyNone.Instance;
            }

            private async ValueTask<object> PushSourceAsync(object[] arguments, ExecutionContext context, LythonSourceSpan span)
            {
                var source = await ReadExplicitLexerSourceAsync(
                    arguments[0],
                    arguments.Length >= 2 ? arguments[1] : PyNone.Instance,
                    context,
                    span).ConfigureAwait(false);
                InstallSource(source);
                return PyNone.Instance;
            }

            private void InstallSource(LexerSource source)
            {
                _sourceStack.Push(new SourceSnapshot(_input, _index, _instream, _infile, LineNumber));
                _input = source.Text;
                _index = 0;
                _instream = source.Stream;
                _infile = source.Infile;
                LineNumber = 1;
            }

            private void PopSource(LythonSourceSpan span)
            {
                if (!_sourceStack.TryPop(out var source))
                {
                    throw new LythonRuntimeException("IndexError", "pop from an empty source stack", span);
                }

                switch (_instream)
                {
                    case ExecutionContext.TextFileHandle textFile:
                        _ = textFile.Exit();
                        break;
                    case GzipFileHandle gzipFile:
                        gzipFile.ClosePushedLexerSource(span);
                        break;
                }

                _input = source.Input;
                _index = source.Index;
                _instream = source.Stream;
                _infile = source.Infile;
                LineNumber = source.LineNumber;
                _state = LexerState.Whitespace;
            }

            private enum LexerStateKind
            {
                Whitespace,
                Word,
                Punctuation,
                Quote,
                Escape,
                End
            }

            private enum TokenTransition
            {
                Continue,
                Finish,
            }

            private ref struct TokenReadState
            {
                public TokenReadState()
                {
                    Text = new StringBuilder();
                    Quoted = false;
                    EscapedState = LexerState.Whitespace;
                }

                public StringBuilder Text { get; }

                public bool Quoted { get; set; }

                public LexerState EscapedState { get; set; }

                public bool HasValue(bool posix) => Text.Length > 0 || posix && Quoted;
            }

            private readonly record struct LexerState(LexerStateKind Kind, string Symbol)
            {
                public static LexerState Whitespace => new(LexerStateKind.Whitespace, string.Empty);

                public static LexerState Word => new(LexerStateKind.Word, string.Empty);

                public static LexerState Punctuation => new(LexerStateKind.Punctuation, string.Empty);

                public static LexerState End => new(LexerStateKind.End, string.Empty);

                public static LexerState Quote(string symbol) => new(LexerStateKind.Quote, symbol);

                public static LexerState Escape(string symbol) => new(LexerStateKind.Escape, symbol);
            }

            private string ReadSymbol()
            {
                string next;
                if (_pushbackChars.TryPop(out var pushed))
                {
                    next = pushed;
                }
                else if (_index >= _input.Length)
                {
                    return string.Empty;
                }
                else
                {
                    // shlex operates on Python characters, not UTF-16 code units.
                    var rune = Rune.GetRuneAt(_input, _index);
                    next = rune.ToString();
                    _index += rune.Utf16SequenceLength;
                }

                if (next == "\n")
                {
                    LineNumber++;
                }

                return next;
            }

            private void SkipComment()
            {
                while (_index < _input.Length)
                {
                    var rune = Rune.GetRuneAt(_input, _index);
                    _index += rune.Utf16SequenceLength;
                    if (rune.Value == '\n')
                    {
                        break;
                    }
                }

                LineNumber++;
            }

            private PyString CreateToken(string value, LythonSourceSpan span)
                => PyString.FromString(value, _context.MemoryGovernor, span);

            private static bool Contains(string characters, string value)
                => characters.Contains(value, StringComparison.Ordinal);

            private static string RequireConfigurationString(string name, object value)
            {
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", $"shlex.{name} must be a string.", null);
                }

                return text.AsString();
            }

            private static void RequireNoArguments(object[] arguments, string signature, LythonSourceSpan span)
            {
                if (arguments.Length != 0)
                {
                    throw new LythonRuntimeException("TypeError", $"{signature} expects no arguments.", span);
                }
            }

            private static LythonRuntimeException SourceInclusionUnsupported(LythonSourceSpan span)
                => new(
                    "NotImplementedError",
                    "automatic shlex source inclusion is unsupported; pass an already-authorized string or readable text handle to push_source().",
                    span);

            private sealed record SourceSnapshot(
                string Input,
                int Index,
                object Stream,
                object Infile,
                int LineNumber);
        }
    }
}
