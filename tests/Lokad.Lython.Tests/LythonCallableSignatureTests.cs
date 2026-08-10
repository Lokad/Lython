using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class LythonCallableSignatureTests
{
    [Fact]
    public void Create_ReusesValidatedMetadataForEquivalentShapes()
    {
        var first = LythonCallableSignature.Create("test.cached_signature", ["value"], RequiredCount: 0);
        var second = LythonCallableSignature.Create("test.cached_signature", ["value"], RequiredCount: 0);
        var keywordOnly = LythonCallableSignature.Create(
            "test.cached_signature",
            ["value"],
            RequiredCount: 0,
            MaxPositionalCount: 0);

        Assert.Same(first, second);
        Assert.Same(first.ParameterIndices, second.ParameterIndices);
        Assert.NotSame(first, keywordOnly);
    }

    [Fact]
    public void Create_RejectsInvalidParameterMetadata()
    {
        Assert.Throws<ArgumentException>(() =>
            LythonCallableSignature.Create("test.duplicate_signature", ["value", "value"]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LythonCallableSignature.Create("test.invalid_signature", ["value"], RequiredCount: 2));
    }
}
