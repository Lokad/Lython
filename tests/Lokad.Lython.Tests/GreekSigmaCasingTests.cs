using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class GreekSigmaCasingTests
{
    [Theory]
    [InlineData("ΟΣ", "ος")]
    [InlineData("ΟΣΑ", "οσα")]
    [InlineData("Σ", "σ")]
    [InlineData("AΣ1", "aς1")]
    [InlineData("AΣ_", "aς_")]
    [InlineData("AΣ́", "aς́")]
    [InlineData("AΣ́A", "aσ́a")]
    [InlineData("AΣ.A", "aσ.a")]
    [InlineData("AΣ:A", "aσ:a")]
    [InlineData("AΣ/A", "aς/a")]
    [InlineData("AΣʰA", "aσʰa")]
    [InlineData("AʰΣ", "aʰς")]
    [InlineData("ʰΣ", "ʰσ")]
    public void LowerUsesCasedContextAndSkipsIgnorableScalars(string source, string expected)
        => Assert.Equal(expected, PyStringOps.Lower(PyString.FromString(source)).AsString());

    [Fact]
    public void LongIgnorableRunsPreserveContext()
    {
        var ignored = new string('́', 20_000);
        Assert.Equal("a" + ignored + "ς" + ignored, PyStringOps.Lower(PyString.FromString("A" + ignored + "Σ" + ignored)).AsString());
        Assert.Equal("a" + ignored + "σ" + ignored + "a", PyStringOps.Lower(PyString.FromString("A" + ignored + "Σ" + ignored + "A")).AsString());
    }
}
