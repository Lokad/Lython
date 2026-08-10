using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class AbstractValueTests
{
    private static readonly LythonSourceSpan Span = new(1, 1, 1, 1);

    [Fact]
    public void JoinLiteralDictionaries_MatchesKeysIndependentlyOfOrder()
    {
        var left = AbstractValue.Dict(
        [
            Pair(AbstractValue.String("alpha", Span), AbstractValue.Integer("1", Span)),
            Pair(AbstractValue.Bytes([1, 2, 3], Span), AbstractValue.String("left", Span)),
        ], Span);
        var right = AbstractValue.Dict(
        [
            Pair(AbstractValue.Bytes([1, 2, 3], Span), AbstractValue.String("right", Span)),
            Pair(AbstractValue.String("alpha", Span), AbstractValue.Integer("1", Span)),
        ], Span);

        var joined = AbstractValue.Join(left, right, Span);

        Assert.Equal(AbstractValueKind.Dict, joined.Kind);
        var pairs = joined.RequirePayload<IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>>();
        Assert.Equal(AbstractValueKind.StringType, pairs[1].Value.Kind);
    }

    [Fact]
    public void JoinLiteralDictionaries_RejectsKeysWithoutStableLiteralEquality()
    {
        var key = AbstractValue.ListOf(AbstractValue.IntegerType(Span), Span);
        var left = AbstractValue.Dict([Pair(key, AbstractValue.String("left", Span))], Span);
        var right = AbstractValue.Dict([Pair(key, AbstractValue.String("right", Span))], Span);

        var joined = AbstractValue.Join(left, right, Span);

        Assert.Equal(AbstractValueKind.Unknown, joined.Kind);
    }

    [Fact]
    public void DefaultValue_IsAValidUnknownWithoutAPayload()
    {
        AbstractValue value = default;

        Assert.Equal(AbstractValueKind.Unknown, value.Kind);
        Assert.Throws<InvalidOperationException>(() => value.RequirePayload<string>());
    }

    [Fact]
    public void Factories_ExposeOnlyTheirTypedPayloads()
    {
        var value = AbstractValue.List([AbstractValue.Integer("1", Span)], Span);

        var items = value.RequirePayload<IReadOnlyList<AbstractValue>>();
        Assert.Single(items);
        Assert.Equal("1", items[0].RequirePayload<string>());
        Assert.Throws<InvalidOperationException>(() => value.RequirePayload<string>());
    }

    private static KeyValuePair<AbstractValue, AbstractValue> Pair(AbstractValue key, AbstractValue value)
        => new(key, value);
}
