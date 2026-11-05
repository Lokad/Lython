using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class Utf8ScalarWidthTests
{
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("\0\t\r\na", 5, 5)]
    [InlineData("\u007F\u0080\u07FF\u0800\uD7FF\uE000\uFFFF\U00010000\U0010FFFF", 9, 11)]
    public void ScalarAndUtf16WidthsMatchIndependentCharacterCounts(string value, int scalars, int units)
    {
        var text = PyString.FromString(value);
        Assert.Equal(scalars, text.GetCharacterCount(default, null));
        Assert.Equal(scalars, text.Length);
        Assert.Equal(units, text.GetUtf16CodeUnitCount());
    }

    [Theory]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void ByteChunksCanEndInsideUtf8Scalars(int prefix)
    {
        var value = new string('a', prefix) + "😀β\u0800\U0010FFFF" + new string('b', 4095) + "é😀";
        var text = PyString.FromString(value);
        Assert.Equal(prefix + 4095 + 6, text.GetCharacterCount(default, null));
        Assert.Equal(value.Length, text.GetUtf16CodeUnitCount());
        Assert.Equal(value, text.AsString());
    }

    [Fact]
    public void CancelledCountingCallerDoesNotPoisonLaterMetadataReuse()
    {
        var text = PyString.FromString(new string('a', 4095) + "😀");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var span = new LythonSourceSpan(0, 0, 1, 1);
        var first = Assert.Throws<LythonRuntimeException>(() => text.GetCharacterCount(cancellation.Token, span));
        Assert.Equal(LythonFailureReason.Cancelled, first.Attribution?.Reason);
        Assert.Equal(4096, text.GetCharacterCount(default, span));
        var second = Assert.Throws<LythonRuntimeException>(() => text.GetUtf16CodeUnitCount(cancellation.Token, span));
        Assert.Equal(LythonFailureReason.Cancelled, second.Attribution?.Reason);
        Assert.Equal(4097, text.GetUtf16CodeUnitCount());
    }

    [Fact]
    public void ProjectionKeepsFirstRefusalAheadOfLaterCancellation()
    {
        var root = new LythonMemoryBudget(32);
        using var owner = root.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var budget = new ProjectionBudget(owner, cancellation.Token);
        var first = Assert.Throws<ProjectionException>(() => budget.Reserve(33));
        cancellation.Cancel();
        var text = PyString.FromString("😀");
        Assert.Same(first, Assert.Throws<ProjectionException>(() => PublicProjection.ProjectString(text, budget)));
        Assert.Equal(0, budget.CurrentBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void ProjectionForwardsItsOwnTokenWithoutReservingOrCachingIt()
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var text = PyString.FromString(new string('a', 4095) + "😀");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = new ProjectionBudget(owner, cancellation.Token);
        Assert.Throws<LythonRuntimeException>(() => PublicProjection.ProjectString(text, cancelled));
        Assert.Equal(0, cancelled.CurrentBytes);
        var current = new ProjectionBudget(owner);
        Assert.Equal(new string('a', 4095) + "😀", PublicProjection.ProjectString(text, current));
        Assert.Equal(32 + 2 * 4097, current.CurrentBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }
}
