using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class StaticCallArgumentStateTests
{
    [Fact]
    public void OrdinaryArgumentsResolveValuesInTheCurrentBindingState()
    {
        var call = ParseCall("probe(item, named=item)");
        var state = new AbstractState();
        state.Set("item", AbstractValue.String("before", call.Span));
        Assert.True(StaticCallArguments.TryGetConcreteArguments(call, state, out var first));
        Assert.Equal("before", first.ResolvePositionalValue(0, state).RequireText());
        Assert.Equal("before", first.ResolveKeywordValue("named", state).RequireText());

        var changed = state.Clone();
        changed.Set("item", AbstractValue.String("after", call.Span));
        Assert.True(StaticCallArguments.TryGetConcreteArguments(call, changed, out var second));
        Assert.Equal("after", second.ResolvePositionalValue(0, changed).RequireText());
        Assert.Equal("after", second.ResolveKeywordValue("named", changed).RequireText());
        Assert.Equal("before", first.ResolvePositionalValue(0, state).RequireText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpansionIsResolvedAgainAfterBindingsChange(bool keyword)
    {
        var call = ParseCall(keyword ? "probe(**items)" : "probe(*items)");
        var state = new AbstractState();
        Assert.False(StaticCallArguments.TryGetConcreteArguments(call, state, out _));
        Bind("first", "before");
        Assert.True(StaticCallArguments.TryGetConcreteArguments(call, state, out var first));
        Assert.Equal("before", Value(first, "first"));
        Bind("second", "after");
        Assert.True(StaticCallArguments.TryGetConcreteArguments(call, state, out var second));
        Assert.Equal("after", Value(second, "second"));
        if (keyword) Assert.False(second.Keywords.ContainsKey("first"));

        void Bind(string name, string value)
        {
            var item = AbstractValue.String(value, call.Span);
            state.Set("items", keyword
                ? AbstractValue.Dict([new(AbstractValue.String(name, call.Span), item)], call.Span)
                : AbstractValue.Tuple([item], call.Span));
        }

        string Value(ConcreteCallArguments arguments, string name) => keyword
            ? arguments.ResolveKeywordValue(name, state).RequireText()
            : arguments.ResolvePositionalValue(0, state).RequireText();
    }

    private static CallExpressionSyntax ParseCall(string source)
    {
        var parsed = new Parser(PythonTokenization.Read(source)).Parse();
        Assert.Empty(parsed.Diagnostics);
        return Assert.IsType<CallExpressionSyntax>(
            Assert.IsType<ExpressionStatementSyntax>(Assert.Single(parsed.Script!.Statements)).Expression);
    }
}
