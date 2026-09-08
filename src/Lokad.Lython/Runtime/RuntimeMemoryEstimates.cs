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
        // R42: allocation-free magnitude bit-length. Abs plus GetBitLength
        // matches the previous ToByteArray scan on zero, negatives, and powers
        // of two without allocating in proportion to the operand.
        => BigInteger.Abs(value).GetBitLength();

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
