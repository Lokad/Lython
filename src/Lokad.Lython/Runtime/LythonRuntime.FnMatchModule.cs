using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class FnMatchModule : PyModule
    {
        public static readonly FnMatchModule Instance = new();

        private FnMatchModule() : base("fnmatch")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fnmatch" => BuiltinCallable.Create(LythonKnownCallableSignatures.FnMatch, Match),
                "fnmatchcase" => BuiltinCallable.Create(LythonKnownCallableSignatures.FnMatchCase, MatchCase),
                "filter" => BuiltinCallable.Create(LythonKnownCallableSignatures.FnMatchFilter, Filter),
                "translate" => BuiltinCallable.Create(LythonKnownCallableSignatures.FnMatchTranslate, Translate),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatch(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern, context.State.CancellationToken);
        }

        private object MatchCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatchcase(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern, context.State.CancellationToken);
        }

        private object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable and a string pattern.", span);
            }

            var result = new PyList([], context.MemoryGovernor, span);
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                if (!PyStringOps.TryAsString(item, out var name))
                {
                    throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable of strings.", span);
                }

                if (MatchSimple(name, pattern, context.State.CancellationToken))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private object Translate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.translate(pattern) expects a string pattern.", span);
            }

            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(128L + 24L * pattern.Utf8Bytes.Length), span);
            return CreateString(TranslatePattern(pattern.AsString(), context.State.CancellationToken), context, span);
        }

        internal static bool MatchSimple(PyString name, PyString pattern, CancellationToken cancellationToken = default)
            => MatchSimple(name.Utf8Bytes.Span, pattern.Utf8Bytes.Span, cancellationToken);

        private static bool MatchSimple(ReadOnlySpan<byte> name, ReadOnlySpan<byte> pattern, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nameIndex = 0;
            var patternIndex = 0;
            var fallbackPatternIndex = -1;
            var fallbackNameIndex = -1;

            while (nameIndex < name.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (patternIndex < pattern.Length && pattern[patternIndex] == (byte)'*')
                {
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        patternIndex++;
                    }
                    while (patternIndex < pattern.Length && pattern[patternIndex] == (byte)'*');

                    if (patternIndex == pattern.Length) return true;
                    fallbackPatternIndex = patternIndex;
                    fallbackNameIndex = nameIndex;
                    continue;
                }

                var nextNameIndex = nameIndex;
                var value = ReadScalar(name, ref nextNameIndex);
                if (patternIndex < pattern.Length &&
                    TryMatchSingleToken(value, pattern, patternIndex, cancellationToken, out var nextPatternIndex))
                {
                    nameIndex = nextNameIndex;
                    patternIndex = nextPatternIndex;
                    continue;
                }

                if (fallbackPatternIndex < 0 || fallbackNameIndex >= name.Length) return false;
                // Retry after the most recent star, assigning one more scalar
                // to it. All positions stay in the original UTF-8 storage.
                _ = ReadScalar(name, ref fallbackNameIndex);
                nameIndex = fallbackNameIndex;
                patternIndex = fallbackPatternIndex;
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == (byte)'*')
            {
                cancellationToken.ThrowIfCancellationRequested();
                patternIndex++;
            }
            return patternIndex == pattern.Length;
        }

        private static bool TryMatchSingleToken(int value, ReadOnlySpan<byte> pattern, int patternIndex,
            CancellationToken cancellationToken, out int nextPatternIndex)
        {
            nextPatternIndex = patternIndex;
            var token = ReadScalar(pattern, ref nextPatternIndex);
            if (token == '?') return true;
            if (token == '[' && TryMatchCharacterClass(value, pattern, patternIndex, cancellationToken,
                out var closeIndex, out var matches))
            {
                nextPatternIndex = closeIndex + 1;
                return matches;
            }
            return token == value;
        }

        private static bool TryMatchCharacterClass(int value, ReadOnlySpan<byte> pattern, int openIndex,
            CancellationToken cancellationToken, out int closeIndex, out bool matches)
        {
            closeIndex = -1;
            matches = false;
            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Length) return false;
            var negated = pattern[contentStart] == (byte)'!';
            if (negated) contentStart++;
            if (contentStart >= pattern.Length) return false;
            var searchStart = contentStart;
            if (pattern[searchStart] == (byte)']') searchStart++;
            for (var index = searchStart; index < pattern.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pattern[index] == (byte)']')
                {
                    closeIndex = index;
                    break;
                }
            }
            if (closeIndex < 0) return false;
            var included = CharacterClassIncludes(value, pattern, contentStart, closeIndex, cancellationToken);
            matches = negated ? !included : included;
            return true;
        }

        private static bool CharacterClassIncludes(int value, ReadOnlySpan<byte> pattern, int start, int closeIndex,
            CancellationToken cancellationToken)
        {
            for (var index = start; index < closeIndex;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = ReadScalar(pattern, ref index);
                if (index + 1 < closeIndex && pattern[index] == (byte)'-')
                {
                    index++;
                    var last = ReadScalar(pattern, ref index);
                    if (first <= last && value >= first && value <= last) return true;
                }
                else if (first == value) return true;
            }
            return false;
        }

        private static int ReadScalar(ReadOnlySpan<byte> text, ref int index)
        {
            if (text[index] < 128) return text[index++];
            if (Rune.DecodeFromUtf8(text[index..], out var rune, out var width) != System.Buffers.OperationStatus.Done)
                throw new InvalidOperationException("Malformed internal UTF-8 string.");
            index += width;
            return rune.Value;
        }

        private static string TranslatePattern(string pattern, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder(pattern.Length + 7);
            builder.Append("(?s:");
            var noClosingBracketRemaining = false;
            for (var index = 0; index < pattern.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ch = pattern[index];
                switch (ch)
                {
                    case '*':
                        builder.Append(".*");
                        break;
                    case '?':
                        builder.Append('.');
                        break;
                    case '[':
                        if (noClosingBracketRemaining)
                        {
                            AppendEscapedRegexLiteral(builder, ch);
                            break;
                        }

                        index = AppendWildcardCharacterClass(builder, pattern, index, cancellationToken, out var foundClosingBracket);
                        noClosingBracketRemaining = !foundClosingBracket;
                        break;
                    default:
                        AppendEscapedRegexLiteral(builder, ch);
                        break;
                }
            }

            builder.Append(@")\Z");
            return builder.ToString();
        }

    }
}
