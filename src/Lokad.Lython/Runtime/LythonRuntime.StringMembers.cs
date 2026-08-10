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
            BasicStringMemberProvider.Instance,
            AdvancedStringMemberProvider.Instance,
        ];

        private interface IStringMemberProvider
        {
            /// <summary>Resolves members owned by one cohesive string-operation family.</summary>
            bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value);
        }

        public static bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
        {
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
        {
            return value switch
            {
                BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                    ? throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span)
                    : (int)integer,
                int integer => integer,
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects {name} to be an integer.", span)
            };
        }

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

        private static object ResolveFormatField(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var index = 0;
            var current = ResolveFormatFieldRoot(field, positional, keywords, ref index);

            while (index < field.Length)
            {
                if (field[index] == '.')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] is not '.' and not '[')
                    {
                        index++;
                    }

                    if (start == index)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var memberName = field[start..index];
                    var memberTarget = current;
                    if (!PyMemberAccess.TryResolve(memberTarget, memberName, context, span, out current))
                    {
                        throw PyMemberAccess.CreateMissingMemberError(memberTarget, memberName, span);
                    }

                    continue;
                }

                if (field[index] == '[')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] != ']')
                    {
                        index++;
                    }

                    if (index >= field.Length)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var token = field[start..index];
                    index++;
                    object key = int.TryParse(token, out var intIndex)
                        ? new BigInteger(intIndex)
                        : PyString.FromString(token);
                    current = PyIndexing.ReadIndex(current, key, span);
                    continue;
                }

                throw new InvalidOperationException("Invalid format field.");
            }

            return current;
        }

        private static object ResolveFormatFieldRoot(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            ref int index)
        {
            var start = index;
            while (index < field.Length && field[index] is not '.' and not '[')
            {
                index++;
            }

            var root = field[start..index];
            if (root.Length == 0)
            {
                throw new InvalidOperationException("Invalid format field.");
            }

            if (int.TryParse(root, out var intIndex))
            {
                if (intIndex < 0 || intIndex >= positional.Count)
                {
                    throw new IndexOutOfRangeException($"Replacement index {intIndex} out of range for positional args tuple");
                }

                return positional[intIndex];
            }

            if (!keywords.TryGetValue(root, out var value))
            {
                throw new KeyNotFoundException(root);
            }

            return value;
        }
    }
}
