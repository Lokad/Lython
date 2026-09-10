using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class BytesMembers
    {
        public static bool TryGetMember(PyBytes bytes, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fromhex" => new BuiltinTypeMethod("bytes", "fromhex", bindsOwner: true, BytesFromHex),
                "maketrans" => BuiltinTypeMethod.BytesMaketrans,
                "translate" => new RawBoundCallable((arguments, span, context) => TranslateBytes(bytes, arguments, span, context)) { BoundName = "bytes.translate", BoundReceiver = bytes },
                "decode" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.decode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    return DecodeText(bytes.ToArray(), encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
                }, "bytes.decode", ["encoding", "errors"], 0),
                "hex" => new RawBoundCallable((arguments, span, context) => HexEncode(bytes, arguments, span, context)) { BoundName = "bytes.hex", BoundReceiver = bytes },
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static object TranslateBytes(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? table = null;
        object? delete = null;
        var positionals = 0;
        var keywords = 0;
        foreach (var argument in arguments)
        {
            if (!argument.IsKeyword)
            {
                positionals++;
                if (positionals == 1)
                {
                    table = argument.Value;
                }
                else if (positionals == 2)
                {
                    delete = argument.Value;
                }

                continue;
            }

            keywords++;
            if (argument.KeywordName == "delete")
            {
                delete = argument.Value;
            }
        }

        if (positionals == 0)
        {
            throw new LythonRuntimeException("TypeError", "translate() takes at least 1 positional argument (0 given)", span);
        }

        if (positionals + keywords > 2)
        {
            throw new LythonRuntimeException("TypeError", "translate() takes at most 2 arguments (" + (positionals + keywords) + " given)", span);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword && argument.KeywordName != "delete")
            {
                throw new LythonRuntimeException("TypeError", "translate() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
            }
        }

        if (table is not PyBytes tableBytes || tableBytes.Length != 256)
        {
            throw new LythonRuntimeException("ValueError", "translation table must be 256 characters long", span);
        }

        var mapping = tableBytes.ToArray();
        var discarded = new bool[256];
        if (delete is not null)
        {
            if (delete is not PyBytes deleteBytes)
            {
                throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(delete, context) + "'", span);
            }

            foreach (var octet in deleteBytes.ToArray())
            {
                discarded[octet] = true;
            }
        }

        var source = value.ToArray();
        var result = new List<byte>(source.Length);
        foreach (var octet in source)
        {
            if (!discarded[octet])
            {
                result.Add(mapping[octet]);
            }
        }

        return CreateBytes([.. result], context, span);
    }

    private static object HexEncode(PyBytes value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object? sep = null;
        object? group = null;
        var hasSepPositional = false;
        var hasGroupPositional = false;
        var positionals = 0;
        var keywords = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                keywords++;
                continue;
            }
            positionals++;
            if (positionals == 1)
            {
                sep = argument.Value;
                hasSepPositional = true;
            }
            else if (positionals == 2)
            {
                group = argument.Value;
                hasGroupPositional = true;
            }
        }
        if (positionals + keywords > 2)
        {
            throw new LythonRuntimeException("TypeError", "hex() takes at most 2 arguments (" + (positionals + keywords) + " given)", span);
        }

        foreach (var argument in arguments)
        {
            if (!argument.IsKeyword)
            {
                continue;
            }
            if (argument.KeywordName == "sep")
            {
                if (hasSepPositional)
                {
                    throw new LythonRuntimeException("TypeError", "argument for hex() given by name ('sep') and position (1)", span);
                }

                sep = argument.Value;
            }
            else if (argument.KeywordName == "bytes_per_sep")
            {
                if (hasGroupPositional)
                {
                    throw new LythonRuntimeException("TypeError", "argument for hex() given by name ('bytes_per_sep') and position (2)", span);
                }

                group = argument.Value;
            }
            else
            {
                throw new LythonRuntimeException("TypeError", "hex() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
            }
        }

        byte? separator = null;
        if (sep is not null)
        {
            separator = ParseHexSeparator(sep, span);
        }

        var perSep = 1;
        if (group is not null)
        {
            perSep = RuntimeArgumentValidation.ParseInt32(group, "bytes_per_sep", "bytes.hex([sep[, bytes_per_sep]])", span);
        }

        if (perSep < 0)
        {
            perSep = perSep == int.MinValue ? int.MaxValue : -perSep;
        }

        return CreateString(RenderHex(value.ToArray(), separator, perSep), context, span);
    }

    private static byte ParseHexSeparator(object sep, LythonSourceSpan span)
    {
        if (sep is PyString sepText)
        {
            var count = 0;
            var code = 0;
            foreach (var rune in sepText.AsString().EnumerateRunes())
            {
                count++;
                code = rune.Value;
            }

            if (count != 1)
            {
                throw new LythonRuntimeException("ValueError", "sep must be length 1.", span);
            }

            if (code > 127)
            {
                throw new LythonRuntimeException("ValueError", "sep must be ASCII.", span);
            }

            return (byte)code;
        }

        if (sep is PyBytes sepBytes)
        {
            if (sepBytes.Length != 1)
            {
                throw new LythonRuntimeException("ValueError", "sep must be length 1.", span);
            }

            var octet = sepBytes.Bytes[0];
            if (octet > 127)
            {
                throw new LythonRuntimeException("ValueError", "sep must be ASCII.", span);
            }

            return octet;
        }

        throw new LythonRuntimeException("TypeError", "hex() expects sep to be str or bytes.", span);
    }

    private static string RenderHex(byte[] source, byte? separator, int perSep)
    {
        const string Digits = "0123456789abcdef";
        var first = source.Length;
        if (separator is not null && perSep > 0 && source.Length > 0)
        {
            var head = source.Length % perSep;
            first = head == 0 ? Math.Min(perSep, source.Length) : head;
        }

        var groups = 1;
        if (first < source.Length)
        {
            groups += (source.Length - first + perSep - 1) / perSep;
        }

        var sepChar = (char)separator.GetValueOrDefault();
        var text = new char[2 * source.Length + groups - 1];
        var at = 0;
        var index = 0;
        for (var g = 0; g < groups; g++)
        {
            if (g > 0)
            {
                text[at++] = sepChar;
            }

            var end = g == 0 ? first : Math.Min(index + perSep, source.Length);
            while (index < end)
            {
                var octet = source[index++];
                text[at++] = Digits[octet >> 4];
                text[at++] = Digits[octet & 15];
            }
        }

        return new string(text);
    }


    private sealed class RawBoundCallable(
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation) : ICallable, IPyDynamicAttributes, IPyHashableValue, IPyRawBoundCallable
    {
        public string? BoundName { get; init; }

        public object? BoundReceiver { get; init; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return implementation(arguments, span, context);
        }

        // Named shapes expose CPython-style identity like bound builtins:
        // the short __name__, the qualified __qualname__, a None __module__
        // and the bound receiver (anonymous callables stay missing).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (BoundName is not null)
            {
                if (name == "__name__")
                {
                    value = PyString.FromString(ShortMethodName(BoundName));
                    return true;
                }

                if (name == "__qualname__")
                {
                    value = PyString.FromString(BoundName);
                    return true;
                }

                if (name == "__module__")
                {
                    value = PyNone.Instance;
                    return true;
                }

                if (name == "__self__" && BoundReceiver is not null)
                {
                    value = BoundReceiver;
                    return true;
                }
            }

            value = PyNone.Instance;
            return false;
        }

        private static string ShortMethodName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        public int GetPyHashCode() => BoundName is null || BoundReceiver is null
            ? RuntimeHelpers.GetHashCode(this)
            : HashCode.Combine(string.GetHashCode(BoundName, StringComparison.Ordinal), RuntimeHelpers.GetHashCode(BoundReceiver));
    }
}
