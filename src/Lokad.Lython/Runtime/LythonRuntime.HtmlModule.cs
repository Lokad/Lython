using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class HtmlModule : PyModule
    {
        internal static readonly HtmlModule Instance = new();
        private HtmlModule() : base("html") { }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "escape" => BuiltinCallable.Create(LythonKnownCallableSignatures.HtmlEscape,
                    (args, span, context) => EscapeAsync(args, span, context, false).GetAwaiter().GetResult(),
                    (args, span, context) => EscapeAsync(args, span, context, true)),
                "unescape" => BuiltinCallable.Create(LythonKnownCallableSignatures.HtmlUnescape,
                    (args, span, context) => UnescapeAsync(args, span, context, false).GetAwaiter().GetResult(),
                    (args, span, context) => UnescapeAsync(args, span, context, true)),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static readonly PyString Ampersand = PyString.FromString("&");
        private static readonly PyString LessThan = PyString.FromString("<");
        private static readonly PyString GreaterThan = PyString.FromString(">");
        private static readonly PyString DoubleQuote = PyString.FromString("\"");
        private static readonly PyString Apostrophe = PyString.FromString("'");
        private static readonly PyString AmpEntity = PyString.FromString("&amp;");
        private static readonly PyString LessEntity = PyString.FromString("&lt;");
        private static readonly PyString GreaterEntity = PyString.FromString("&gt;");
        private static readonly PyString QuoteEntity = PyString.FromString("&quot;");
        private static readonly PyString ApostropheEntity = PyString.FromString("&#x27;");

        private static async ValueTask<object> EscapeAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var quote = args.Length == 2 ? args[1] : true;
            if (args[0] is PyString text && quote is bool boolean)
                return EscapeText(text, boolean, span, context.Services);

            // Guest replace results become the receiver for the next call.
            // Quote truth testing happens after the first three replacements.
            var result = await ReplaceAsync(args[0], Ampersand, EscapeMask.Ampersand, span, context, asynchronous).ConfigureAwait(false);
            result = await ReplaceAsync(result, LessThan, EscapeMask.LessThan, span, context, asynchronous).ConfigureAwait(false);
            result = await ReplaceAsync(result, GreaterThan, EscapeMask.GreaterThan, span, context, asynchronous).ConfigureAwait(false);
            var includeQuotes = asynchronous
                ? await IsTruthyAsync(quote, context, span).ConfigureAwait(false)
                : IsTruthy(quote, context, span);
            context.Services.CheckExecutionBudget(span);
            if (includeQuotes)
            {
                result = await ReplaceAsync(result, DoubleQuote, EscapeMask.DoubleQuote, span, context, asynchronous).ConfigureAwait(false);
                result = await ReplaceAsync(result, Apostrophe, EscapeMask.Apostrophe, span, context, asynchronous).ConfigureAwait(false);
            }
            return result;
        }

        private static async ValueTask<object> ReplaceAsync(object target, PyString oldText, EscapeMask mask,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (target is PyString text) return EscapeCharacters(text, mask, span, context.Services);
            var member = asynchronous
                ? await PyTextStream.ResolveMemberAsync(target, "replace", span, context).ConfigureAwait(false)
                : PyTextStream.ResolveMember(target, "replace", span, context);
            var replacement = Replacement(oldText.Utf8Bytes.Span[0], mask)!;
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(oldText), CallArgumentValue.Positional(replacement)];
            var result = asynchronous
                ? await InvokeCallableTargetAsync(member, span, span, context,
                    () => ValueTask.FromResult(arguments)).ConfigureAwait(false)
                : InvokeCallableTarget(member, span, span, context, () => arguments);
            context.Services.State.CallTemporaries.TrackCallResult(result, span);
            context.Services.CheckExecutionBudget(span);
            return result;
        }

        [Flags]
        private enum EscapeMask
        {
            Ampersand = 1, LessThan = 2, GreaterThan = 4, DoubleQuote = 8, Apostrophe = 16,
            Basic = Ampersand | LessThan | GreaterThan,
            All = Basic | DoubleQuote | Apostrophe,
        }

        internal static PyString EscapeText(PyString text, bool quote, LythonSourceSpan span, ExecutionServices services)
            => EscapeCharacters(text, quote ? EscapeMask.All : EscapeMask.Basic, span, services);

        private static PyString EscapeCharacters(PyString text, EscapeMask mask, LythonSourceSpan span, ExecutionServices services)
        {
            var source = text.Utf8Bytes.Span;
            var builder = new GovernedByteBuilder(services.MemoryGovernor, span);
            var changed = false;
            long scalars = 0;
            long nextCheck = 0;
            try
            {
                for (var cursor = 0; cursor < source.Length;)
                {
                    if (cursor >= nextCheck)
                    {
                        services.CheckExecutionBudget(span);
                        nextCheck = (long)cursor + 1024;
                    }
                    Rune.DecodeFromUtf8(source[cursor..], out var rune, out var width);
                    var replacement = Replacement(rune.Value, mask);
                    scalars += replacement?.Utf8Bytes.Length ?? 1;
                    CheckHtmlOutputLength(scalars, span, services);
                    if (replacement is not null)
                    {
                        if (!changed) { builder.Append(source[..cursor]); changed = true; }
                        builder.Append(replacement);
                    }
                    else if (changed) builder.Append(source.Slice(cursor, width));
                    cursor += width;
                }
                return changed ? FinishHtmlText(builder, span, services) : text;
            }
            finally { builder.Release(); }
        }

        private static PyString? Replacement(int scalar, EscapeMask mask) => scalar switch
        {
            '&' when (mask & EscapeMask.Ampersand) != 0 => AmpEntity,
            '<' when (mask & EscapeMask.LessThan) != 0 => LessEntity,
            '>' when (mask & EscapeMask.GreaterThan) != 0 => GreaterEntity,
            '"' when (mask & EscapeMask.DoubleQuote) != 0 => QuoteEntity,
            '\'' when (mask & EscapeMask.Apostrophe) != 0 => ApostropheEntity,
            _ => null,
        };

        private static void CheckHtmlOutputLength(long scalars, LythonSourceSpan span, ExecutionServices services)
        {
            if (services.Limits.MaxStringLength is { } maximum && scalars > maximum)
                throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximum})", span);
        }

        private static PyString FinishHtmlText(GovernedByteBuilder builder, LythonSourceSpan span, ExecutionServices services)
        {
            var result = builder.ToPyStringAndRelease();
            services.State.CallTemporaries.TrackFreshString(result, span);
            return result;
        }
    }
}
