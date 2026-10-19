using System.Collections;
using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class TupleConstructionFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationFailureRefundsConstructionWithoutReleasingAnotherReservation(bool counted)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        using var other = governor.ReserveTemporary(17, null);
        var expected = new InvalidOperationException("source stopped");
        static IEnumerable<object> Values(Exception failure)
        {
            yield return new BigInteger(7);
            throw failure;
        }
        IEnumerable<object> values = Values(expected);
        if (counted) values = new CountedSource(values);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => new PyTuple(values, governor)));
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        var recovered = new PyTuple(new object[] { PyNone.Instance }, governor);
        Assert.Single(recovered);
        Assert.Equal(48, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        GC.KeepAlive(recovered);
    }

    private sealed class CountedSource(IEnumerable<object> source) : IReadOnlyCollection<object>
    {
        public int Count => 2;
        public IEnumerator<object> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
