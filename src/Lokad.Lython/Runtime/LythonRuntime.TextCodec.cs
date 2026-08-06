using System.Buffers;
using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal enum TextErrorMode
    {
        Strict,
        Ignore,
        Replace,
        BackslashReplace,
    }

    internal enum TextNewlineMode
    {
        TranslateUniversal,
        PreserveUniversal,
        PreserveLineFeed,
        PreserveCarriageReturn,
        PreserveCarriageReturnLineFeed,
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static TextEncodingMode ParseTextEncoding(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return TextEncodingMode.Utf8;
        }

        if (!PyStringOps.TryAsString(value, out var encoding))
        {
            throw UnsupportedTextEncoding(owner, span);
        }

        return encoding.AsString().ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => TextEncodingMode.Utf8,
            "utf-8-sig" => TextEncodingMode.Utf8Bom,
            "latin-1" or "latin1" or "iso-8859-1" => TextEncodingMode.Latin1,
            _ => throw UnsupportedTextEncoding(owner, span)
        };
    }

    private static LythonRuntimeException UnsupportedTextEncoding(string owner, LythonSourceSpan span)
        => new(
            "ValueError",
            $"{owner} only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            span);

    private static TextErrorMode ParseTextErrors(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return TextErrorMode.Strict;
        }

        if (!PyStringOps.TryAsString(value, out var errors))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", span);
        }

        return errors.AsString().ToLowerInvariant() switch
        {
            "strict" => TextErrorMode.Strict,
            "ignore" => TextErrorMode.Ignore,
            "replace" => TextErrorMode.Replace,
            "backslashreplace" => TextErrorMode.BackslashReplace,
            "surrogateescape" or "surrogatepass" => throw new LythonRuntimeException("NotImplementedError", $"{owner} does not support surrogate error handlers because Lython strings are UTF-8 scalar values.", span),
            _ => throw new LythonRuntimeException("ValueError", $"{owner} only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", span)
        };
    }

    private static TextNewlineMode ParseTextNewline(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return TextNewlineMode.TranslateUniversal;
        }

        if (!PyStringOps.TryAsString(value, out var newline))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} newline must be None, '', '\\n', '\\r', or '\\r\\n'.", span);
        }

        return newline.AsString() switch
        {
            "" => TextNewlineMode.PreserveUniversal,
            "\n" => TextNewlineMode.PreserveLineFeed,
            "\r" => TextNewlineMode.PreserveCarriageReturn,
            "\r\n" => TextNewlineMode.PreserveCarriageReturnLineFeed,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} newline must be None, '', '\\n', '\\r', or '\\r\\n'.", span)
        };
    }

    private static string ParseTextOpenMode(PyString mode, string owner, LythonSourceSpan span)
    {
        var text = mode.AsString();
        if (text.Contains('b', StringComparison.Ordinal))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} only supports text modes; binary modes like 'rb' and 'wb' are unsupported.", span);
        }

        if (text.Contains('+', StringComparison.Ordinal))
        {
            throw new LythonRuntimeException("NotImplementedError", $"{owner} does not support updating text modes such as 'r+'.", span);
        }

        return text switch
        {
            "r" or "rt" => "r",
            "w" or "wt" => "w",
            "a" or "at" => "a",
            _ => throw new LythonRuntimeException("ValueError", $"{owner} only supports modes 'r', 'w', and 'a' with optional text marker 't'.", span)
        };
    }

    private static void ValidateTextBuffering(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return;
        }

        if (!PyNumberOps.TryAsInteger(value, out _))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects buffering to be an integer or None.", span);
        }
    }

    private static void ValidateCloseFd(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone or true)
        {
            return;
        }

        if (value is false)
        {
            throw new LythonRuntimeException("NotImplementedError", $"{owner} only supports closefd=True for host-mediated paths.", span);
        }

        throw new LythonRuntimeException("TypeError", $"{owner} expects closefd to be a bool.", span);
    }

    private static void ValidateOpener(object value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return;
        }

        throw new LythonRuntimeException("NotImplementedError", $"{owner} opener is not supported because file access is host-mediated.", span);
    }

    private static PyString DecodeUtf8Text(
        ReadOnlyMemory<byte> utf8,
        ExecutionContext context,
        LythonSourceSpan? span,
        TextErrorMode errors = TextErrorMode.Strict,
        TextNewlineMode newline = TextNewlineMode.TranslateUniversal)
        => DecodeUtf8Text(utf8, context.MemoryGovernor, span, errors, newline);

    internal static PyString DecodeUtf8Text(
        ReadOnlyMemory<byte> utf8,
        MemoryGovernor governor,
        LythonSourceSpan? span,
        TextErrorMode errors = TextErrorMode.Strict,
        TextNewlineMode newline = TextNewlineMode.TranslateUniversal)
    {
        if (utf8.Length == 0)
        {
            return PyString.Empty;
        }

        var decoded = DecodeUtf8ToString(utf8.Span, errors, span);
        decoded = ApplyReadNewlineMode(decoded, newline);
        return decoded.Length == 0
            ? PyString.Empty
            : PyString.FromString(decoded, governor, span);
    }

    private static PyString DecodeText(
        ReadOnlyMemory<byte> payload,
        TextEncodingMode encoding,
        ExecutionContext context,
        LythonSourceSpan? span,
        TextErrorMode errors = TextErrorMode.Strict,
        TextNewlineMode newline = TextNewlineMode.TranslateUniversal)
    {
        if (encoding != TextEncodingMode.Latin1)
        {
            var text = DecodeUtf8Text(payload, context, span, errors, newline);
            if (encoding != TextEncodingMode.Utf8Bom)
            {
                return text;
            }

            var decoded = text.AsString();
            return decoded.Length > 0 && decoded[0] == '\uFEFF'
                ? CreateString(decoded[1..], context, span)
                : text;
        }

        if (payload.Length == 0)
        {
            return PyString.Empty;
        }

        var chars = new char[payload.Length];
        var bytes = payload.Span;
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = (char)bytes[i];
        }

        var decodedLatin1 = ApplyReadNewlineMode(new string(chars), newline);
        return decodedLatin1.Length == 0
            ? PyString.Empty
            : CreateString(decodedLatin1, context, span);
    }

    private static PyString StripUtf8Bom(PyString text, TextEncodingMode encoding)
    {
        if (encoding != TextEncodingMode.Utf8Bom)
        {
            return text;
        }

        var decoded = text.AsString();
        return decoded.Length > 0 && decoded[0] == '\uFEFF'
            ? PyString.FromString(decoded[1..])
            : text;
    }

    private static byte[] EncodeText(
        PyString text,
        TextEncodingMode encoding,
        TextErrorMode errors,
        TextNewlineMode newline,
        ExecutionContext context,
        LythonSourceSpan? span)
    {
        var output = ApplyWriteNewlineMode(text.AsString(), newline);
        if (encoding == TextEncodingMode.Latin1)
        {
            return EncodeLatin1(output, errors, context, span);
        }

        var byteCount = StrictUtf8.GetByteCount(output) + (encoding == TextEncodingMode.Utf8Bom ? 3 : 0);
        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(byteCount), span);
        var utf8 = StrictUtf8.GetBytes(output);
        return encoding == TextEncodingMode.Utf8Bom
            ? [0xEF, 0xBB, 0xBF, .. utf8]
            : utf8;
    }

    private static byte[] EncodeLatin1(
        string text,
        TextErrorMode errors,
        ExecutionContext context,
        LythonSourceSpan? span)
    {
        var byteCount = 0;
        var position = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value <= byte.MaxValue)
            {
                byteCount++;
            }
            else
            {
                byteCount = errors switch
                {
                    TextErrorMode.Ignore => byteCount,
                    TextErrorMode.Replace => checked(byteCount + 1),
                    TextErrorMode.BackslashReplace => checked(byteCount + (rune.Value <= 0xFFFF ? 6 : 10)),
                    _ => throw Latin1EncodeError(rune, position, span)
                };
            }

            position++;
        }

        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(byteCount), span);
        if (byteCount == 0)
        {
            return [];
        }

        var bytes = new byte[byteCount];
        var offset = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value <= byte.MaxValue)
            {
                bytes[offset++] = (byte)rune.Value;
                continue;
            }

            switch (errors)
            {
                case TextErrorMode.Ignore:
                    break;
                case TextErrorMode.Replace:
                    bytes[offset++] = (byte)'?';
                    break;
                case TextErrorMode.BackslashReplace:
                    var escaped = rune.Value <= 0xFFFF
                        ? $"\\u{rune.Value:x4}"
                        : $"\\U{rune.Value:x8}";
                    foreach (var character in escaped)
                    {
                        bytes[offset++] = (byte)character;
                    }
                    break;
            }
        }

        return bytes;
    }

    private static LythonRuntimeException Latin1EncodeError(Rune rune, int position, LythonSourceSpan? span)
    {
        var escaped = rune.Value <= 0xFFFF
            ? $"\\u{rune.Value:x4}"
            : $"\\U{rune.Value:x8}";
        return new LythonRuntimeException(
            "UnicodeEncodeError",
            $"'latin-1' codec can't encode character '{escaped}' in position {position}: ordinal not in range(256)",
            span);
    }

    private static string DecodeUtf8ToString(ReadOnlySpan<byte> utf8, TextErrorMode errors, LythonSourceSpan? span)
    {
        if (errors == TextErrorMode.Strict)
        {
            try
            {
                return StrictUtf8.GetString(utf8);
            }
            catch (DecoderFallbackException ex)
            {
                throw new LythonRuntimeException("UnicodeDecodeError", "invalid UTF-8 text", span, ex);
            }
        }

        var builder = new StringBuilder(utf8.Length);
        for (var i = 0; i < utf8.Length;)
        {
            if (TryDecodeUtf8Rune(utf8[i..], out var rune, out var validLength, out var invalidLength))
            {
                builder.Append(rune.ToString());
                i += validLength;
                continue;
            }

            var invalid = utf8.Slice(i, invalidLength);
            switch (errors)
            {
                case TextErrorMode.Ignore:
                    break;
                case TextErrorMode.Replace:
                    builder.Append('\uFFFD');
                    break;
                case TextErrorMode.BackslashReplace:
                    AppendBackslashEscapedBytes(builder, invalid);
                    break;
            }

            i += invalidLength;
        }

        return builder.ToString();
    }

    private static bool TryDecodeUtf8Rune(
        ReadOnlySpan<byte> utf8,
        out Rune rune,
        out int validLength,
        out int invalidLength)
    {
        rune = default;
        validLength = 0;
        invalidLength = 1;

        var first = utf8[0];
        if (first < 0x80)
        {
            rune = new Rune(first);
            validLength = 1;
            return true;
        }

        var expectedLength = first switch
        {
            >= 0xC2 and <= 0xDF => 2,
            >= 0xE0 and <= 0xEF => 3,
            >= 0xF0 and <= 0xF4 => 4,
            _ => 0
        };

        if (expectedLength == 0)
        {
            return false;
        }

        var consumedContinuationBytes = 0;
        for (var i = 1; i < expectedLength; i++)
        {
            if (i >= utf8.Length)
            {
                invalidLength = 1 + consumedContinuationBytes;
                return false;
            }

            if (!IsUtf8Continuation(utf8[i]))
            {
                invalidLength = 1 + consumedContinuationBytes;
                return false;
            }

            consumedContinuationBytes++;
        }

        if (!IsCanonicalUtf8Sequence(utf8[..expectedLength]))
        {
            invalidLength = Math.Max(1, expectedLength - 1);
            return false;
        }

        var status = Rune.DecodeFromUtf8(utf8[..expectedLength], out rune, out validLength);
        if (status == OperationStatus.Done && validLength == expectedLength)
        {
            return true;
        }

        validLength = 0;
        invalidLength = expectedLength;
        return false;
    }

    private static bool IsUtf8Continuation(byte value)
        => (value & 0b1100_0000) == 0b1000_0000;

    private static bool IsCanonicalUtf8Sequence(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length switch
        {
            2 => true,
            3 => bytes[0] switch
            {
                0xE0 => bytes[1] >= 0xA0,
                0xED => bytes[1] <= 0x9F,
                _ => true
            },
            4 => bytes[0] switch
            {
                0xF0 => bytes[1] >= 0x90,
                0xF4 => bytes[1] <= 0x8F,
                _ => true
            },
            _ => false
        };
    }

    private static void AppendBackslashEscapedBytes(StringBuilder builder, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            builder.Append("\\x");
            builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }
    }

    private static string ApplyReadNewlineMode(string text, TextNewlineMode newline)
        => newline == TextNewlineMode.TranslateUniversal
            ? NormalizeNewlineString(text)
            : text;

    private static string ApplyWriteNewlineMode(string text, TextNewlineMode newline)
    {
        return newline switch
        {
            TextNewlineMode.PreserveUniversal or TextNewlineMode.PreserveLineFeed => text,
            TextNewlineMode.PreserveCarriageReturn => text.Replace("\n", "\r", StringComparison.Ordinal),
            TextNewlineMode.PreserveCarriageReturnLineFeed => text.Replace("\n", "\r\n", StringComparison.Ordinal),
            _ => text
        };
    }

    private static string NormalizeNewlineString(string text)
    {
        if (!text.Contains('\r', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                builder.Append('\n');
                continue;
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static int ParseOptionalSize(object[] arguments, string signature, LythonSourceSpan span)
    {
        if (arguments.Length == 0 || arguments[0] is null or PyNone)
        {
            return -1;
        }

        if (arguments.Length != 1 ||
            !PyNumberOps.TryAsInteger(arguments[0], out var size) ||
            size < int.MinValue ||
            size > int.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", $"{signature} expects an optional integer size.", span);
        }

        return (int)size;
    }
}
