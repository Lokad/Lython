using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// S-track: sequence membership consults the member __eq__ protocol like ==
// does (identity implies membership without invoking it). Covers tuples and
// lists through the operator and the dunder, plus misses, negation, default
// identity equality, and reflected order, in both execution modes.
public sealed class ContainmentProtocolScenarioTests
{
    private const string BoxPrelude = "class Box:\n    def __init__(self, v):\n        self.v = v\n    def __eq__(self, other):\n        return isinstance(other, Box) and self.v == other.v\n";

    private static async Task AssertValue(string source, object expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TupleConsultsCustomEq()
        => await AssertValue(BoxPrelude + "return str([Box(1) in (Box(1),), Box(9) in (Box(1),)])\n", "[True, False]");

    [Fact]
    public async Task ListConsultsCustomEq()
        => await AssertValue(BoxPrelude + "return str([Box(1) in [Box(1)], Box(9) in [Box(1)]])\n", "[True, False]");

    [Fact]
    public async Task IdentityImpliesMembership()
        => await AssertValue(BoxPrelude + "b = Box(1)\nreturn str([b in (b,), b in [b], b not in (b,)])\n", "[True, True, False]");

    [Fact]
    public async Task DefaultEqStaysIdentity()
        => await AssertValue("class Plain:\n    pass\nreturn str([Plain() in (Plain(),), Plain() in [Plain()]])\n", "[False, False]");

    [Fact]
    public async Task NotInNegatesCustomEq()
        => await AssertValue(BoxPrelude + "return str([Box(1) not in (Box(1),), Box(9) not in (Box(1),)])\n", "[False, True]");

    [Fact]
    public async Task ReflectedOrderStaysMiss()
        => await AssertValue(BoxPrelude + "return str([Box(1) in (1,), 1 in (Box(1),)])\n", "[False, False]");

    [Fact]
    public async Task DunderParity()
        => await AssertValue(BoxPrelude + "return str([(Box(1),).__contains__(Box(1)), [Box(1)].__contains__(Box(9))])\n", "[True, False]");

    [Fact]
    public async Task CustomEqSeesEachElement()
        => await AssertValue(BoxPrelude + "t = (Box(1), Box(2), Box(3))\nreturn str([Box(3) in t, Box(4) in t])\n", "[True, False]");
}
