using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class SpecializationPolicySubsystemTests
{
    [Fact]
    public void RuntimeSpecializationPolicy_ExplicitlyOwnsCurrentHotPathSeams()
    {
        Assert.True(RuntimeSpecializationPolicy.SupportsSpecializedListStorage);
        Assert.True(RuntimeSpecializationPolicy.SupportsSpecializedDictionaryStorage);
        Assert.True(RuntimeSpecializationPolicy.SupportsSpecializedNumericPaths);
        Assert.True(RuntimeSpecializationPolicy.SupportsSpecializedProjection);
    }
}
