using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyRandomState
{
    private const ulong DefaultSeed = 0x5A17_1C0D_DA7A_2026UL;
    private ulong _state;

    public PyRandomState()
    {
        Seed(DefaultSeed);
    }

    public void Seed(ulong seed)
    {
        _state = seed == 0 ? DefaultSeed : seed;
    }

    public double NextDouble()
    {
        // Match the usual Python contract: 0.0 <= x < 1.0.
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public ulong NextUInt64()
    {
        _state += 0x9E37_79B9_7F4A_7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D0_49BB_1331_11EBUL;
        return z ^ (z >> 31);
    }

    public ulong NextBelow(ulong upperExclusive)
    {
        if (upperExclusive == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(upperExclusive));
        }

        var threshold = unchecked((0UL - upperExclusive) % upperExclusive);
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
            {
                return value % upperExclusive;
            }
        }
    }

    public BigInteger GetRandBits(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        if (bitCount == 0)
        {
            return BigInteger.Zero;
        }

        var byteCount = (bitCount + 7) / 8;
        var bytes = new byte[byteCount + 1];
        var offset = 0;
        while (offset < byteCount)
        {
            var chunk = NextUInt64();
            for (var i = 0; i < 8 && offset < byteCount; i++, offset++)
            {
                bytes[offset] = (byte)(chunk & 0xFF);
                chunk >>= 8;
            }
        }

        var excessBits = (byteCount * 8) - bitCount;
        if (excessBits > 0)
        {
            bytes[byteCount - 1] &= (byte)(0xFF >> excessBits);
        }

        return new BigInteger(bytes);
    }
}
