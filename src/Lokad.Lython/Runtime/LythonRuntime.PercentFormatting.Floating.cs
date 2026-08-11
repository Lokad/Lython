using System.Globalization;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static class PercentFloatingFormatter
    {
        public static string Format(double value, PercentSpecifier specifier)
        {
            var upper = specifier.Conversion is 'E' or 'F' or 'G';
            if (double.IsNaN(value))
            {
                return upper ? "NAN" : "nan";
            }

            if (double.IsPositiveInfinity(value))
            {
                return upper ? "INF" : "inf";
            }

            var precision = specifier.Precision ?? 6;
            return specifier.Conversion switch
            {
                'f' or 'F' => FormatFixed(value, precision, specifier.Alternate),
                'e' or 'E' => FormatExponential(value, precision, specifier.Alternate, upper),
                'g' or 'G' => FormatGeneral(value, precision, specifier.Alternate, upper),
                _ => string.Empty,
            };
        }

        private static string FormatFixed(double value, int precision, bool alternate)
        {
            var text = value.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return alternate && precision == 0 ? text + "." : text;
        }

        private static string FormatExponential(double value, int precision, bool alternate, bool upper)
        {
            var text = value.ToString("E" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var exponentIndex = text.IndexOf('E');
            var mantissa = text[..exponentIndex];
            if (alternate && precision == 0)
            {
                mantissa += ".";
            }

            var exponent = int.Parse(text[(exponentIndex + 1)..], CultureInfo.InvariantCulture);
            return mantissa + (upper ? "E" : "e") + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string FormatGeneral(double value, int precision, bool alternate, bool upper)
        {
            precision = precision == 0 ? 1 : precision;
            var rounded = double.Parse(
                value.ToString("G" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            var exponent = rounded == 0.0 ? 0 : (int)Math.Floor(Math.Log10(rounded));
            var scientific = exponent < -4 || exponent >= precision;
            if (!scientific)
            {
                var decimals = Math.Max(0, precision - exponent - 1);
                var fixedText = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                var integerDigits = fixedText.TrimStart('0').TakeWhile(char.IsAsciiDigit).Count();
                if (integerDigits <= precision)
                {
                    return alternate ? EnsureDecimalPoint(fixedText) : TrimFractionZeros(fixedText);
                }
            }

            var exponential = FormatExponential(value, precision - 1, alternate, upper);
            if (alternate)
            {
                return exponential;
            }

            var marker = upper ? 'E' : 'e';
            var markerIndex = exponential.IndexOf(marker);
            return TrimFractionZeros(exponential[..markerIndex]) + exponential[markerIndex..];
        }

        private static string EnsureDecimalPoint(string value)
            => value.Contains('.', StringComparison.Ordinal) ? value : value + ".";

        private static string TrimFractionZeros(string value)
            => value.Contains('.', StringComparison.Ordinal)
                ? value.TrimEnd('0').TrimEnd('.')
                : value;
    }
}
