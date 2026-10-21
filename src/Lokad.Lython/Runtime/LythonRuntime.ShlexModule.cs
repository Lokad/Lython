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
                "join" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexJoin, Join, JoinAsync),
                "split" => BuiltinCallable.Create(LythonKnownCallableSignatures.ShlexSplit, Split),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Quote(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var value = RequireString(arguments[0], "shlex.quote", span);
            if (IsSafeShellWord(value, span, context)) return value;
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                AppendQuotedWord(builder, value, span, context);
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        private static object Join(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => JoinCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> JoinAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => JoinCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> JoinCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var first = true;
            try
            {
                if (asynchronous)
                {
                    await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false)) AppendWord(item);
                }
                else
                {
                    foreach (var item in ToSequence(arguments[0], span, context)) AppendWord(item);
                }
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }

            void AppendWord(object item)
            {
                context.CheckExecution(span);
                var value = RequireString(item, "shlex.join", span);
                if (!first) builder.Append((byte)' ');
                AppendQuotedWord(builder, value, span, context);
                first = false;
            }
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
            var lexer = new ShlexLexer(input, arguments[0], PyNone.Instance, posix, PyString.Empty, context, span)
            {
                WhitespaceSplit = true,
                Commenters = comments ? PyString.FromString("#") : PyString.Empty,
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
                : PyString.Empty;
            return new LexerOptions(instream, infile, posix, punctuationChars);
        }

        private static PyString ParsePunctuationChars(object value, LythonSourceSpan span)
        {
            if (!IsTruthy(value))
            {
                return PyString.Empty;
            }

            if (value is bool boolean)
            {
                return boolean ? PyString.FromString("();<>|&") : PyString.Empty;
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "shlex.shlex(..., punctuation_chars=...) expects a bool or string.",
                    span);
            }

            return text;
        }

        private static LexerSource ReadInitialLexerSource(
            object instream,
            object infile,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (instream is PyNone)
            {
                return new LexerSource(context.State.Stdin.ReadAll(span), context.State.Stdin, PyNone.Instance);
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
                return new LexerSource(text, context.State.Stdin, PyNone.Instance);
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
                _ => throw new LythonRuntimeException("TypeError", "shlex.shlex input must be a string or a supported readable text handle.", span),
            };
            context.CheckExecution(span);
            return new LexerSource(text, instream, infile);
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
                context.CheckExecution(span);
                return new LexerSource(text, instream, infile);
            }

            return ReadExplicitLexerSource(instream, infile, context, span);
        }

        private sealed record LexerOptions(object Instream, object Infile, bool Posix, PyString PunctuationChars);

        private sealed record LexerSource(PyString Text, object Stream, object Infile);

        private static PyString RequireString(object value, string callable, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{callable}() argument must be str", span);
            }

            return text;
        }

        private static bool IsSafeShellWord(PyString value, LythonSourceSpan span, ExecutionContext context)
        {
            var bytes = value.Utf8Bytes.Span;
            if (bytes.IsEmpty) return false;
            for (var i = 0; i < bytes.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (!IsSafeShellWordCharacter(bytes[i])) return false;
            }
            return true;
        }

        private static void AppendQuotedWord(GovernedByteBuilder builder, PyString value, LythonSourceSpan span, ExecutionContext context)
        {
            if (IsSafeShellWord(value, span, context))
            {
                builder.Append(value);
                return;
            }
            builder.Append((byte)'\'');
            var bytes = value.Utf8Bytes.Span;
            for (var i = 0; i < bytes.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (bytes[i] == (byte)'\'') builder.AppendAscii("'\"'\"'");
                else builder.Append(bytes[i]);
            }
            builder.Append((byte)'\'');
        }

        private static bool IsSafeShellWordCharacter(byte value)
            => value is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or
                (byte)'_' or (byte)'@' or (byte)'%' or (byte)'+' or (byte)'=' or (byte)':' or (byte)',' or (byte)'.' or (byte)'/' or (byte)'-';
    }
}
