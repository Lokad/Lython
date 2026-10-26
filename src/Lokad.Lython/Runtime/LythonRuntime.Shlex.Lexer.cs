using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ShlexModule : PyModule
    {
        private sealed partial class ShlexLexer : IPyMutableDynamicAttributes, IPyAsyncIteratorValue, IPyRenderableValue
        {
            private const string BasicWordChars = "abcdfeghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
            private const string PosixWordChars = "ßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýþÿÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞ";

            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _creationSpan;
            private readonly LinkedList<object> _pushback = [];
            private readonly Stack<int> _pushbackChars = [];
            private readonly LinkedList<SourceSnapshot> _sourceStack = [];
            private PyString _input;
            private int _index;
            // Quote and escape states retain their triggering symbol because both sets are mutable Python attributes.
            private LexerState _state = LexerState.Whitespace;
            private object _instream;
            private object _infile;
            private object _eof;
            private object _source = PyNone.Instance;

            public ShlexLexer(
                PyString input,
                object instream,
                object infile,
                object posix,
                ExecutionContext context,
                LythonSourceSpan creationSpan)
            {
                _input = input;
                _instream = instream;
                _infile = infile;
                Posix = posix;
                _context = context;
                _creationSpan = creationSpan;
                _eof = PyString.Empty;
                WordChars = PyString.FromString(BasicWordChars);
            }

            public async ValueTask InitializeAsync(object punctuationChars, LythonSourceSpan span, bool asynchronous)
            {
                _eof = await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) ? PyNone.Instance : PyString.Empty;
                var extended = await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false);
                WordChars = PyString.FromString(BasicWordChars + (extended ? PosixWordChars : string.Empty));
                if (!await TruthAsync(punctuationChars, span, asynchronous).ConfigureAwait(false)) PunctuationChars = PyString.Empty;
                else if (punctuationChars is true) PunctuationChars = PyString.FromString("();<>|&");
                else if (PyStringOps.TryAsString(punctuationChars, out var text)) PunctuationChars = text;
                else throw new LythonRuntimeException("TypeError", "shlex.shlex(..., punctuation_chars=...) expects a bool or string.", span);
                if (!PunctuationChars.Utf8Bytes.IsEmpty)
                {
                    var builder = new GovernedByteBuilder(_context.MemoryGovernor, span);
                    try
                    {
                        foreach (var ch in (BasicWordChars + (extended ? PosixWordChars : string.Empty) + "~-./*?=").EnumerateRunes())
                        {
                            _context.CheckExecution(span);
                            if (!Contains(PunctuationChars, ch.Value)) builder.Append(ch);
                        }
                        WordChars = builder.ToPyStringAndRelease();
                    }
                    finally { builder.Release(); }
                }
            }

            public object Posix { get; private set; }

            public PyString Commenters { get; set; } = PyString.FromString("#");

            public PyString WordChars { get; private set; }

            public PyString Whitespace { get; private set; } = PyString.FromString(" \t\r\n");

            public object WhitespaceSplit { get; set; } = false;

            public PyString Quotes { get; private set; } = PyString.FromString("'\"");

            public PyString Escape { get; private set; } = PyString.FromString("\\");

            public PyString EscapedQuotes { get; private set; } = PyString.FromString("\"");

            public PyString PunctuationChars { get; private set; } = PyString.Empty;

            public int LineNumber { get; private set; } = 1;

            public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

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

            public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

            public async ValueTask<PyIterationResult> TryMoveNextAsync()
            {
                var token = await GetTokenCoreAsync(_creationSpan, true).ConfigureAwait(false);
                return AreEqual(token, _eof) ? PyIterationResult.End : PyIterationResult.Yield(token);
            }

            private object NextToken(LythonSourceSpan span) => NextTokenAsync(span, false).GetAwaiter().GetResult();

            private async ValueTask<object> NextTokenAsync(LythonSourceSpan span, bool asynchronous)
            {
                var token = await GetTokenCoreAsync(span, asynchronous).ConfigureAwait(false);
                if (AreEqual(token, _eof)) throw new LythonRuntimeException("StopIteration", "", span);
                return token;
            }

            public ValueTask<bool> TruthAsync(object value, LythonSourceSpan span, bool asynchronous)
                => asynchronous ? IsTruthyAsync(value, _context, span) : ValueTask.FromResult(IsTruthy(value, _context, span));

            private async ValueTask<bool> HasTokenAsync(TokenReadState token, LythonSourceSpan span, bool asynchronous)
                => token.Text.Length > 0 || (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && token.Quoted);

            private object GetToken(LythonSourceSpan span) => GetTokenCoreAsync(span, false).GetAwaiter().GetResult();

            private async ValueTask<object> GetTokenCoreAsync(LythonSourceSpan span, bool asynchronous)
            {
                _context.CheckExecution(span);
                if (_pushback.First is { } pushed)
                {
                    _pushback.RemoveFirst();
                    _context.MemoryGovernor.Release(64);
                    return pushed.Value;
                }

                while (true)
                {
                    var raw = await ReadTokenCoreAsync(span, asynchronous).ConfigureAwait(false);
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

            private object ReadToken(LythonSourceSpan span) => ReadTokenCoreAsync(span, false).GetAwaiter().GetResult();

            private async ValueTask<object> ReadTokenCoreAsync(LythonSourceSpan span, bool asynchronous)
            {
                var token = new TokenReadState(_context.MemoryGovernor, span);

                try
                {
                    // Classification deliberately consults the public character sets on
                    // every transition: unlike punctuation_chars, Python lets scripts
                    // mutate whitespace, quotes, escapes, commenters, and wordchars.
                    while (true)
                    {
                        _context.CheckExecution(span);
                        var next = ReadSymbol();
                        var atEnd = next < 0;

                        if (_state.Kind == LexerStateKind.End)
                        {
                            return await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) ? PyNone.Instance : PyString.Empty;
                        }

                        var transition = _state.Kind switch
                        {
                            LexerStateKind.Whitespace => await ReadWhitespaceStateAsync(next, atEnd, token, span, asynchronous).ConfigureAwait(false),
                            LexerStateKind.Quote => await ReadQuoteStateAsync(next, atEnd, token, span, asynchronous).ConfigureAwait(false),
                            LexerStateKind.Escape => ReadEscapeState(next, atEnd, token, span),
                            LexerStateKind.Word or LexerStateKind.Punctuation => await ReadWordStateAsync(next, atEnd, token, span, asynchronous).ConfigureAwait(false),
                            _ => throw new InvalidOperationException($"Unknown shlex lexer state: {_state.Kind}")
                        };

                        if (transition == TokenTransition.Finish)
                        {
                            break;
                        }
                    }

                    if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && !token.Quoted && token.Text.Length == 0)
                    {
                        return PyNone.Instance;
                    }

                    return token.Text.ToPyStringAndRelease();
                }
                finally { token.Text.Release(); }
            }

            private async ValueTask<TokenTransition> ReadWhitespaceStateAsync(int next, bool atEnd, TokenReadState token, LythonSourceSpan span, bool asynchronous)
            {
                if (atEnd)
                {
                    _state = LexerState.End;
                    return TokenTransition.Finish;
                }

                if (Contains(Whitespace, next))
                {
                    return await HasTokenAsync(token, span, asynchronous).ConfigureAwait(false) ? TokenTransition.Finish : TokenTransition.Continue;
                }

                if (Contains(Commenters, next))
                {
                    SkipComment(span);
                }
                else if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && Contains(Escape, next))
                {
                    token.EscapedState = LexerState.Word;
                    _state = LexerState.Escape(next);
                }
                else if (Contains(WordChars, next))
                {
                    token.Text.Append(new Rune(next));
                    _state = LexerState.Word;
                }
                else if (Contains(PunctuationChars, next))
                {
                    token.Text.Append(new Rune(next));
                    _state = LexerState.Punctuation;
                }
                else if (Contains(Quotes, next))
                {
                    if (!await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false))
                    {
                        token.Text.Append(new Rune(next));
                    }

                    _state = LexerState.Quote(next);
                }
                else if (await TruthAsync(WhitespaceSplit, span, asynchronous).ConfigureAwait(false))
                {
                    token.Text.Append(new Rune(next));
                    _state = LexerState.Word;
                }
                else
                {
                    token.Text.Append(new Rune(next));
                    return TokenTransition.Finish;
                }

                return TokenTransition.Continue;
            }

            private async ValueTask<TokenTransition> ReadQuoteStateAsync(int next, bool atEnd, TokenReadState token, LythonSourceSpan span, bool asynchronous)
            {
                token.Quoted = true;
                if (atEnd)
                {
                    throw new LythonRuntimeException("ValueError", "No closing quotation", span);
                }

                if (next == _state.Symbol)
                {
                    if (!await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false))
                    {
                        token.Text.Append(new Rune(next));
                        _state = LexerState.Whitespace;
                        return TokenTransition.Finish;
                    }

                    _state = LexerState.Word;
                }
                else if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && Contains(Escape, next) && Contains(EscapedQuotes, _state.Symbol))
                {
                    token.EscapedState = _state;
                    _state = LexerState.Escape(next);
                }
                else
                {
                    token.Text.Append(new Rune(next));
                }

                return TokenTransition.Continue;
            }

            private TokenTransition ReadEscapeState(int next, bool atEnd, TokenReadState token, LythonSourceSpan span)
            {
                if (atEnd)
                {
                    throw new LythonRuntimeException("ValueError", "No escaped character", span);
                }

                if (token.EscapedState.Kind == LexerStateKind.Quote &&
                    next != _state.Symbol &&
                    next != token.EscapedState.Symbol)
                {
                    token.Text.Append(new Rune(_state.Symbol));
                }

                token.Text.Append(new Rune(next));
                _state = token.EscapedState;
                return TokenTransition.Continue;
            }

            private async ValueTask<TokenTransition> ReadWordStateAsync(int next, bool atEnd, TokenReadState token, LythonSourceSpan span, bool asynchronous)
            {
                if (atEnd)
                {
                    _state = LexerState.End;
                    return TokenTransition.Finish;
                }

                if (Contains(Whitespace, next))
                {
                    _state = LexerState.Whitespace;
                    return await HasTokenAsync(token, span, asynchronous).ConfigureAwait(false) ? TokenTransition.Finish : TokenTransition.Continue;
                }

                if (Contains(Commenters, next))
                {
                    SkipComment(span);
                    if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false))
                    {
                        _state = LexerState.Whitespace;
                        return await HasTokenAsync(token, span, asynchronous).ConfigureAwait(false) ? TokenTransition.Finish : TokenTransition.Continue;
                    }

                    return TokenTransition.Continue;
                }

                if (_state.Kind == LexerStateKind.Punctuation)
                {
                    if (Contains(PunctuationChars, next))
                    {
                        token.Text.Append(new Rune(next));
                        return TokenTransition.Continue;
                    }

                    if (!Contains(Whitespace, next))
                    {
                        _pushbackChars.Push(next);
                    }

                    _state = LexerState.Whitespace;
                    return TokenTransition.Finish;
                }

                if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && Contains(Quotes, next))
                {
                    _state = LexerState.Quote(next);
                    return TokenTransition.Continue;
                }

                if (await TruthAsync(Posix, span, asynchronous).ConfigureAwait(false) && Contains(Escape, next))
                {
                    token.EscapedState = LexerState.Word;
                    _state = LexerState.Escape(next);
                    return TokenTransition.Continue;
                }

                if (Contains(WordChars, next) || Contains(Quotes, next) || await TruthAsync(WhitespaceSplit, span, asynchronous).ConfigureAwait(false) && !Contains(PunctuationChars, next))
                {
                    token.Text.Append(new Rune(next));
                    return TokenTransition.Continue;
                }

                // Punctuation mode needs character pushback so the next token can
                // reconsider it under mutable sets; otherwise CPython pushes a token.
                if (!PunctuationChars.Utf8Bytes.IsEmpty)
                {
                    _pushbackChars.Push(next);
                }
                else
                {
                    PushToken(CreateToken(next, span), span);
                }

                _state = LexerState.Whitespace;
                return await HasTokenAsync(token, span, asynchronous).ConfigureAwait(false) ? TokenTransition.Finish : TokenTransition.Continue;
            }

            private object PushSource(object[] arguments, ExecutionContext context, LythonSourceSpan span)
            {
                using var storage = context.MemoryGovernor.ReserveTemporary(96, span);
                var source = ReadExplicitLexerSource(
                    arguments[0],
                    arguments.Length >= 2 ? arguments[1] : PyNone.Instance,
                    context,
                    span);
                InstallSource(source, storage);
                return PyNone.Instance;
            }

            private async ValueTask<object> PushSourceAsync(object[] arguments, ExecutionContext context, LythonSourceSpan span)
            {
                using var storage = context.MemoryGovernor.ReserveTemporary(96, span);
                var source = await ReadExplicitLexerSourceAsync(
                    arguments[0],
                    arguments.Length >= 2 ? arguments[1] : PyNone.Instance,
                    context,
                    span).ConfigureAwait(false);
                InstallSource(source, storage);
                return PyNone.Instance;
            }

            private void InstallSource(LexerSource source, MemoryGovernor.TemporaryMemoryReservation storage)
            {
                _sourceStack.AddFirst(new SourceSnapshot(_input, _index, _instream, _infile, LineNumber));
                storage.Commit(96);
                _input = source.Text;
                _index = 0;
                _instream = source.Stream;
                _infile = source.Infile;
                LineNumber = 1;
            }

            private void PopSource(LythonSourceSpan span)
            {
                if (_sourceStack.First is not { } saved)
                {
                    throw new LythonRuntimeException("IndexError", "pop from an empty source stack", span);
                }

                var source = saved.Value;
                _sourceStack.RemoveFirst();
                try
                {
                    switch (_instream)
                    {
                        case ExecutionContext.TextFileHandle textFile:
                            _ = textFile.Exit();
                            break;
                        case GzipTextFile gzipFile:
                            gzipFile.CloseFromRuntimeAsync(_context, span, false).GetAwaiter().GetResult();
                            break;
                    }

                    _input = source.Input;
                    _index = source.Index;
                    _instream = source.Stream;
                    _infile = source.Infile;
                    LineNumber = source.LineNumber;
                    _state = LexerState.Whitespace;
                }
                finally { _context.MemoryGovernor.Release(96); }
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

            private sealed class TokenReadState
            {
                public TokenReadState(MemoryGovernor governor, LythonSourceSpan span)
                {
                    Text = new GovernedByteBuilder(governor, span);
                    Quoted = false;
                    EscapedState = LexerState.Whitespace;
                }

                public GovernedByteBuilder Text { get; }

                public bool Quoted { get; set; }

                public LexerState EscapedState { get; set; }

                }

            private readonly record struct LexerState(LexerStateKind Kind, int Symbol)
            {
                public static LexerState Whitespace => new(LexerStateKind.Whitespace, -1);

                public static LexerState Word => new(LexerStateKind.Word, -1);

                public static LexerState Punctuation => new(LexerStateKind.Punctuation, -1);

                public static LexerState End => new(LexerStateKind.End, -1);

                public static LexerState Quote(int symbol) => new(LexerStateKind.Quote, symbol);

                public static LexerState Escape(int symbol) => new(LexerStateKind.Escape, symbol);
            }

            private int ReadSymbol()
            {
                int next;
                if (_pushbackChars.TryPop(out var pushed)) next = pushed;
                else if (_index >= _input.Utf8Bytes.Length) return -1;
                else
                {
                    Rune.DecodeFromUtf8(_input.Utf8Bytes.Span[_index..], out var rune, out var consumed);
                    next = rune.Value;
                    _index += consumed;
                }
                if (next == '\n') LineNumber++;
                return next;
            }

            private void SkipComment(LythonSourceSpan span)
            {
                var bytes = _input.Utf8Bytes.Span;
                while (_index < bytes.Length)
                {
                    if ((_index & 1023) == 0) _context.CheckExecution(span);
                    if (bytes[_index++] == (byte)'\n') break;
                }
                LineNumber++;
            }

            private PyString CreateToken(int value, LythonSourceSpan span)
            {
                Span<byte> bytes = stackalloc byte[4];
                var length = new Rune(value).EncodeToUtf8(bytes);
                return PyString.CopyFromUtf8(bytes[..length], _context.MemoryGovernor, span);
            }

            private void PushToken(object token, LythonSourceSpan span)
            {
                using var storage = _context.MemoryGovernor.ReserveTemporary(64, span);
                _pushback.AddFirst(token);
                storage.Commit(64);
            }

            private static bool Contains(PyString characters, int value)
            {
                if (value < 0) return false;
                if (value < 128) return characters.Utf8Bytes.Span.IndexOf((byte)value) >= 0;
                Span<byte> bytes = stackalloc byte[4];
                var length = new Rune(value).EncodeToUtf8(bytes);
                return characters.Utf8Bytes.Span.IndexOf(bytes[..length]) >= 0;
            }

            private static LythonRuntimeException SourceInclusionUnsupported(LythonSourceSpan span)
                => new(
                    "NotImplementedError",
                    "automatic shlex source inclusion is unsupported; pass an already-authorized string or readable text handle to push_source().",
                    span);

            private sealed record SourceSnapshot(
                PyString Input,
                int Index,
                object Stream,
                object Infile,
                int LineNumber);
        }
    }
}
