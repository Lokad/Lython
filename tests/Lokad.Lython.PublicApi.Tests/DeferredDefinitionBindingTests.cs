using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeferredDefinitionBindingTests
{
    [Theory]
    [InlineData("type Value = int\nreturn Value.__value__ is int")]
    [InlineData("type Value = list[Value]\nreturn Value.__value__.__args__[0] is Value")]
    [InlineData("type Value = Later\nLater=int\nreturn Value.__value__ is int")]
    [InlineData("type Value[T] = tuple[T, Later]\nLater=int\nreturn Value.__value__.__args__[1] is int")]
    public async Task LocalAliasesBindTheirNameAndEvaluateValuesLazily(string body)
    {
        var source = "def make():\n" + string.Join('\n', body.Split('\n').Select(line => " " + line)) + "\nreturn make()\n";
        await AssertTrueInBothModes(source);
    }

    [Theory]
    [InlineData("items=[]\ndef read(): return items[0]\nitems.append(7)\nreturn read()==7")]
    [InlineData("def make():\n items=[]\n def read(): return items[0]\n items.append(7)\n return read\nreturn make()()==7")]
    [InlineData("items={}\ndef read(): return items['key']\nitems['key']=7\nreturn read()==7")]
    [InlineData("items=[]\nclass C:\n def read(self): return items[0]\nitems.append(7)\nreturn C().read()==7")]
    [InlineData("items=[1]\ndef read(): return items[0].upper()\nitems[0]='x'\nreturn read()=='X'")]
    [InlineData("items=([],)\ndef read(): return items[0][0]\nitems[0].append(7)\nreturn read()==7")]
    public async Task DeferredBodiesDoNotFreezeCapturedCollectionContents(string source)
        => await AssertTrueInBothModes(source);

    [Theory]
    [InlineData("def f():\n print(Value)\n type Value=int\n", "LA3146")]
    [InlineData("def f():\n type Value=int\n del Value\n return Value\n", "LA3146")]
    [InlineData("def f():\n items=[]\n return items[0]\n", "LA3117")]
    [InlineData("def f():\n items={}\n return items['missing']\n", "LA3157")]
    public void ProvenLocalErrorsStillProduceDiagnostics(string source, string diagnostic)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == diagnostic);
    }

    private static async Task AssertTrueInBothModes(string source)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var sync = compiled.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(true, sync.ReturnValue);
        var asynchronous = await compiled.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(true, asynchronous.ReturnValue);
    }
}
