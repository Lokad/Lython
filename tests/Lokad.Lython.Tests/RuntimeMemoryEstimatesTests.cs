using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// R42: magnitude bit-length is allocation-free and exact on zero, negatives,
/// and powers of two, so power/shift guards do not allocate in proportion to
/// the operand before deciding the result fits.
/// </summary>
public sealed class RuntimeMemoryEstimatesTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("-1", 1)]
    [InlineData("2", 2)]
    [InlineData("3", 2)]
    [InlineData("7", 3)]
    [InlineData("8", 4)]
    [InlineData("127", 7)]
    [InlineData("128", 8)]
    [InlineData("255", 8)]
    [InlineData("256", 9)]
    [InlineData("-8", 4)]
    [InlineData("-128", 8)]
    [InlineData("-129", 8)]
    public void MagnitudeBitLengthMatchesExactValues(string text, long expected)
    {
        Assert.Equal(expected, RuntimeMemoryEstimates.GetMagnitudeBitLength(BigInteger.Parse(text)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(4096)]
    public void MagnitudeBitLengthHandlesPowersOfTwo(int exponent)
    {
        var power = BigInteger.One << exponent;
        Assert.Equal(exponent + 1, RuntimeMemoryEstimates.GetMagnitudeBitLength(power));
        Assert.Equal(exponent, RuntimeMemoryEstimates.GetMagnitudeBitLength(power - BigInteger.One));
        Assert.Equal(exponent + 1, RuntimeMemoryEstimates.GetMagnitudeBitLength(power + BigInteger.One));
        Assert.Equal(exponent + 1, RuntimeMemoryEstimates.GetMagnitudeBitLength(-power));
    }

    [Fact]
    public void MagnitudeBitLengthHandlesLargeMagnitudes()
    {
        var big = BigInteger.Pow(2, 100000);
        Assert.Equal(100001, RuntimeMemoryEstimates.GetMagnitudeBitLength(big));
        Assert.Equal(100000, RuntimeMemoryEstimates.GetMagnitudeBitLength(big - BigInteger.One));
        Assert.Equal(100001, RuntimeMemoryEstimates.GetMagnitudeBitLength(-big - BigInteger.One));
    }

    [Fact]
    public void MagnitudeBitLengthAvoidsProportionalAllocation()
    {
        // A 1M-bit operand needs ~125KB just for a byte copy; the 1K-bit
        // control needs ~128B. If sizing allocated in proportion to the
        // operand, the two windows would differ by ~25MB over 200 calls.
        var big = BigInteger.Pow(2, 1000000);
        var small = BigInteger.Pow(2, 1000);
        for (var i = 0; i < 500; i++)
        {
            RuntimeMemoryEstimates.GetMagnitudeBitLength(big);
            RuntimeMemoryEstimates.GetMagnitudeBitLength(small);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var beforeSmall = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(1001, RuntimeMemoryEstimates.GetMagnitudeBitLength(small));
        }

        var smallAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSmall;
        var beforeBig = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(1000001, RuntimeMemoryEstimates.GetMagnitudeBitLength(big));
        }

        var bigAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeBig;
        Assert.True(bigAllocated < 1048576, $"1M-bit sizing allocated {bigAllocated} bytes over 200 calls");
        var delta = bigAllocated >= smallAllocated ? bigAllocated - smallAllocated : 0;
        Assert.True(delta < 65536, $"sizing allocation scales with the operand: small={smallAllocated}, big={bigAllocated}");
    }
}

