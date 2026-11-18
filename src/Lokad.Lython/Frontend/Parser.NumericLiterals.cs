using System.Globalization;
using System.Numerics;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private IntegerLiteralExpressionSyntax ParseIntegerLiteral(int tokenIndex)
    {
        var text = _tokens.GetSpan(tokenIndex);
        var bitsPerDigit = text.Length > 2 && text[0] == '0' ? text[1] switch
        {
            'x' or 'X' => 4,
            'o' or 'O' => 3,
            'b' or 'B' => 1,
            _ => 0,
        } : 0;
        var hasSeparators = text.Contains('_');
        // Keep the spelling distinction used by conservative branch joins.
        // Ordinary decimal tokens and inferred constants need only a value.
        var spelling = hasSeparators || text.Length > 1 && text[0] == '0'
            ? text.ToString() : null;
        var value = bitsPerDigit == 0
            ? BigInteger.Parse(hasSeparators ? spelling!.Replace("_", "", StringComparison.Ordinal).AsSpan() : text,
                NumberStyles.Integer, CultureInfo.InvariantCulture)
            : ParsePrefixedInteger(text[2..], bitsPerDigit);
        return new IntegerLiteralExpressionSyntax(value, spelling, bitsPerDigit == 0, hasSeparators, SpanOf(tokenIndex));
    }

    private BigInteger ParsePrefixedInteger(ReadOnlySpan<char> digits, int bitsPerDigit)
    {
        // The tokenizer already validated each digit. Pack directly into the
        // compilation owner's admitted scratch instead of multiplying a
        // growing BigInteger for every digit.
        var capacity = checked((int)(((long)digits.Length * bitsPerDigit + 7) / 8));
        Span<byte> bytes = capacity <= 32 ? stackalloc byte[capacity] : new byte[capacity];
        var written = 0;
        var pending = 0;
        var pendingBits = 0;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            PythonTokenization.CheckCancellation(_cancellationToken, i);
            var digit = digits[i];
            if (digit == '_') continue;
            var value = digit <= '9' ? digit - '0' : (digit | 32) - 'a' + 10;
            pending |= value << pendingBits;
            pendingBits += bitsPerDigit;
            if (pendingBits < 8) continue;
            bytes[written++] = (byte)pending;
            pending >>= 8;
            pendingBits -= 8;
        }
        if (pendingBits != 0) bytes[written++] = (byte)pending;
        return new BigInteger(bytes[..written], isUnsigned: true);
    }

    private static double ParseFloatingLiteral(string spelling, bool imaginary)
    {
        var text = spelling.Replace("_", "", StringComparison.Ordinal).AsSpan();
        return double.Parse(imaginary ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
