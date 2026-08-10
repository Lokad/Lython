using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ShlexModule : PyModule
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
                "shlex" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexClass, CreateLexer, CreateLexerAsync),
                "quote" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexQuote, Quote),
                "join" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexJoin, Join),
                "split" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexSplit, Split),
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
    }
}
