using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class PyIterationResultTests
{
    [Fact]
    public void End_HasNoAccessibleValue()
    {
        var result = PyIterationResult.End;

        Assert.False(result.HasValue);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Yield_CarriesTheProducedValue()
    {
        var value = new object();
        var result = PyIterationResult.Yield(value);

        Assert.True(result.HasValue);
        Assert.Same(value, result.Value);
    }
}
