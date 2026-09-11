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

        var spec = ParseInterpolatedFormatSpecifier(formatSpecifier, value, span);
        if (TryFormatNumericValue(value, spec, context, span, out var numericText, out var numericPrefixLength))
        {
            return ApplyInterpolatedFormatPadding(numericText, spec, numericPrefixLength, numeric: true, span, context.MemoryGovernor);
        }

        // CPython's default __format__ only accepts the empty spec; any
        // other spec on a non-string value reports TypeError naming the
        // type instead of formatting str(value).
        if (value is not PyString && formatSpecifier.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"unsupported format string passed to {RuntimeErrors.OperandTypeName(value)}.__format__", span);
        }

        if (spec.Grouping is { } stringGrouping)
        {
            throw new LythonRuntimeException("ValueError", $"Cannot specify '{stringGrouping}' with '{spec.Type ?? 's'}'.", span);
        }

        if (spec.Type is not null and not 's')
        {
            throw new LythonRuntimeException("ValueError", $"Unknown format code '{spec.Type}' for object of type '{RuntimeErrors.OperandTypeName(value)}'", span);
        }

        if (spec.Sign is '+' or '-')
        {
            throw new LythonRuntimeException("ValueError", "Sign not allowed in string format specifier", span);
        }

        if (spec.Sign is ' ')
        {
            throw new LythonRuntimeException("ValueError", "Space not allowed in string format specifier", span);
        }

        if (spec.Alternate)
        {
            throw new LythonRuntimeException("ValueError", "Alternate form (#) not allowed in string format specifier", span);
        }

        if (spec.Align == '=')
        {
            throw new LythonRuntimeException("ValueError", "'=' alignment not allowed in string format specifier", span);
        }

        var text = value switch
        {
            PyString pyString => pyString.AsString(),
            _ => ToInterpolatedString(value, context)
        };

        if (spec.Precision is { } precision)
        {
            text = text.Length <= precision ? text : text[..precision];
        }

        return ApplyInterpolatedFormatPadding(text, spec, numericPrefixLength: 0, numeric: false, span, context.MemoryGovernor);
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
            if (spec.Type is 'f' or 'F' or 'g' or 'G' or '%' or 'e' or 'E')
            {
                // Like CPython's int-to-double conversion, integers past
                // the double range fail explicitly instead of rendering
                // infinity.
                var asDouble = (double)integer;
                if (!double.IsFinite(asDouble))
                {
                    throw new LythonRuntimeException("OverflowError", "int too large to convert to float", span);
                }

                return TryFormatFloatingValue(asDouble, spec, span, context.MemoryGovernor, out text, out numericPrefixLength, RuntimeErrors.OperandTypeName(value));
            }

            text = FormatIntegerValue(integer, spec, span, RuntimeErrors.OperandTypeName(value), out numericPrefixLength);
            return true;
        }

        if (value is double floating)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Unknown format code '{spec.Type}' for object of type '{RuntimeErrors.OperandTypeName(floating)}'", span);
            }

            return TryFormatFloatingValue(floating, spec, span, context.MemoryGovernor, out text, out numericPrefixLength, RuntimeErrors.OperandTypeName(floating));
        }

        if (value is PyDecimal decimalValue)
        {
            return TryFormatDecimalValue(decimalValue, spec, span, context, out text, out numericPrefixLength);
        }

        _ = context;
        return false;
    }

    // Decimal has its own format grammar: exact coefficient rendering with
    // the ambient context rounding, per-code default precisions, single-digit
    // exponents and 'invalid format string' failures.
    private static bool TryFormatDecimalValue(
        PyDecimal decimalValue,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        ExecutionContext context,
        out string text,
        out int numericPrefixLength)
    {
        text = string.Empty;
        numericPrefixLength = 0;

        var type = spec.Type;
        var supported = type is null or 'f' or 'F' or 'e' or 'E' or 'g' or 'G' or 'n' or '%';
        if (!supported || spec.Grouping == '_' || (type == 'n' && spec.Grouping == ','))
        {
            throw new LythonRuntimeException("ValueError", "invalid format string", span);
        }

        // Guest-controlled precision scales the output without bound from a
        // tiny input, so bound it before materializing digit runs.
        if (context.MemoryGovernor is not null && spec.Precision is { } digits && digits > 1024)
        {
            var headroom = type is null or 'g' or 'G' or 'n' ? 16L : 320L;
            context.MemoryGovernor.EnsureCanReserve(32L + headroom + digits, span);
        }

        var tuple = PyDecimalOps.AsTuple(decimalValue);
        var negative = tuple.Sign == 1;
        var coefficient = PyDecimalOps.DigitsToString(tuple.Digits);
        var exponent = decimalValue.Exponent;
        var isZero = coefficient is "0";
        var rounding = context.DecimalContext.Rounding;

        string body = type switch
        {
            '%' => RenderDecimalPercent(coefficient, exponent, isZero, spec, rounding, negative, span),
            'e' or 'E' => RenderDecimalScientific(coefficient, exponent, isZero, spec.Precision is null ? null : spec.Precision.Value + 1, spec.Alternate, upper: type == 'E', rounding, negative, span),
            'g' or 'G' or 'n' => RenderDecimalGeneral(coefficient, exponent, isZero, spec, upper: type == 'G', rounding, negative, span),
            null => RenderDecimalDefault(decimalValue, spec),
            _ => RenderDecimalFixed(coefficient, exponent, isZero, spec.Precision, spec.Alternate, rounding, negative, span),
        };

        if (negative && !body.StartsWith('-'))
        {
            body = "-" + body;
        }

        if (spec.Grouping == ',')
        {
            body = GroupFloatingDigits(body, ',');
        }

        text = ApplyNumericSign(body, spec.Sign);
        numericPrefixLength = GetNumericPrefixLength(text);
        return true;
    }

    private static string RenderDecimalDefault(PyDecimal decimalValue, InterpolatedFormatSpecifier spec)
    {
        var body = PyDecimalOps.Format(decimalValue);
        if (spec.Alternate && !body.Contains('.'))
        {
            var marker = body.IndexOf('E');
            body = marker < 0 ? body + "." : body[..marker] + "." + body[marker..];
        }

        return body;
    }

    // Unsigned fixed-point body. A null precision renders the natural scale.
    private static string RenderDecimalFixed(
        string coefficient,
        int exponent,
        bool isZero,
        int? precision,
        bool alternate,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
    {
        string digits;
        int point;
        if (precision is null)
        {
            digits = coefficient;
            point = coefficient.Length + exponent;
        }
        else
        {
            (digits, var scaled) = RoundToFractionDigits(coefficient, exponent, isZero, precision.Value, rounding, negative, span);
            point = digits.Length + scaled;
        }

        string body;
        if (point <= 0)
        {
            body = "0." + new string('0', -point) + digits;
        }
        else if (point >= digits.Length)
        {
            body = StripLeadingZeros(digits + new string('0', point - digits.Length));
        }
        else
        {
            body = digits[..point] + "." + digits[point..];
        }

        if (alternate && !body.Contains('.'))
        {
            body += ".";
        }

        return body;
    }

    private static string RenderDecimalPercent(
        string coefficient,
        int exponent,
        bool isZero,
        InterpolatedFormatSpecifier spec,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
        => RenderDecimalFixed(coefficient, exponent + 2, isZero, spec.Precision, spec.Alternate, rounding, negative, span) + "%";

    // Unsigned scientific body with single-digit-minimum Decimal exponents.
    private static string RenderDecimalScientific(
        string coefficient,
        int exponent,
        bool isZero,
        int? precision,
        bool alternate,
        bool upper,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
    {
        var keep = precision ?? coefficient.Length;
        if (keep < 1)
        {
            keep = 1;
        }

        string digits;
        int scaled;
        if (isZero)
        {
            digits = new string('0', keep);
            scaled = exponent;
        }
        else
        {
            (digits, scaled) = RoundSignificant(coefficient, exponent, keep, rounding, negative, span);
        }

        return RenderDecimalMantissa(digits, scaled, alternate, upper);
    }

    private static string RenderDecimalMantissa(string digits, int scaled, bool alternate, bool upper)
    {
        var mantissa = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
        if (alternate && digits.Length == 1)
        {
            mantissa += ".";
        }

        var shown = scaled + digits.Length - 1;
        return mantissa + (upper ? 'E' : 'e') + (shown < 0 ? "-" : "+") + Math.Abs(shown).ToString(CultureInfo.InvariantCulture);
    }

    // Unsigned general body: an explicit precision rounds first and then
    // picks fixed/scientific from the rounded digits; the default shows
    // every digit with the str() cut. Zeros are never padded or stripped
    // here, so exactly the significant digits survive.
    private static string RenderDecimalGeneral(
        string coefficient,
        int exponent,
        bool isZero,
        InterpolatedFormatSpecifier spec,
        bool upper,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
    {
        if (spec.Precision is null)
        {
            var spontaneous = isZero ? exponent : coefficient.Length - 1 + exponent;
            var body = exponent > 0 || spontaneous < -6
                ? RenderDecimalScientific(coefficient, exponent, isZero, null, alternate: false, upper, rounding, negative, span)
                : RenderDecimalFixed(coefficient, exponent, isZero, null, alternate: false, rounding, negative, span);
            return ForceGeneralPoint(body, spec.Alternate);
        }

        var keep = spec.Precision.Value < 1 ? 1 : spec.Precision.Value;
        var effective = isZero ? 1 : Math.Min(keep, coefficient.Length);
        var (digits, scaled) = RoundSignificant(coefficient, exponent, effective, rounding, negative, span);

        var adjusted = digits.Length - 1 + scaled;
        if (exponent > 0 || adjusted < -6 || adjusted >= keep)
        {
            return ForceGeneralPoint(RenderDecimalMantissa(digits, scaled, spec.Alternate, upper), spec.Alternate);
        }

        int? fraction = isZero ? null : keep - (adjusted + 1);
        var fixedBody = RenderDecimalFixed(digits, scaled, isZero, fraction, alternate: false, rounding, negative, span);
        if (isZero)
        {
            return ForceGeneralPoint(fixedBody, spec.Alternate);
        }

        var floor = Math.Max(0, digits.Length - (adjusted + 1));
        return ForceGeneralPoint(StripToFloor(fixedBody, floor, spec.Alternate), spec.Alternate);
    }

    private static string StripToFloor(string body, int floor, bool alternate)
    {
        var point = body.IndexOf('.');
        if (point < 0)
        {
            return body;
        }

        var end = body.Length;
        var kept = body.Length - point - 1;
        while (kept > floor && body[end - 1] == '0')
        {
            end--;
            kept--;
        }

        var trimmed = body[..end];
        if (trimmed.EndsWith('.') && !alternate)
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }

    private static string ForceGeneralPoint(string body, bool alternate)
    {
        if (!alternate || body.Contains('.'))
        {
            return body;
        }

        var marker = body.IndexOfAny(['e', 'E']);
        return marker < 0 ? body + "." : body[..marker] + "." + body[marker..];
    }

    private static string StripLeadingZeros(string digits)
    {
        var stripped = digits.TrimStart('0');
        return stripped.Length == 0 ? "0" : stripped;
    }

    private static (string Digits, int Exponent) RoundToFractionDigits(
        string coefficient,
        int exponent,
        bool isZero,
        int precision,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
    {
        if (isZero)
        {
            return ("0", -precision);
        }

        var shift = exponent + precision;
        if (shift >= 0)
        {
            return (coefficient + new string('0', shift), -precision);
        }

        var keep = coefficient.Length + shift;
        if (keep < 1)
        {
            coefficient = new string('0', 1 - keep) + coefficient;
            keep = 1;
        }

        return RoundSignificant(coefficient, exponent, keep, rounding, negative, span);
    }

    // Rounds the coefficient to `keep` significant digits (keep >= 1),
    // shifting the exponent so the value is preserved (zeros keep theirs).
    private static (string Digits, int Exponent) RoundSignificant(
        string coefficient,
        int exponent,
        int keep,
        DecimalRoundingMode rounding,
        bool negative,
        LythonSourceSpan span)
    {
        if (coefficient is "0")
        {
            return ("0", exponent);
        }

        if (keep >= coefficient.Length)
        {
            var pad = keep - coefficient.Length;
            return (pad == 0 ? coefficient : coefficient + new string('0', pad), exponent - pad);
        }

        var head = coefficient[..keep];
        var scaled = exponent + (coefficient.Length - keep);
        if (RoundUp(head[^1] - '0', coefficient[keep..], rounding, negative, span))
        {
            head = IncrementDigits(head);
            if (head.Length > keep)
            {
                head = head[..^1];
                scaled += 1;
            }
        }

        return (head, scaled);
    }

    private static bool RoundUp(int lastKept, string tail, DecimalRoundingMode rounding, bool negative, LythonSourceSpan span)
    {
        var first = tail[0] - '0';
        var sticky = false;
        for (var i = 1; i < tail.Length; i++)
        {
            if (tail[i] != '0')
            {
                sticky = true;
                break;
            }
        }

        return rounding switch
        {
            DecimalRoundingMode.HalfEven => first > 5 || (first == 5 && (sticky || lastKept % 2 == 1)),
            DecimalRoundingMode.HalfUp => first >= 5,
            DecimalRoundingMode.HalfDown => first > 5 || (first == 5 && sticky),
            DecimalRoundingMode.Down => false,
            DecimalRoundingMode.Up => first > 0 || sticky,
            DecimalRoundingMode.Ceiling => !negative && (first > 0 || sticky),
            DecimalRoundingMode.Floor => negative && (first > 0 || sticky),
            DecimalRoundingMode.ZeroFiveUp => (first > 0 || sticky) && lastKept is 0 or 5,
            _ => throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span),
        };
    }

    private static string IncrementDigits(string head)
    {
        var chars = head.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] != '9')
            {
                chars[i]++;
                return new string(chars);
            }

            chars[i] = '0';
        }

        return "1" + new string(chars);
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
        string typeName,
        out int numericPrefixLength)
    {
        if (spec.Precision is not null)
        {
            throw new LythonRuntimeException("ValueError", "Precision not allowed in integer format specifier", span);
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
                throw new LythonRuntimeException("ValueError", $"Unknown format code '{type}' for object of type '{typeName}'", span);
        }

        if (spec.Grouping is { } grouping)
        {
            if (type is not ('d' or 'n'))
            {
                throw new LythonRuntimeException("ValueError", $"Cannot specify '{grouping}' with '{type}'.", span);
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
        MemoryGovernor? governor,
        out string text,
        out int numericPrefixLength,
        string typeName)
    {
        var type = spec.Type;
        var precision = spec.Precision;
        if (type is 'b' or 'o' or 'x' or 'X' or 'd' or 'n')
        {
            text = string.Empty;
            numericPrefixLength = 0;
            return false;
        }

        if (spec.Grouping is { } floatGrouping &&
            type is not null and not 'f' and not 'F' and not 'g' and not 'G' and not '%' and not 'e' and not 'E')
        {
            throw new LythonRuntimeException("ValueError", $"Cannot specify '{floatGrouping}' with '{type}'.", span);
        }

        if (spec.Alternate && spec.Type is 'g' or 'G')
        {
            throw new LythonRuntimeException("ValueError", "Alternate floating-point formatting is not supported.", span);
        }

        if (!double.IsFinite(value))
        {
            var nonFinite = double.IsNaN(value) ? "nan" : value < 0 ? "-inf" : "inf";
            if (spec.Type is 'F' or 'E' or 'G')
            {
                nonFinite = nonFinite.ToUpperInvariant();
            }
            if (spec.Type == '%')
            {
                nonFinite += "%";
            }

            text = ApplyNumericSign(nonFinite, spec.Sign);
            numericPrefixLength = GetNumericPrefixLength(text);
            return true;
        }

        // Guest-controlled precision scales the output without bound from a
        // tiny input, so bound it before BCL formatting materializes it.
        // Small precisions behave exactly as before; non-finite values
        // always render short. Integer digits of a double need at most 309
        // chars; significant-digit codes need at most precision plus a few.
        if (governor is not null && double.IsFinite(value) && spec.Precision is { } digits && digits > 1024)
        {
            var headroom = spec.Type is null or 'g' or 'G' ? 16L : 320L;
            governor.EnsureCanReserve(32L + headroom + digits, span);
        }

        var formatted = type switch
        {
            null when precision is null => value.ToString(CultureInfo.InvariantCulture),
            null => value.ToString("G" + precision.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'f' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'F' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'g' => NormalizeExponentMarker(value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), upper: false),
            'G' => NormalizeExponentMarker(value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), upper: true),
            'e' => NormalizeExponentMarker(value.ToString("E" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), upper: false),
            'E' => NormalizeExponentMarker(value.ToString("E" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), upper: true),
            '%' => FormatPercentText(value, precision),
            _ => throw new LythonRuntimeException("ValueError", $"Unknown format code '{type}' for object of type '{typeName}'", span)
        };

        if (type == 'F')
        {
            formatted = formatted.ToUpperInvariant();
        }

        // Alternate form forces a decimal point when the rendering has
        // none (g/G stay rejected above, like before).
        // Null-type precision has its own significant-digits rendering
        // (a separate slice); the point-only rule below covers the
        // precision-free null type.
        var hashType = type is 'f' or 'F' or 'e' or 'E' or '%' || (type is null && precision is null);
        if (spec.Alternate && type is null && precision is null)
        {
            // CPython renders the no-type form float-style (lowercase
            // marker); the bare rendering keeps the shared BCL shape.
            formatted = formatted.Replace('E', 'e');
        }
        if (spec.Alternate && hashType && !formatted.Contains('.'))
        {
            var pointAt = formatted.IndexOfAny(['e', 'E']);
            if (pointAt < 0 && formatted.EndsWith('%'))
            {
                pointAt = formatted.Length - 1;
            }

            if (pointAt >= 0)
            {
                formatted = formatted[..pointAt] + "." + formatted[pointAt..];
            }
            else
            {
                // With no presentation type the point carries a zero so the
                // rendering still parses as a float.
                formatted += type is null ? ".0" : ".";
            }
        }

        if (spec.Grouping is { } grouping)
        {
            formatted = GroupFloatingDigits(formatted, grouping);
        }

        text = ApplyNumericSign(formatted, spec.Sign);
        numericPrefixLength = GetNumericPrefixLength(text);
        return true;
    }

    // Scaling by 100 can overflow a large finite value to infinity;
    // CPython then renders the non-finite word with the percent suffix.
    private static string FormatPercentText(double value, int? precision)
    {
        var scaled = value * 100.0;
        if (!double.IsFinite(scaled))
        {
            return (double.IsNaN(scaled) ? "nan" : scaled < 0 ? "-inf" : "inf") + "%";
        }

        return scaled.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";
    }

    // BCL scientific notation pads exponents to three digits with an
    // uppercase marker; CPython uses a lowercase marker for 'e'/'g' and a
    // minimum of two exponent digits.
    private static string NormalizeExponentMarker(string raw, bool upper)
    {
        var marker = raw.IndexOf('E');
        if (marker < 0)
        {
            return raw;
        }

        var digits = raw[(marker + 2)..];
        var stripped = digits.TrimStart('0');
        stripped = stripped.Length switch
        {
            0 => "00",
            1 => "0" + stripped,
            _ => stripped,
        };

        return raw[..marker] + (upper ? 'E' : 'e') + raw[marker + 1] + stripped;
    }

    private static InterpolatedFormatSpecifier ParseInterpolatedFormatSpecifier(string text, object value, LythonSourceSpan span)
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
                throw new LythonRuntimeException("ValueError", "Format specifier missing precision", span);
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
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}' for object of type '{RuntimeErrors.OperandTypeName(value)}'", span);
        }

        return new InterpolatedFormatSpecifier(fill, align, sign, alternate, zeroPad, width, grouping, precision, type);
    }

    private static bool IsFormatAlign(char value)
        => value is '<' or '>' or '^' or '=';

    private static string ApplyInterpolatedFormatPadding(
        string text,
        InterpolatedFormatSpecifier spec,
        int numericPrefixLength,
        bool numeric,
        LythonSourceSpan span,
        MemoryGovernor? governor)
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
        // Bound the expansion before any padding buffer is built: the result
        // is exactly the source text plus one fill per missing char, so a
        // hostile width fails here instead of materializing multi-megabyte
        // CLR strings ahead of the governed result charge (zfill shape).
        if (governor is not null)
        {
            var fillWidth = fill <= 0x7F ? 1L : fill <= 0x7FF ? 2L : 3L;
            governor.EnsureCanReserve(32L + Encoding.UTF8.GetByteCount(text) + (fillWidth * padding), span);
        }

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
        // A percent suffix is not part of the grouped digits.
        var suffix = text.EndsWith('%') ? "%" : string.Empty;
        var core = suffix.Length == 0 ? text : text[..^1];
        var signLength = GetNumericPrefixLength(core);
        var exponentIndex = core.IndexOfAny(['e', 'E']);
        var mantissaEnd = exponentIndex >= 0 ? exponentIndex : core.Length;
        var decimalIndex = core.IndexOf('.');
        if (decimalIndex < 0 || decimalIndex > mantissaEnd)
        {
            decimalIndex = mantissaEnd;
        }

        var grouped = GroupDigits(core[signLength..decimalIndex], separator);
        return core[..signLength] + grouped + core[decimalIndex..] + suffix;
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
