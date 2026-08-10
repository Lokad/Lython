using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class DynamicAttributeCapabilityTests
{
    [Fact]
    public void ReadOnlyValues_DoNotAdvertiseMutation()
    {
        var slice = new PySlice(PyNone.Instance, PyNone.Instance, PyNone.Instance);

        Assert.IsAssignableFrom<IPyDynamicAttributes>(slice);
        Assert.IsNotAssignableFrom<IPyMutableDynamicAttributes>(slice);
    }

    [Fact]
    public void MutableValues_AdvertiseBothCapabilities()
    {
        var context = new PyDecimalContext(28, DecimalRoundingMode.HalfEven, -999_999, 999_999, 1, 0);

        Assert.IsAssignableFrom<IPyDynamicAttributes>(context);
        Assert.IsAssignableFrom<IPyMutableDynamicAttributes>(context);
    }
}
