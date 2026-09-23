using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N17: static contracts and runtime signatures agree on member arity facts.
// Direct calls execute through static analysis (rejected with LA diagnostics);
// aliased calls evade static tracking and execute through runtime binding
// (rejected with TypeError). Both layers must accept and reject the same shapes.
public sealed class StaticRuntimeArityAgreementTests
{
    public static TheoryData<string, string, string, string, object?> Cases() => new()
    {
        { "x = []\nx.append(1)\nreturn len(x)\n", "x = []\nx.append()\nreturn x\n", "x = []\nm = x.append\nm()\nreturn x\n", "LA3121", new BigInteger(1) },
        { "return [3, 1].index(1)\n", "x = [1]\nreturn x.index()\n", "x = [1]\nm = x.index\nreturn m()\n", "LA3123", new BigInteger(1) },
        { "return [1, 2].pop()\n", "x = [1]\nreturn x.pop(1, 2)\n", "x = [1]\nm = x.pop\nreturn m(1, 2)\n", "LA3123", new BigInteger(2) },
        { "x = [1]\nx.clear()\nreturn len(x)\n", "x = [1]\nx.clear(1)\nreturn x\n", "x = [1]\nm = x.clear\nm(1)\nreturn x\n", "LA3125", new BigInteger(0) },
        { "x = [1]\nx.insert(0, 9)\nreturn x[0]\n", "x = [1]\nx.insert(1)\nreturn x\n", "x = [1]\nm = x.insert\nm(1)\nreturn x\n", "LA3123", new BigInteger(9) },
        { "return (5).bit_length()\n", "return (5).bit_length(1)\n", "m = (5).bit_length\nreturn m(1)\n", "LA3170", new BigInteger(3) },
        { "return (5).__round__(1)\n", "return (5).__round__(1, 2)\n", "m = (5).__round__\nreturn m(1, 2)\n", "LA3170", new BigInteger(5) },
        { "return (5).__eq__(5)\n", "return (5).__eq__()\n", "m = (5).__eq__\nreturn m()\n", "LA3170", true },
        { "return {}.get(\"a\", 1)\n", "d = {}\nreturn d.get()\n", "d = {}\nm = d.get\nreturn m()\n", "LA3126", new BigInteger(1) },
        { "return {\"a\": 1}.pop(\"a\")\n", "d = {}\nreturn d.pop(1, 2, 3)\n", "d = {}\nm = d.pop\nreturn m(1, 2, 3)\n", "LA3131", new BigInteger(1) },
        { "d = {}\nreturn d.setdefault(\"a\", 1)\n", "d = {}\nreturn d.setdefault()\n", "d = {}\nm = d.setdefault\nreturn m()\n", "LA3134", new BigInteger(1) },
        { "return {}.__contains__(\"a\")\n", "d = {}\nreturn d.__contains__()\n", "d = {}\nm = d.__contains__\nreturn m()\n", "LA3132", false },
        { "return (1.5).hex() != \"\"\n", "return (1.5).hex(1)\n", "m = (1.5).hex\nreturn m(1)\n", "LA3171", true },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BothLayersAcceptAndRejectTheSameShapes(string good, string bad, string aliasBad, string diagnosticCode, object? expected)
    {
        var goodScript = new LythonEngine().Compile(good);
        Assert.True(goodScript.IsValid, string.Join("|", goodScript.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var syncGood = goodScript.Run(new MockLythonHost());
        Assert.True(syncGood.Success, syncGood.Failure?.Message);
        Assert.Equal(expected, syncGood.ReturnValue);
        var asyncGood = await goodScript.RunAsync(new MockLythonHost());
        Assert.True(asyncGood.Success, asyncGood.Failure?.Message);
        Assert.Equal(syncGood.ReturnValue, asyncGood.ReturnValue);

        var badScript = new LythonEngine().Compile(bad);
        Assert.False(badScript.IsValid);
        Assert.Contains(badScript.Diagnostics, d => d.Code == diagnosticCode);

        var aliasScript = new LythonEngine().Compile(aliasBad);
        Assert.True(aliasScript.IsValid, string.Join("|", aliasScript.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var syncBad = aliasScript.Run(new MockLythonHost());
        Assert.False(syncBad.Success);
        Assert.Equal("TypeError", syncBad.Failure?.ExceptionType);
        var asyncBad = await aliasScript.RunAsync(new MockLythonHost());
        Assert.False(asyncBad.Success);
        Assert.Equal("TypeError", asyncBad.Failure?.ExceptionType);
    }
}
