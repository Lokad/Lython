using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object EvaluateFormattedString(FormattedStringExpressionSyntax formatted, ExecutionContext context)
        => EvaluateFormattedStringParts(formatted.Parts, context, formatted.Span);

    private static PyString EvaluateFormattedStringParts(
        IReadOnlyList<FormattedStringPartSyntax> parts,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        foreach (var part in parts)
        {
            switch (part)
            {
                case FormattedStringTextPartSyntax text:
                    builder.AppendString(text.Text);
                    break;
                case FormattedStringExpressionPartSyntax expression:
                    var formatSpecifier = expression.FormatSpecifierParts is null
                        ? expression.FormatSpecifier
                        : EvaluateFormattedStringParts(expression.FormatSpecifierParts, context, span).AsString();
                    builder.Append(FormatInterpolatedStringPart(
                        EvaluateExpression(expression.Expression, context),
                        expression.Conversion,
                        formatSpecifier,
                        context,
                        span));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown formatted string part: {part.GetType().Name}");
            }
        }

        var value = builder.ToPyStringAndRelease();
        context.ObserveString(value, span);
        return value;
    }

    private static PyString ToInterpolatedPyString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedPyString(value, new PyRenderingContext(context));

    private static string ToInterpolatedString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedString(value, new PyRenderingContext(context));

    private static PyString ToPythonPyString(object value, ExecutionContext context)
        => PyRendering.ToPythonPyString(value, new PyRenderingContext(context));

    private static string ToPythonString(object value, ExecutionContext context)
        => PyRendering.ToPythonString(value, new PyRenderingContext(context));

    private static PyString ToReprPyString(object value, ExecutionContext context)
        => PyRendering.ToReprPyString(value, new PyRenderingContext(context));

    private static PyString FormatInterpolatedStringPart(
        object value,
        char? conversion,
        string? formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var converted = conversion switch
        {
            null => null,
            's' => ToInterpolatedPyString(value, context),
            'r' or 'a' => ToReprPyString(value, context),
            _ => throw new LythonRuntimeException("ValueError", $"Unknown conversion specifier '!{conversion}'.", span)
        };

        if (string.IsNullOrEmpty(formatSpecifier))
        {
            return converted ?? ToInterpolatedPyString(value, context);
        }

        var formatted = FormatInterpolatedStringValue(
            converted ?? value,
            formatSpecifier,
            context,
            span);
        return PyString.FromString(formatted, context.MemoryGovernor, span);
    }

    private static string FormatInterpolatedStringValue(
        object value,
        string formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (value is PyInstance instance &&
            instance.TryGetAttribute("__format__", context, span, out var formatMember) &&
            formatMember is ICallable formatCallable)
        {
            var formatted = CallableInvocation.InvokeUnary(
                formatCallable,
                PyString.FromString(formatSpecifier, context.MemoryGovernor, span),
                span,
                context);
            if (!PyStringOps.TryAsString(formatted, out var formattedText))
            {
                throw new LythonRuntimeException("TypeError", "__format__ returned non-string", span);
            }

            return formattedText.AsString();
        }

        if (value is PyDate or PyTime or PyDateTime)
        {
            return PyDateTimeOps.FormatValue(value, PyString.FromString(formatSpecifier), span).AsString();
        }

        var spec = ParseInterpolatedFormatSpecifier(formatSpecifier, span);
        if (TryFormatNumericValue(value, spec, context, span, out var numericText, out var numericPrefixLength))
        {
            return ApplyInterpolatedFormatPadding(numericText, spec, numericPrefixLength, numeric: true, span);
        }

        if (RequiresNumericFormat(spec))
        {
            throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires a numeric value.", span);
        }

        if (spec.Sign is not null || spec.Alternate || spec.Grouping is not null || spec.Align == '=')
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{formatSpecifier}' for string value.", span);
        }

        var text = value switch
        {
            PyString pyString => pyString.AsString(),
            _ => ToInterpolatedString(value, context)
        };

        if (spec.Type is not null and not 's')
        {
            throw new LythonRuntimeException("ValueError", $"Unknown format code '{spec.Type}' for string value.", span);
        }

        if (spec.Precision is { } precision)
        {
            text = text.Length <= precision ? text : text[..precision];
        }

        return ApplyInterpolatedFormatPadding(text, spec, numericPrefixLength: 0, numeric: false, span);
    }

    private static bool TryFormatNumericValue(
        object value,
        InterpolatedFormatSpecifier spec,
        ExecutionContext context,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        text = string.Empty;
        numericPrefixLength = 0;

        if (TryGetIntegerFormatValue(value, out var integer))
        {
            if (spec.Type is 'f' or 'F' or 'g' or 'G' or '%')
            {
                return TryFormatFloatingValue((double)integer, spec, span, out text, out numericPrefixLength);
            }

            text = FormatIntegerValue(integer, spec, span, out numericPrefixLength);
            return true;
        }

        if (value is double floating)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue(floating, spec, span, out text, out numericPrefixLength);
        }

        if (value is PyDecimal decimalValue)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue((double)decimalValue.Value, spec, span, out text, out numericPrefixLength);
        }

        _ = context;
        return false;
    }

    private static bool TryGetIntegerFormatValue(object value, out BigInteger integer)
    {
        switch (value)
        {
            case BigInteger bigInteger:
                integer = bigInteger;
                return true;
            case int intValue:
                integer = new BigInteger(intValue);
                return true;
            case bool boolValue:
                integer = boolValue ? BigInteger.One : BigInteger.Zero;
                return true;
            default:
                integer = BigInteger.Zero;
                return false;
        }
    }

    private static string FormatIntegerValue(
        BigInteger value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out int numericPrefixLength)
    {
        if (spec.Precision is not null)
        {
            throw new LythonRuntimeException("ValueError", "Precision is not allowed in integer format specifiers.", span);
        }

        var type = spec.Type ?? 'd';
        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        string digits;
        string prefix;
        switch (type)
        {
            case 'd':
            case 'n':
                digits = magnitude.ToString(CultureInfo.InvariantCulture);
                prefix = string.Empty;
                break;
            case 'b':
                digits = ToUnsignedBaseString(magnitude, 2, upper: false);
                prefix = spec.Alternate ? "0b" : string.Empty;
                break;
            case 'o':
                digits = ToUnsignedBaseString(magnitude, 8, upper: false);
                prefix = spec.Alternate ? "0o" : string.Empty;
                break;
            case 'x':
                digits = ToUnsignedBaseString(magnitude, 16, upper: false);
                prefix = spec.Alternate ? "0x" : string.Empty;
                break;
            case 'X':
                digits = ToUnsignedBaseString(magnitude, 16, upper: true);
                prefix = spec.Alternate ? "0X" : string.Empty;
                break;
            default:
                throw new LythonRuntimeException("ValueError", $"Unknown integer format code '{type}'.", span);
        }

        if (spec.Grouping is { } grouping)
        {
            if (type is not ('d' or 'n'))
            {
                throw new LythonRuntimeException("ValueError", "Grouping is only supported for decimal integer formatting.", span);
            }

            digits = GroupDigits(digits, grouping);
        }

        var sign = FormatNumericSign(negative, spec.Sign);
        numericPrefixLength = sign.Length + prefix.Length;
        return sign + prefix + digits;
    }

    private static bool TryFormatFloatingValue(
        double value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        var type = spec.Type;
        var precision = spec.Precision;
        if (type is 's' or 'b' or 'o' or 'x' or 'X' or 'd' or 'n')
        {
            text = string.Empty;
            numericPrefixLength = 0;
            return false;
        }

        if (spec.Alternate)
        {
            throw new LythonRuntimeException("ValueError", "Alternate floating-point formatting is not supported.", span);
        }

        var formatted = type switch
        {
            null when precision is null => value.ToString(CultureInfo.InvariantCulture),
            null => value.ToString("G" + precision.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'f' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'F' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'g' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'G' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            '%' => (value * 100.0).ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%",
            _ => throw new LythonRuntimeException("ValueError", $"Unknown floating-point format code '{type}'.", span)
        };

        if (type == 'F')
        {
            formatted = formatted.ToUpperInvariant();
        }

        if (spec.Grouping is { } grouping)
        {
            formatted = GroupFloatingDigits(formatted, grouping);
        }

        text = ApplyNumericSign(formatted, spec.Sign);
        numericPrefixLength = GetNumericPrefixLength(text);
        return true;
    }

    private static InterpolatedFormatSpecifier ParseInterpolatedFormatSpecifier(string text, LythonSourceSpan span)
    {
        var index = 0;
        char? fill = null;
        char? align = null;

        if (index + 1 < text.Length && IsFormatAlign(text[index + 1]))
        {
            fill = text[index];
            align = text[index + 1];
            index += 2;
        }
        else if (index < text.Length && IsFormatAlign(text[index]))
        {
            align = text[index];
            index++;
        }

        char? sign = null;
        if (index < text.Length && text[index] is '+' or '-' or ' ')
        {
            sign = text[index++];
        }

        var alternate = false;
        if (index < text.Length && text[index] == '#')
        {
            alternate = true;
            index++;
        }

        var zeroPad = false;
        if (index < text.Length && text[index] == '0')
        {
            zeroPad = true;
            index++;
        }

        int? width = null;
        var widthStart = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        if (index > widthStart)
        {
            width = int.Parse(text[widthStart..index], CultureInfo.InvariantCulture);
        }

        char? grouping = null;
        if (index < text.Length && text[index] is ',' or '_')
        {
            grouping = text[index++];
        }

        int? precision = null;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            var precisionStart = index;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
            }

            if (precisionStart == index)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
            }

            precision = int.Parse(text[precisionStart..index], CultureInfo.InvariantCulture);
        }

        char? type = null;
        if (index < text.Length)
        {
            type = text[index++];
        }

        if (index != text.Length)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
        }

        return new InterpolatedFormatSpecifier(fill, align, sign, alternate, zeroPad, width, grouping, precision, type);
    }

    private static bool RequiresNumericFormat(InterpolatedFormatSpecifier spec)
        => spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n' or 'f' or 'F' or 'g' or 'G' or '%';

    private static bool IsFormatAlign(char value)
        => value is '<' or '>' or '^' or '=';

    private static string ApplyInterpolatedFormatPadding(
        string text,
        InterpolatedFormatSpecifier spec,
        int numericPrefixLength,
        bool numeric,
        LythonSourceSpan span)
    {
        if (spec.Width is not { } width || text.Length >= width)
        {
            return text;
        }

        var align = spec.Align ?? (numeric ? '>' : '<');
        var fill = spec.Fill ?? (spec.ZeroPad ? '0' : ' ');
        if (numeric && spec.ZeroPad && spec.Align is null)
        {
            align = '=';
            fill = '0';
        }

        if (align == '=' && !numeric)
        {
            throw new LythonRuntimeException("ValueError", "'=' alignment requires a numeric value.", span);
        }

        var padding = width - text.Length;
        var padText = new string(fill, padding);
        return align switch
        {
            '<' => text + padText,
            '>' => padText + text,
            '^' => new string(fill, padding / 2) + text + new string(fill, padding - padding / 2),
            '=' => text[..numericPrefixLength] + padText + text[numericPrefixLength..],
            _ => throw new LythonRuntimeException("ValueError", $"Unknown alignment option '{align}'.", span)
        };
    }

    private static string FormatNumericSign(bool negative, char? sign)
        => negative
            ? "-"
            : sign switch
            {
                '+' => "+",
                ' ' => " ",
                _ => string.Empty
            };

    private static string ApplyNumericSign(string text, char? sign)
    {
        if (text.StartsWith("-", StringComparison.Ordinal) ||
            text.StartsWith("+", StringComparison.Ordinal) ||
            text.StartsWith(" ", StringComparison.Ordinal))
        {
            return text;
        }

        return sign switch
        {
            '+' => "+" + text,
            ' ' => " " + text,
            _ => text
        };
    }

    private static int GetNumericPrefixLength(string text)
        => text.Length > 0 && text[0] is '-' or '+' or ' ' ? 1 : 0;

    private static string ToUnsignedBaseString(BigInteger value, int radix, bool upper)
    {
        if (value.IsZero)
        {
            return "0";
        }

        const string lowerDigits = "0123456789abcdef";
        const string upperDigits = "0123456789ABCDEF";
        var digits = upper ? upperDigits : lowerDigits;
        var builder = new StringBuilder();
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, radix, out var remainder);
            builder.Append(digits[(int)remainder]);
        }

        for (var left = 0; left < builder.Length / 2; left++)
        {
            var right = builder.Length - left - 1;
            (builder[left], builder[right]) = (builder[right], builder[left]);
        }

        return builder.ToString();
    }

    private static string GroupDigits(string digits, char separator)
    {
        var builder = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append(separator);
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    private static string GroupFloatingDigits(string text, char separator)
    {
        var signLength = GetNumericPrefixLength(text);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissaEnd = exponentIndex >= 0 ? exponentIndex : text.Length;
        var decimalIndex = text.IndexOf('.');
        if (decimalIndex < 0 || decimalIndex > mantissaEnd)
        {
            decimalIndex = mantissaEnd;
        }

        var grouped = GroupDigits(text[signLength..decimalIndex], separator);
        return text[..signLength] + grouped + text[decimalIndex..];
    }

    private sealed record InterpolatedFormatSpecifier(
        char? Fill,
        char? Align,
        char? Sign,
        bool Alternate,
        bool ZeroPad,
        int? Width,
        char? Grouping,
        int? Precision,
        char? Type);

}
