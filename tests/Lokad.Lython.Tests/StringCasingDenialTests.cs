using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class StringCasingDenialTests
{
    [Theory]
    [InlineData("ABCD", false)]
    [InlineData("abcd", true)]
    [InlineData("İ", false)]
    public void CasingDenialReleasesScratch(string source, bool upper)
    {
        var length = Encoding.UTF8.GetByteCount(source);
        var governor = new MemoryGovernor(2 * PyString.EstimateApproximateBytes(length) + length - 1);
        var value = PyString.FromString(source, governor, null);
        var before = governor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => upper ? PyStringOps.Upper(value) : PyStringOps.Lower(value));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(before, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        GC.KeepAlive(value);
    }
}
