using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class RangeIterationStateTests
{
    [Fact]
    public void RepeatedEnumerationKeepsIndependentPositionAndStableCurrent()
    {
        var values = new PyRange(0, 3, 1).Iterate();
        using var first = values.GetEnumerator();
        using var second = values.GetEnumerator();
        Assert.True(first.MoveNext());
        var zero = first.Current;
        Assert.Same(zero, first.Current);
        Assert.True(second.MoveNext());
        Assert.Equal(BigInteger.Zero, second.Current);
        Assert.True(first.MoveNext());
        Assert.Equal(BigInteger.One, first.Current);
        Assert.Equal(BigInteger.Zero, zero);
        Assert.Equal(BigInteger.Zero, second.Current);
        first.Dispose();
        Assert.False(first.MoveNext());
        Assert.True(second.MoveNext());
        Assert.Equal(BigInteger.One, second.Current);
        Assert.True(second.MoveNext());
        Assert.Equal(new BigInteger(2), second.Current);
        Assert.False(second.MoveNext());
        Assert.False(second.MoveNext());
        Assert.Throws<NotSupportedException>(() => second.Reset());
    }

    public static TheoryData<BigInteger, BigInteger, BigInteger, BigInteger[]> Bounds => new()
    {
        { 9, -8, -4, [9, 5, 1, -3, -7] },
        { long.MaxValue - 2, long.MaxValue, 10, [new BigInteger(long.MaxValue - 2)] },
        { 0, long.MinValue, long.MinValue, [BigInteger.Zero] },
        { long.MaxValue, long.MinValue, long.MinValue, [new BigInteger(long.MaxValue), -BigInteger.One] },
        { long.MinValue, new BigInteger(long.MinValue) + 3, 1,
            [new BigInteger(long.MinValue), new BigInteger(long.MinValue) + 1, new BigInteger(long.MinValue) + 2] },
        { BigInteger.One << 100, (BigInteger.One << 100) + 3, 1,
            [BigInteger.One << 100, (BigInteger.One << 100) + 1, (BigInteger.One << 100) + 2] },
        { -(BigInteger.One << 100), -(BigInteger.One << 100) - 3, -1,
            [-(BigInteger.One << 100), -(BigInteger.One << 100) - 1, -(BigInteger.One << 100) - 2] },
        { 4, 4, 1, [] },
        { 4, 8, -1, [] },
    };

    [Fact]
    public void FullSignedSpanCanAdvanceWithoutNarrowingItsLength()
    {
        using var cursor = new PyRange(long.MinValue, long.MaxValue, BigInteger.One).Iterate().GetEnumerator();
        Assert.True(cursor.MoveNext());
        Assert.Equal(new BigInteger(long.MinValue), cursor.Current);
        Assert.True(cursor.MoveNext());
        Assert.Equal(new BigInteger(long.MinValue) + 1, cursor.Current);
        Assert.True(cursor.MoveNext());
        Assert.Equal(new BigInteger(long.MinValue) + 2, cursor.Current);
    }

    [Theory]
    [MemberData(nameof(Bounds))]
    public void BoundsOvershootAndExhaustionRemainExact(BigInteger start, BigInteger stop,
        BigInteger step, BigInteger[] expected)
    {
        using var cursor = new PyRange(start, stop, step).Iterate().GetEnumerator();
        foreach (var value in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal(value, cursor.Current);
            Assert.Same(cursor.Current, cursor.Current);
        }
        Assert.False(cursor.MoveNext());
        Assert.False(cursor.MoveNext());
    }
}
