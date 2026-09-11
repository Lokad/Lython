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
    internal static partial class StringMembers
    {
        private readonly record struct StringSearchBounds(int Start, int End, bool StartBeyondLength);

        private static readonly IStringMemberProvider[] Providers =
        [
            TextSemanticsMemberProvider.Instance,
            StringSegmentationMemberProvider.Instance,
            StringLayoutSearchMemberProvider.Instance,
            StringAffixFormatMemberProvider.Instance,
            StringTranslateMemberProvider.Instance,
        ];

        private interface IStringMemberProvider
        {
            /// <summary>Resolves members owned by one cohesive string-operation family.</summary>
            bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value);
        }

        public static bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
        {
            // maketrans is a staticmethod shape (no receiver), so it rides
            // the shared singleton instead of a text-bound provider.
            if (name == "maketrans")
            {
                value = BuiltinTypeMethod.StrMaketrans;
                return true;
            }

            foreach (var provider in Providers)
            {
                if (provider.TryGetMember(text, name, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }
        private static int ParseStringOptionalInt(object value, string name, string signature, LythonSourceSpan span)
            => RuntimeArgumentValidation.ParseInt32(value, name, signature, span);

        private static StringSearchBounds ParseStringBounds(
            int textLength,
            object[] arguments,
            LythonSourceSpan span,
            string signature)
        {
            try
            {
                object? start = arguments.Length >= 2 ? arguments[1] : null;
                object? end = arguments.Length == 3 ? arguments[2] : null;
                var normalized = PyStringOps.NormalizeRange(textLength, start, end);
                var startBeyondLength = start switch
                {
                    BigInteger integer => integer > textLength,
                    int integer => integer > textLength,
                    _ => false
                };
                return new StringSearchBounds(normalized.Start, normalized.End, startBeyondLength);
            }
            catch (InvalidOperationException)
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
            }
        }

        private static bool StartsOrEndsWith(
            PyString text,
            object prefixOrTuple,
            int start,
            int end,
            bool startBeyondLength,
            bool isStart,
            LythonSourceSpan span)
        {
            if (end < start)
            {
                return false;
            }

            if (PyStringOps.TryAsString(prefixOrTuple, out var single))
            {
                return !startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, single, start, end) : PyStringOps.EndsWith(text, single, start, end));
            }

            if (prefixOrTuple is not PyTuple tuple)
            {
                throw new LythonRuntimeException("TypeError",
                    isStart
                        ? "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds."
                        : "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.",
                    span);
            }

            foreach (var item in tuple)
            {
                if (!PyStringOps.TryAsString(item, out var textItem))
                {
                    throw new LythonRuntimeException("TypeError",
                        isStart
                            ? $"tuple for startswith must only contain str, not {TypeName(item)}"
                            : $"tuple for endswith must only contain str, not {TypeName(item)}",
                        span);
                }

                if (!startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, textItem, start, end) : PyStringOps.EndsWith(text, textItem, start, end)))
                {
                    return true;
                }
            }

            return false;
        }

        private static PyString? RequireFillChar(object value, string signature, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var fill))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects fillchar to be a string.", span);
            }

            return fill;
        }

        private static string TypeName(object value)
        {
            return value switch
            {
                BigInteger => "int",
                int => "int",
                bool => "bool",
                PyString => "str",
                PyTuple => "tuple",
                PyList => "list",
                PyDict => "dict",
                PyNone => "NoneType",
                _ => value.GetType().Name
            };
        }

        private static object ResolveFormatFieldSuffix(
            object current,
            string suffix,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var index = 0;
            while (index < suffix.Length)
            {
                if (suffix[index] == '.')
                {
                    index++;
                    var start = index;
                    while (index < suffix.Length && suffix[index] is not '.' and not '[')
                    {
                        index++;
                    }

                    if (start == index)
                    {
                        throw new InvalidOperationException("Empty attribute in format string");
                    }

                    var memberName = suffix[start..index];
                    var memberTarget = current;
                    if (!PyMemberAccess.TryResolve(memberTarget, memberName, context, span, out current))
                    {
                        throw PyMemberAccess.CreateMissingMemberError(memberTarget, memberName, span, context);
                    }

                    continue;
                }

                if (suffix[index] == '[')
                {
                    index++;
                    var start = index;
                    while (index < suffix.Length && suffix[index] != ']')
                    {
                        index++;
                    }

                    if (index >= suffix.Length)
                    {
                        throw new InvalidOperationException("expected '}' before end of string");
                    }

                    var token = suffix[start..index];
                    index++;
                    if (token.Length == 0)
                    {
                        throw new InvalidOperationException("Empty attribute in format string");
                    }

                    object key = int.TryParse(token, out var intIndex)
                        ? new BigInteger(intIndex)
                        : PyString.FromString(token);
                    current = PyIndexing.ReadIndex(current, key, span);
                    continue;
                }

                throw new InvalidOperationException("Only '.' or '[' may follow ']' in format field specifier");
            }

            return current;
        }
    }
}
