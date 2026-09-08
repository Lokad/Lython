using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

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
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatch(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern);
        }

        private object MatchCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatchcase(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern);
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

                if (MatchSimple(name, pattern))
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

            return CreateString(TranslatePattern(pattern.AsString()), context, span);
        }

        internal static bool MatchSimple(PyString name, PyString pattern)
        {
            var nameRunes = MaterializeRunes(name);
            var patternRunes = MaterializeRunes(pattern);
            return MatchSimple(nameRunes, patternRunes);
        }

        private static PyString[] MaterializeRunes(PyString value)
        {
            var runes = new PyString[value.Length];
            var index = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                runes[index++] = rune;
            }

            return runes;
        }

        private static bool MatchSimple(
            IReadOnlyList<PyString> name,
            IReadOnlyList<PyString> pattern)
        {
            var nameIndex = 0;
            var patternIndex = 0;
            var fallbackPatternIndex = -1;
            var fallbackNameIndex = -1;

            while (nameIndex < name.Count)
            {
                if (patternIndex < pattern.Count && IsAsciiRune(pattern[patternIndex], '*'))
                {
                    do
                    {
                        patternIndex++;
                    }
                    while (patternIndex < pattern.Count && IsAsciiRune(pattern[patternIndex], '*'));

                    if (patternIndex == pattern.Count)
                    {
                        return true;
                    }

                    fallbackPatternIndex = patternIndex;
                    fallbackNameIndex = nameIndex;
                    continue;
                }

                if (patternIndex < pattern.Count &&
                    TryMatchSingleToken(name[nameIndex], pattern, patternIndex, out var nextPatternIndex))
                {
                    nameIndex++;
                    patternIndex = nextPatternIndex;
                    continue;
                }

                if (fallbackPatternIndex < 0 || fallbackNameIndex >= name.Count)
                {
                    return false;
                }

                // Retry the suffix after the most recent '*' with one more input
                // rune assigned to that wildcard, without growing the CLR stack.
                fallbackNameIndex++;
                nameIndex = fallbackNameIndex;
                patternIndex = fallbackPatternIndex;
            }

            while (patternIndex < pattern.Count && IsAsciiRune(pattern[patternIndex], '*'))
            {
                patternIndex++;
            }

            return patternIndex == pattern.Count;

            static bool TryMatchSingleToken(
                PyString value,
                IReadOnlyList<PyString> pattern,
                int patternIndex,
                out int nextPatternIndex)
            {
                var token = pattern[patternIndex];
                if (IsAsciiRune(token, '?'))
                {
                    nextPatternIndex = patternIndex + 1;
                    return true;
                }

                if (IsAsciiRune(token, '[') &&
                    TryMatchCharacterClass(value, pattern, patternIndex, out var classCloseIndex, out var classMatches))
                {
                    nextPatternIndex = classCloseIndex + 1;
                    return classMatches;
                }

                nextPatternIndex = patternIndex + 1;
                return token.Equals(value);
            }
        }

        private static bool TryMatchCharacterClass(
            PyString value,
            IReadOnlyList<PyString> pattern,
            int openIndex,
            out int closeIndex,
            out bool matches)
        {
            closeIndex = -1;
            matches = false;

            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var negated = IsAsciiRune(pattern[contentStart], '!');
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var searchStart = contentStart;
            if (IsAsciiRune(pattern[searchStart], ']'))
            {
                searchStart++;
            }

            for (var index = searchStart; index < pattern.Count; index++)
            {
                if (IsAsciiRune(pattern[index], ']'))
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                return false;
            }

            var valueScalar = RuneScalarValue(value);
            var included = CharacterClassIncludes(valueScalar, pattern, contentStart, closeIndex);
            matches = negated ? !included : included;
            return true;
        }

        private static bool CharacterClassIncludes(int valueScalar, IReadOnlyList<PyString> pattern, int start, int closeIndex)
        {
            for (var index = start; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && IsAsciiRune(pattern[index + 1], '-'))
                {
                    var rangeStart = RuneScalarValue(pattern[index]);
                    var rangeEnd = RuneScalarValue(pattern[index + 2]);
                    if (rangeStart <= rangeEnd && valueScalar >= rangeStart && valueScalar <= rangeEnd)
                    {
                        return true;
                    }

                    index += 2;
                    continue;
                }

                if (RuneScalarValue(pattern[index]) == valueScalar)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TranslatePattern(string pattern)
        {
            var builder = new StringBuilder(pattern.Length + 2);
            builder.Append('^');
            var noClosingBracketRemaining = false;
            for (var index = 0; index < pattern.Length; index++)
            {
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

                        index = AppendTranslatedCharacterClass(builder, pattern, index, out var foundClosingBracket);
                        noClosingBracketRemaining = !foundClosingBracket;
                        break;
                    default:
                        AppendEscapedRegexLiteral(builder, ch);
                        break;
                }
            }

            builder.Append('$');
            return builder.ToString();
        }

        private static int AppendTranslatedCharacterClass(
            StringBuilder builder,
            string pattern,
            int openIndex,
            out bool foundClosingBracket)
        {
            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Length)
            {
                foundClosingBracket = false;
                builder.Append("\\[");
                return openIndex;
            }

            var negated = pattern[contentStart] == '!';
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Length)
            {
                foundClosingBracket = false;
                builder.Append("\\[");
                return openIndex;
            }

            var searchStart = contentStart;
            if (pattern[searchStart] == ']')
            {
                searchStart++;
            }

            var closeIndex = -1;
            for (var index = searchStart; index < pattern.Length; index++)
            {
                if (pattern[index] == ']')
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                foundClosingBracket = false;
                builder.Append("\\[");
                return openIndex;
            }

            foundClosingBracket = true;
            var classBuilder = new StringBuilder(closeIndex - contentStart);
            for (var index = contentStart; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && pattern[index + 1] == '-')
                {
                    if (char.ConvertToUtf32(pattern, index) <= char.ConvertToUtf32(pattern, index + 2))
                    {
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: true);
                        classBuilder.Append('-');
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index + 2], allowRangeHyphen: true);
                    }

                    index += 2;
                    continue;
                }

                AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: false);
            }

            if (classBuilder.Length == 0)
            {
                builder.Append(negated ? "." : "(?!)");
                return closeIndex;
            }

            builder.Append('[');
            if (negated)
            {
                builder.Append('^');
            }

            builder.Append(classBuilder);
            builder.Append(']');
            return closeIndex;
        }

        private static void AppendEscapedRegexClassCharacter(StringBuilder builder, char ch, bool allowRangeHyphen)
        {
            if (ch is '\\' or ']' or '^' || ch == '-' && !allowRangeHyphen)
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        private static bool IsAsciiRune(PyString value, char ch)
            => value.Utf8Bytes.Length == 1 && value.Utf8Bytes.Span[0] == (byte)ch;

        private static int RuneScalarValue(PyString value)
        {
            var text = value.AsString();
            return char.ConvertToUtf32(text, 0);
        }
    }
}
