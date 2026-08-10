using System.Globalization;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDecimalOps
{
    public static PyString ToEngineeringString(PyDecimal value)
        => PyString.FromString(Format(value));

    public static string Format(PyDecimal value)
    {
        var tuple = AsTuple(value);
        var digits = DigitsToString(tuple.Digits);
        var negative = tuple.Sign == 1;
        var adjusted = value.Value == 0m ? value.Exponent : digits.Length + value.Exponent - 1;
        string body;
        if (value.Exponent > 0 || adjusted < -6)
        {
            body = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
            body += "E" + (adjusted >= 0 ? "+" : string.Empty) + adjusted.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var point = digits.Length + value.Exponent;
            if (point <= 0)
            {
                body = "0." + new string('0', -point) + digits;
            }
            else if (point >= digits.Length)
            {
                body = digits + new string('0', point - digits.Length);
            }
            else
            {
                body = digits[..point] + "." + digits[point..];
            }
        }

        return negative ? "-" + body : body;
    }

    public static string Format(decimal value) => Format(new PyDecimal(value));

    public static bool IsSigned(decimal value)
        => (decimal.GetBits(value)[3] & int.MinValue) != 0;

    public static int GetScale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 31;

    public static decimal Round(decimal value, int scale, object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        var mode = ResolveRounding(rounding, context, span);
        return mode switch
        {
            DecimalRoundingMode.HalfEven => decimal.Round(value, scale, MidpointRounding.ToEven),
            DecimalRoundingMode.HalfUp => RoundHalfUp(value, scale),
            DecimalRoundingMode.HalfDown => RoundHalfDown(value, scale),
            DecimalRoundingMode.Down => TruncateToScale(value, scale),
            DecimalRoundingMode.Up => AwayFromZeroToScale(value, scale),
            DecimalRoundingMode.Ceiling => CeilingToScale(value, scale),
            DecimalRoundingMode.Floor => FloorToScale(value, scale),
            DecimalRoundingMode.ZeroFiveUp => Round05Up(value, scale),
            _ => throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span),
        };
    }

    private static DecimalRoundingMode ResolveRounding(object? rounding, PyDecimalContext? context, LythonSourceSpan span)
    {
        if (rounding is null or PyNone)
        {
            return context?.Rounding ?? DecimalRoundingMode.HalfEven;
        }

        if (!PyStringOps.TryAsString(rounding, out var mode))
        {
            throw new LythonRuntimeException("TypeError", "Decimal rounding argument expects a rounding constant.", span);
        }

        return PyDecimalContext.ParseRoundingName(mode.AsString())
            ?? throw new LythonRuntimeException("ValueError", "Unsupported decimal rounding mode.", span);
    }

    private static decimal TruncateToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Truncate(value * factor) / factor;
    }

    private static decimal AwayFromZeroToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var truncated = decimal.Truncate(scaled);
        if (scaled == truncated)
        {
            return truncated / factor;
        }

        var adjusted = scaled > 0 ? truncated + 1 : truncated - 1;
        return adjusted / factor;
    }

    private static decimal CeilingToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Ceiling(value * factor) / factor;
    }

    private static decimal FloorToScale(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        return decimal.Floor(value * factor) / factor;
    }

    private static decimal RoundHalfUp(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var sign = Math.Sign(scaled);
        return sign >= 0
            ? decimal.Floor(scaled + 0.5m) / factor
            : decimal.Ceiling(scaled - 0.5m) / factor;
    }

    private static decimal RoundHalfDown(decimal value, int scale)
    {
        var factor = Pow(10m, scale);
        var scaled = value * factor;
        var truncated = decimal.Truncate(scaled);
        var fraction = decimal.Abs(scaled - truncated);
        if (fraction <= 0.5m)
        {
            return truncated / factor;
        }

        return (scaled > 0 ? truncated + 1 : truncated - 1) / factor;
    }

    private static decimal Round05Up(decimal value, int scale)
    {
        var truncated = TruncateToScale(value, scale);
        var factor = Pow(10m, scale);
        var lastKeptDigit = (int)(decimal.Abs(truncated * factor) % 10);
        return lastKeptDigit is 0 or 5 && truncated != value
            ? AwayFromZeroToScale(value, scale)
            : truncated;
    }
}
