using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class AbstractValueResolutionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void Unresolved_DoesNotExposeADefaultAbstractValue()
    {
        var resolution = AbstractValueResolution.Unresolved;

        Assert.False(resolution.IsResolved);
        Assert.False(resolution.TryGetValue(out _));
    }

    [Fact]
    public void Resolved_ExposesTheTypedAbstractValue()
    {
        var expected = AbstractValue.Integer("1", Span);
        var resolution = AbstractValueResolution.Resolved(expected);

        Assert.True(resolution.IsResolved);
        Assert.True(resolution.TryGetValue(out var actual));
        Assert.Equal(expected, actual);
    }
}
