using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ShlexModule : PyModule
    {
        public static readonly ShlexModule Instance = new();

        private static readonly string[] Members = ["quote", "join", "split"];

        private ShlexModule() : base("shlex")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "quote" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexQuote, Quote),
                "join" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexJoin, Join),
                "split" => new BuiltinCallable(LythonKnownCallableSignatures.ShlexSplit, Split),
                _ => null!,
            };

            return value is not null;
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
            var tokens = SplitShellWords(input, comments, posix, context, span)
                .Select(token => (object)PyString.FromString(token, context.MemoryGovernor, span));
            return new PyList(tokens, context.MemoryGovernor, span);
        }

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

        private static List<string> SplitShellWords(
            string input,
            bool comments,
            bool posix,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var result = new List<string>();
            var index = 0;

            while (index < input.Length)
            {
                context.CheckExecutionBudget(span);
                var token = new StringBuilder();
                var state = ' ';
                var escapedState = ' ';
                var quoted = false;
                var emit = false;

                while (!emit)
                {
                    context.CheckExecutionBudget(span);
                    var atEnd = index >= input.Length;
                    var next = atEnd ? '\0' : input[index++];

                    switch (state)
                    {
                        case ' ':
                            if (atEnd)
                            {
                                emit = true;
                            }
                            else if (IsShellWhitespace(next))
                            {
                                if (token.Length > 0 || posix && quoted)
                                {
                                    emit = true;
                                }
                            }
                            else if (comments && next == '#')
                            {
                                SkipComment(input, ref index);
                            }
                            else if (posix && next == '\\')
                            {
                                escapedState = 'a';
                                state = '\\';
                            }
                            else if (next is '\'' or '"')
                            {
                                if (!posix)
                                {
                                    token.Append(next);
                                }

                                state = next;
                            }
                            else
                            {
                                token.Append(next);
                                state = 'a';
                            }

                            break;

                        case '\'' or '"':
                            quoted = true;
                            if (atEnd)
                            {
                                throw new LythonRuntimeException("ValueError", "No closing quotation", span);
                            }

                            if (next == state)
                            {
                                if (posix)
                                {
                                    state = 'a';
                                }
                                else
                                {
                                    token.Append(next);
                                    state = ' ';
                                    emit = true;
                                }
                            }
                            else if (posix && next == '\\' && state == '"')
                            {
                                escapedState = state;
                                state = '\\';
                            }
                            else
                            {
                                token.Append(next);
                            }

                            break;

                        case '\\':
                            if (atEnd)
                            {
                                throw new LythonRuntimeException("ValueError", "No escaped character", span);
                            }

                            if (escapedState is '\'' or '"' && next != '\\' && next != escapedState)
                            {
                                token.Append('\\');
                            }

                            token.Append(next);
                            state = escapedState;
                            break;

                        default:
                            if (atEnd)
                            {
                                emit = true;
                            }
                            else if (IsShellWhitespace(next))
                            {
                                state = ' ';
                                emit = token.Length > 0 || posix && quoted;
                            }
                            else if (comments && next == '#')
                            {
                                SkipComment(input, ref index);
                                if (posix)
                                {
                                    state = ' ';
                                    emit = token.Length > 0 || quoted;
                                }
                            }
                            else if (posix && next is '\'' or '"')
                            {
                                state = next;
                            }
                            else if (posix && next == '\\')
                            {
                                escapedState = 'a';
                                state = '\\';
                            }
                            else
                            {
                                token.Append(next);
                            }

                            break;
                    }
                }

                if (token.Length > 0 || posix && quoted)
                {
                    result.Add(token.ToString());
                }
            }

            return result;
        }

        private static bool IsShellWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';

        private static void SkipComment(string input, ref int index)
        {
            while (index < input.Length && input[index] != '\n')
            {
                index++;
            }

            if (index < input.Length)
            {
                index++;
            }
        }
    }
}
