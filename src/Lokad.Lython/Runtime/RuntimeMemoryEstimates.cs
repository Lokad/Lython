using System.Numerics;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal static class RuntimeMemoryEstimates
{
    private const long BigIntegerObjectOverheadBytes = 32;

    // A retained numeric slot pays a conservative box allowance without an
    // identity map. Large magnitudes retain their existing payload owner;
    // only an unowned magnitude needs its byte-count estimate here.
    internal const long ScalarStorageBytes = 64;

    // Mutable storage releases by stored type, independent of payload ownership.
    internal static long EstimateNumericSlotBytes(object? value) =>
        PyNumberOps.IsInteger(value) || value is double or PyDecimal ? ScalarStorageBytes : 0;

    internal static long EstimateScalarStorageBytes(object? value) => value switch
    {
        BigInteger integer => integer.GetByteCount() > 9 && !ChargeReclamationPool.IsTrackedValue(value)
            ? ScalarStorageBytes + integer.GetByteCount() : ScalarStorageBytes,
        _ => EstimateNumericSlotBytes(value),
    };

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
