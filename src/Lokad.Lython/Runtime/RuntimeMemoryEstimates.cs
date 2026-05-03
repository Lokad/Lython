using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static class RuntimeMemoryEstimates
{
    private const long BigIntegerObjectOverheadBytes = 32;

    public static long EstimateBigIntegerBytes(BigInteger value)
        => BigIntegerObjectOverheadBytes + value.GetByteCount();

    public static long EstimateBigIntegerBytesFromBitCount(long bitCount)
    {
        if (bitCount <= 0)
        {
            return EstimateBigIntegerBytes(BigInteger.Zero);
        }

        var payloadBytes = SaturatingAdd(bitCount, 7) / 8;
        return SaturatingAdd(BigIntegerObjectOverheadBytes, payloadBytes);
    }

    public static long GetMagnitudeBitLength(BigInteger value)
    {
        value = BigInteger.Abs(value);
        if (value.IsZero)
        {
            return 0;
        }

        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var leading = bytes[0];
        var leadingBits = 8;
        while ((leading & 0x80) == 0)
        {
            leading <<= 1;
            leadingBits--;
        }

        return ((long)bytes.Length - 1) * 8 + leadingBits;
    }

    public static long SaturatingAdd(long left, long right)
    {
        if (left > long.MaxValue - right)
        {
            return long.MaxValue;
        }

        return left + right;
    }

    public static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        if (left > long.MaxValue / right)
        {
            return long.MaxValue;
        }

        return left * right;
    }
}
