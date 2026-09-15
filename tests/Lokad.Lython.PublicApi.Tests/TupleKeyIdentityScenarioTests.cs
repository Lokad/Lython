using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// S02: validated tuple keys keep their identity (and nested identities) instead of
// rebuilding copies: displays, constructors, updates, membership and views preserve
// `is` identity while invalid nested keys still raise TypeError.
public sealed class TupleKeyIdentityScenarioTests
{
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
    public async Task DictDisplayPreservesKeyIdentity()
        => await AssertValue("key = (1, 2)\nd = {key: 3}\nreturn next(iter(d)) is key\n", true);

    [Fact]
    public async Task SetDisplayPreservesItemIdentity()
        => await AssertValue("key = (1, 2)\ns = {key}\nreturn next(iter(s)) is key\n", true);

    [Fact]
    public async Task NestedTuplePreservesInnerIdentity()
        => await AssertValue("inner = (1, 2)\nkey = (inner, 3)\nd = {key: 4}\nouter = next(iter(d))\nreturn outer[0] is inner\n", true);

    [Fact]
    public async Task DictConstructorPreservesKeyIdentity()
        => await AssertValue("key = (1, 2)\nd = dict([(key, 3)])\nreturn next(iter(d)) is key\n", true);

    [Fact]
    public async Task SetConstructorPreservesItemIdentity()
        => await AssertValue("key = (1, 2)\ns = set([key])\nreturn next(iter(s)) is key\n", true);

    [Fact]
    public async Task SubscriptUpdatePreservesKeyIdentity()
        => await AssertValue("key = (1, 2)\nd = {}\nd[key] = 3\nreturn next(iter(d)) is key\n", true);

    [Fact]
    public async Task MembershipFindsEqualTuple()
        => await AssertValue("d = {(1, 2): 3}\nreturn (1, 2) in d\n", true);

    [Fact]
    public async Task SubscriptGetFindsEqualTuple()
        => await AssertValue("d = {(1, 2): 3}\nreturn d[(1, 2)]\n", new BigInteger(3));

    [Fact]
    public async Task UpdateOverwritesEqualTuple()
        => await AssertValue("d = {(1, 2): 1}\nd[(1, 2)] = 5\nreturn d[(1, 2)]\n", new BigInteger(5));

    [Fact]
    public async Task KeysViewPreservesKeyIdentity()
        => await AssertValue("key = (1, 2)\nd = {key: 3}\nreturn next(iter(d.keys())) is key\n", true);

    [Fact]
    public async Task NestedInvalidDictKeyRaises()
        => await AssertValue("try:\n    d = {(1, [2]): 3}\n    return 0\nexcept TypeError:\n    return 1\n", new BigInteger(1));

    [Fact]
    public async Task NestedInvalidSetItemRaises()
        => await AssertValue("try:\n    s = {(1, [2])}\n    return 0\nexcept TypeError:\n    return 1\n", new BigInteger(1));

    [Fact]
    public async Task UnhashableTopLevelKeyRaises()
        => await AssertValue("try:\n    d = {[1]: 2}\n    return 0\nexcept TypeError:\n    return 1\n", new BigInteger(1));
}
