using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ShlexModule : PyModule
    {
        public static readonly ShlexModule Instance = new();

        private static readonly string[] Members = ["shlex", "quote", "join", "split"];

        private ShlexModule() : base("shlex")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "shlex" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexClass, CreateLexer, CreateLexerAsync),
                "quote" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexQuote, Quote),
                "join" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexJoin, Join),
                "split" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexSplit, Split),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Quote(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = RequireString(arguments[0], "shlex.quote", span);
            return PyString.FromString(QuoteShellWord(value), context.MemoryGovernor, span);
        }

        private static object Join(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                context.CheckExecutionBudget(span);
                var value = RequireString(item, "shlex.join", span);
                if (!first)
                {
                    builder.Append(' ');
                }

                builder.Append(QuoteShellWord(value));
                first = false;
            }

            return PyString.FromString(builder.ToString(), context.MemoryGovernor, span);
        }

        private static object Split(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments[0] is PyNone)
            {
                throw new LythonRuntimeException("ValueError", "s argument must not be None", span);
            }

            var input = RequireString(arguments[0], "shlex.split", span);
            var comments = arguments.Length >= 2 && IsTruthy(arguments[1]);
            var posix = arguments.Length < 3 || IsTruthy(arguments[2]);
            var lexer = new ShlexLexer(input, arguments[0], PyNone.Instance, posix, string.Empty, context, span)
            {
                WhitespaceSplit = true,
                Commenters = comments ? "#" : string.Empty,
            };
            return new PyList(lexer.Iterate(), context.MemoryGovernor, span);
        }

        private static object CreateLexer(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseLexerOptions(arguments, span);
            var source = ReadInitialLexerSource(options.Instream, options.Infile, context, span);
            return new ShlexLexer(source.Text, source.Stream, source.Infile, options.Posix, options.PunctuationChars, context, span);
        }

        private static async ValueTask<object> CreateLexerAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseLexerOptions(arguments, span);
            var source = await ReadInitialLexerSourceAsync(options.Instream, options.Infile, context, span).ConfigureAwait(false);
            return new ShlexLexer(source.Text, source.Stream, source.Infile, options.Posix, options.PunctuationChars, context, span);
        }

        private static LexerOptions ParseLexerOptions(object[] arguments, LythonSourceSpan span)
        {
            var instream = arguments.Length >= 1 ? arguments[0] : PyNone.Instance;
            var infile = arguments.Length >= 2 ? arguments[1] : PyNone.Instance;
            var posix = arguments.Length >= 3 && IsTruthy(arguments[2]);
            var punctuationChars = arguments.Length >= 4
                ? ParsePunctuationChars(arguments[3], span)
                : string.Empty;
            return new LexerOptions(instream, infile, posix, punctuationChars);
        }

        private static string ParsePunctuationChars(object value, LythonSourceSpan span)
        {
            if (!IsTruthy(value))
            {
                return string.Empty;
            }

            if (value is bool boolean)
            {
                return boolean ? "();<>|&" : string.Empty;
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "shlex.shlex(..., punctuation_chars=...) expects a bool or string.",
                    span);
            }

            return text.AsString();
        }

        private static LexerSource ReadInitialLexerSource(
            object instream,
            object infile,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (instream is PyNone)
            {
                return new LexerSource(context.State.Stdin.ReadAll(span).AsString(), context.State.Stdin, PyNone.Instance);
            }

            return ReadExplicitLexerSource(instream, infile, context, span);
        }

        private static async ValueTask<LexerSource> ReadInitialLexerSourceAsync(
            object instream,
            object infile,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (instream is PyNone)
            {
                var text = await context.State.Stdin.ReadAllAsync(span).ConfigureAwait(false);
                return new LexerSource(text.AsString(), context.State.Stdin, PyNone.Instance);
            }

            return await ReadExplicitLexerSourceAsync(instream, infile, context, span).ConfigureAwait(false);
        }

        private static LexerSource ReadExplicitLexerSource(
            object instream,
            object infile,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var text = instream switch
            {
                PyString value => value,
                string value => PyString.FromString(value),
                ExecutionContext.TextFileHandle handle => handle.Read(),
                HostTextInputHandle handle => handle.ReadAll(span),
                GzipFileHandle handle => handle.ReadRemainingTextForLexer(span),
                _ => throw UnsupportedLexerStream(span),
            };
            context.ObserveString(text, span);
            return new LexerSource(text.AsString(), instream, infile);
        }

        private static async ValueTask<LexerSource> ReadExplicitLexerSourceAsync(
            object instream,
            object infile,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (instream is HostTextInputHandle input)
            {
                var text = await input.ReadAllAsync(span).ConfigureAwait(false);
                context.ObserveString(text, span);
                return new LexerSource(text.AsString(), instream, infile);
            }

            return ReadExplicitLexerSource(instream, infile, context, span);
        }

        private static LythonRuntimeException UnsupportedLexerStream(LythonSourceSpan span)
            => new(
                "TypeError",
                "shlex.shlex input must be a string or a supported readable text handle.",
                span);

        private sealed record LexerOptions(object Instream, object Infile, bool Posix, string PunctuationChars);

        private sealed record LexerSource(string Text, object Stream, object Infile);

        private static string RequireString(object value, string callable, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{callable}() argument must be str", span);
            }

            return text.AsString();
        }

        private static string QuoteShellWord(string value)
        {
            if (value.Length == 0)
            {
                return "''";
            }

            if (value.All(IsSafeShellWordCharacter))
            {
                return value;
            }

            return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
        }

        private static bool IsSafeShellWordCharacter(char value)
            => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or
                '_' or '@' or '%' or '+' or '=' or ':' or ',' or '.' or '/' or '-';

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
            private string? _state = " ";
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

                    if (!AreEqual(raw, _eof) || _sourceStack.Count == 0)
                    {
                        return raw;
                    }

                    PopSource(span);
                }
            }

            private object ReadToken(LythonSourceSpan span)
            {
                var token = new StringBuilder();
                var quoted = false;
                var escapedState = " ";

                while (true)
                {
                    _context.CheckExecutionBudget(span);
                    var next = ReadSymbol();
                    var atEnd = next.Length == 0;

                    if (_state is null)
                    {
                        token.Clear();
                        break;
                    }

                    if (_state == " ")
                    {
                        if (atEnd)
                        {
                            _state = null;
                            break;
                        }

                        if (Contains(Whitespace, next))
                        {
                            if (token.Length > 0 || Posix && quoted)
                            {
                                break;
                            }
                        }
                        else if (Contains(Commenters, next))
                        {
                            SkipComment();
                        }
                        else if (Posix && Contains(Escape, next))
                        {
                            escapedState = "a";
                            _state = next;
                        }
                        else if (Contains(WordChars, next))
                        {
                            token.Append(next);
                            _state = "a";
                        }
                        else if (Contains(PunctuationChars, next))
                        {
                            token.Append(next);
                            _state = "c";
                        }
                        else if (Contains(Quotes, next))
                        {
                            if (!Posix)
                            {
                                token.Append(next);
                            }

                            _state = next;
                        }
                        else if (WhitespaceSplit)
                        {
                            token.Append(next);
                            _state = "a";
                        }
                        else
                        {
                            token.Append(next);
                            if (token.Length > 0 || Posix && quoted)
                            {
                                break;
                            }
                        }
                    }
                    else if (Contains(Quotes, _state))
                    {
                        quoted = true;
                        if (atEnd)
                        {
                            throw new LythonRuntimeException("ValueError", "No closing quotation", span);
                        }

                        if (next == _state)
                        {
                            if (!Posix)
                            {
                                token.Append(next);
                                _state = " ";
                                break;
                            }

                            _state = "a";
                        }
                        else if (Posix && Contains(Escape, next) && Contains(EscapedQuotes, _state))
                        {
                            escapedState = _state;
                            _state = next;
                        }
                        else
                        {
                            token.Append(next);
                        }
                    }
                    else if (Contains(Escape, _state))
                    {
                        if (atEnd)
                        {
                            throw new LythonRuntimeException("ValueError", "No escaped character", span);
                        }

                        if (Contains(Quotes, escapedState) && next != _state && next != escapedState)
                        {
                            token.Append(_state);
                        }

                        token.Append(next);
                        _state = escapedState;
                    }
                    else if (_state is "a" or "c")
                    {
                        if (atEnd)
                        {
                            _state = null;
                            break;
                        }

                        if (Contains(Whitespace, next))
                        {
                            _state = " ";
                            if (token.Length > 0 || Posix && quoted)
                            {
                                break;
                            }
                        }
                        else if (Contains(Commenters, next))
                        {
                            SkipComment();
                            if (Posix)
                            {
                                _state = " ";
                                if (token.Length > 0 || quoted)
                                {
                                    break;
                                }
                            }
                        }
                        else if (_state == "c")
                        {
                            if (Contains(PunctuationChars, next))
                            {
                                token.Append(next);
                            }
                            else
                            {
                                if (!Contains(Whitespace, next))
                                {
                                    _pushbackChars.Push(next);
                                }

                                _state = " ";
                                break;
                            }
                        }
                        else if (Posix && Contains(Quotes, next))
                        {
                            _state = next;
                        }
                        else if (Posix && Contains(Escape, next))
                        {
                            escapedState = "a";
                            _state = next;
                        }
                        else if (Contains(WordChars, next) || Contains(Quotes, next) || WhitespaceSplit && !Contains(PunctuationChars, next))
                        {
                            token.Append(next);
                        }
                        else
                        {
                            if (PunctuationChars.Length > 0)
                            {
                                _pushbackChars.Push(next);
                            }
                            else
                            {
                                _pushback.AddFirst(CreateToken(next, span));
                            }

                            _state = " ";
                            if (token.Length > 0 || Posix && quoted)
                            {
                                break;
                            }
                        }
                    }
                }

                var text = token.ToString();
                if (Posix && !quoted && text.Length == 0)
                {
                    return PyNone.Instance;
                }

                return CreateToken(text, span);
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
                _state = " ";
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
