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
        var context = PyDecimalContext.Default();

        Assert.IsAssignableFrom<IPyDynamicAttributes>(context);
        Assert.IsAssignableFrom<IPyMutableDynamicAttributes>(context);
    }
}
