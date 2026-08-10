using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Round(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "round(number[, ndigits]) expects one or two arguments.", span);
        }

        var digits = arguments.Length == 2 && arguments[1] is not PyNone
            ? ToInt32(ExpectBuiltinInteger(arguments[1], "round(number[, ndigits]) expects ndigits to be an integer.", span), "round(number[, ndigits])", span)
            : (int?)null;

        return arguments[0] switch
        {
            bool boolean => RoundInteger(boolean ? BigInteger.One : BigInteger.Zero, digits, span),
            BigInteger integer => RoundInteger(integer, digits, span),
            double floating => RoundFloat(floating, digits, span),
            PyDecimal decimalValue => RoundDecimal(decimalValue, digits, context.DecimalContext, span),
            _ => throw new LythonRuntimeException("TypeError", "round(number[, ndigits]) expects a numeric value.", span)
        };

        static object RoundInteger(BigInteger value, int? digits, LythonSourceSpan span)
        {
            if (digits is null || digits >= 0)
            {
                return value;
            }

            if (value.IsZero)
            {
                return BigInteger.Zero;
            }

            var places = -(long)digits.Value;
            var decimalDigitUpperBound = (long)Math.Ceiling(BigInteger.Abs(value).GetBitLength() * Math.Log10(2.0));
            if (places > decimalDigitUpperBound)
            {
                return BigInteger.Zero;
            }

            if (places > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "round() ndigits is too large.", span);
            }

            var factor = BigInteger.Pow(10, (int)places);
            var sign = value < BigInteger.Zero ? -1 : 1;
            var quotient = BigInteger.DivRem(BigInteger.Abs(value), factor, out var remainder);
            var comparison = (remainder * 2).CompareTo(factor);
            if (comparison > 0 || (comparison == 0 && !quotient.IsEven))
            {
                quotient += BigInteger.One;
            }

            return quotient * factor * sign;
        }

        static object RoundDecimal(PyDecimal value, int? digits, PyDecimalContext context, LythonSourceSpan span)
        {
            if (digits is null)
            {
                return new BigInteger(decimal.Round(value.Value, 0, MidpointRounding.ToEven));
            }

            if (digits is >= 0 and <= 28)
            {
                return new PyDecimal(PyDecimalOps.Round(value.Value, digits.Value, PyNone.Instance, context, span));
            }

            if (digits > 28)
            {
                return value;
            }

            var exponent = -(long)digits.Value;
            if (exponent > 29)
            {
                return new PyDecimal(decimal.Zero);
            }

            if (exponent == 29)
            {
                const decimal half = 50_000_000_000_000_000_000_000_000_000m;
                if (decimal.Abs(value.Value) <= half)
                {
                    return new PyDecimal(decimal.Zero);
                }

                throw new LythonRuntimeException("OverflowError", "rounded decimal value is outside Lython's decimal range.", span);
            }

            var factor = 1m;
            for (var i = 0; i < exponent; i++)
            {
                factor *= 10m;
            }

            return new PyDecimal(PyDecimalOps.Round(value.Value / factor, 0, PyNone.Instance, context, span) * factor);
        }

        static object RoundFloat(double value, int? digits, LythonSourceSpan span)
        {
            if (digits is null)
            {
                return FloatToInteger(value, "round", span, static number => Math.Round(number, MidpointRounding.ToEven));
            }

            try
            {
                return RoundToDigits(value, digits.Value);
            }
            catch (OverflowException ex)
            {
                throw new LythonRuntimeException("OverflowError", ex.Message, span);
            }

            static double RoundToDigits(double value, int digits)
            {
                if (!double.IsFinite(value)
                    || value == 0.0
                    || digits > 323
                    || (digits >= 0 && Math.Abs(value) >= 9_007_199_254_740_992.0))
                {
                    return value;
                }

                if (digits < -308)
                {
                    return Math.CopySign(0.0, value);
                }

                // A binary64 value is an exact integer times a power of two. Round that
                // rational value directly so decimal halfway decisions do not inherit
                // System.Math.Round's decimal-scaling approximation (for example 2.675).
                var bits = (ulong)BitConverter.DoubleToInt64Bits(Math.Abs(value));
                var exponentBits = (int)((bits >> 52) & 0x7FF);
                var fraction = bits & 0x000F_FFFF_FFFF_FFFF;
                var significand = exponentBits == 0 ? fraction : fraction | 0x0010_0000_0000_0000;
                var binaryExponent = exponentBits == 0 ? -1074 : exponentBits - 1023 - 52;

                var numerator = new BigInteger(significand);
                var denominator = BigInteger.One;
                if (digits >= 0)
                {
                    numerator *= BigInteger.Pow(5, digits);
                }
                else
                {
                    denominator = BigInteger.Pow(5, -digits);
                }

                binaryExponent += digits;
                if (binaryExponent >= 0)
                {
                    numerator <<= binaryExponent;
                }
                else
                {
                    denominator <<= -binaryExponent;
                }

                var rounded = BigInteger.DivRem(numerator, denominator, out var remainder);
                var midpointComparison = (remainder * 2).CompareTo(denominator);
                if (midpointComparison > 0 || (midpointComparison == 0 && !rounded.IsEven))
                {
                    rounded += BigInteger.One;
                }

                // Parsing the exact decimal result delegates the final binary64 choice to
                // the runtime's correctly-rounded parser, including subnormal results. The
                // cutoffs above bound this representation to 344 characters.
                Span<char> text = stackalloc char[344];
                if (!rounded.TryFormat(text, out var length, default, CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("rounded float text exceeded its proven bound");
                }

                text[length++] = 'e';
                if (!(-digits).TryFormat(text[length..], out var exponentLength, default, CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("rounded float exponent exceeded its proven bound");
                }

                length += exponentLength;
                var result = double.Parse(text[..length], NumberStyles.Float, CultureInfo.InvariantCulture);
                if (!double.IsFinite(result))
                {
                    throw new OverflowException("rounded value too large to represent");
                }

                return Math.CopySign(result, value);
            }
        }
    }
}
