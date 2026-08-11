using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PercentSpecifierParser(
        string format,
        Func<BigInteger> nextStarInteger,
        LythonSourceSpan span)
    {
        public PercentSpecifier Parse(ref int index)
        {
            if (index >= format.Length)
            {
                throw ValueError("incomplete format");
            }

            var mappingKey = format[index] == '(' ? ParseMappingKey(ref index) : null;
            var alternate = false;
            var zeroPad = false;
            var leftAdjust = false;
            var plusSign = false;
            var spaceSign = false;
            while (index < format.Length)
            {
                switch (format[index])
                {
                    case '#':
                        alternate = true;
                        index++;
                        continue;
                    case '0':
                        zeroPad = true;
                        index++;
                        continue;
                    case '-':
                        leftAdjust = true;
                        index++;
                        continue;
                    case '+':
                        plusSign = true;
                        index++;
                        continue;
                    case ' ':
                        spaceSign = true;
                        index++;
                        continue;
                }

                break;
            }

            int? width = null;
            if (index < format.Length && format[index] == '*')
            {
                index++;
                var starWidth = nextStarInteger();
                if (starWidth < 0)
                {
                    leftAdjust = true;
                    starWidth = -starWidth;
                }

                width = ToFormatSize(starWidth, "width");
            }
            else if (index < format.Length && char.IsAsciiDigit(format[index]))
            {
                width = ParseFormatSize(ref index, "width");
            }

            int? precision = null;
            if (index < format.Length && format[index] == '.')
            {
                index++;
                if (index < format.Length && format[index] == '*')
                {
                    index++;
                    precision = ToFormatSize(BigInteger.Max(BigInteger.Zero, nextStarInteger()), "precision");
                }
                else
                {
                    precision = index < format.Length && char.IsAsciiDigit(format[index])
                        ? ParseFormatSize(ref index, "precision")
                        : 0;
                }
            }

            while (index < format.Length && format[index] is 'h' or 'l' or 'L')
            {
                index++;
            }

            if (index >= format.Length)
            {
                throw ValueError("incomplete format");
            }

            var conversion = format[index++];
            if (conversion is not ('s' or 'r' or 'a' or 'd' or 'i' or 'u' or 'o' or 'x' or 'X' or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or 'c'))
            {
                throw ValueError($"unsupported format character '{conversion}'");
            }

            return new PercentSpecifier(
                mappingKey,
                alternate,
                zeroPad,
                leftAdjust,
                plusSign,
                spaceSign,
                width,
                precision,
                conversion);
        }

        private string ParseMappingKey(ref int index)
        {
            index++;
            var start = index;
            var depth = 1;
            while (index < format.Length)
            {
                if (format[index] == '(')
                {
                    depth++;
                }
                else if (format[index] == ')' && --depth == 0)
                {
                    var key = format[start..index];
                    index++;
                    return key;
                }

                index++;
            }

            throw ValueError("incomplete format key");
        }

        private int ParseFormatSize(ref int index, string owner)
        {
            var value = BigInteger.Zero;
            while (index < format.Length && char.IsAsciiDigit(format[index]))
            {
                value = value * 10 + (format[index] - '0');
                index++;
                if (value > int.MaxValue)
                {
                    throw ValueError($"{owner} too big");
                }
            }

            return (int)value;
        }

        private int ToFormatSize(BigInteger value, string owner)
        {
            if (value > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", $"{owner} too big", span);
            }

            return (int)value;
        }

        private LythonRuntimeException ValueError(string message)
            => new("ValueError", message, span);
    }
}
