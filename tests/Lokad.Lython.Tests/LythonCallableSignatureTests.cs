using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class LythonCallableSignatureTests
{
    [Fact]
    public void Create_ReusesValidatedMetadataForEquivalentShapes()
    {
        var first = LythonCallableSignature.Create("test.cached_signature", ["value"], requiredCount: 0);
        var second = LythonCallableSignature.Create("test.cached_signature", ["value"], requiredCount: 0);
        var keywordOnly = LythonCallableSignature.Create(
            "test.cached_signature",
            ["value"],
            requiredCount: 0,
            maximumPositionalArgumentCount: 0);

        Assert.Same(first, second);
        var firstParameters = Assert.IsType<NamedCallableParameterLayout>(first.Parameters);
        var secondParameters = Assert.IsType<NamedCallableParameterLayout>(second.Parameters);
        Assert.Same(firstParameters.ParameterIndices, secondParameters.ParameterIndices);
        Assert.NotSame(first, keywordOnly);
    }

    [Fact]
    public void Create_RejectsInvalidParameterMetadata()
    {
        Assert.Throws<ArgumentException>(() =>
            LythonCallableSignature.Create("test.duplicate_signature", ["value", "value"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LythonCallableSignature.Create("test.invalid_signature", ["value"], requiredCount: 2));
    }

    [Fact]
    public void Create_ModelsUnnamedAndNamedParametersAsClosedLayouts()
    {
        var positional = LythonCallableSignature.Create("test.positional", requiredCount: 1);
        var named = LythonCallableSignature.Create("test.named", ["value"], requiredCount: 1);

        Assert.IsType<PositionalCallableParameterLayout>(positional.Parameters);
        var namedParameters = Assert.IsType<NamedCallableParameterLayout>(named.Parameters);
        Assert.Equal(1, namedParameters.RequiredCount);
        Assert.True(namedParameters.MaximumArgumentCount.IsBounded);
        Assert.Equal(1, namedParameters.MaximumArgumentCount.Maximum);
    }
}
