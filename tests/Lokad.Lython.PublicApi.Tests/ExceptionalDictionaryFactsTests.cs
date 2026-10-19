using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionalDictionaryFactsTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "box={}\nclass Bad:\n def __setitem__(self,key,value): raise ValueError('bad')\ntry: box['ok']=Bad()['bad']=[]\nexcept ValueError: print('ok' in box,box['ok'])", "True []\n" },
        { "box={}\nalias=box\ntry:\n alias['ok']=3\n raise ValueError()\nexcept ValueError: print(box['ok'])", "3\n" },
        { "box={}\nholder=(box,)\ntry:\n box['ok']=4\n raise ValueError()\nexcept ValueError: print(holder[0]['ok'])", "4\n" },
        { "box={}\ntry:\n box['ok']=5\n raise ValueError()\nexcept (ValueError if box['ok'] else TypeError): print(box['ok'])", "5\n" },
        { "box={}\ntry:\n box['ok']=6\n raise ValueError()\nexcept ValueError: pass\nprint(box['ok'])", "6\n" },
        { "box={}\ntry:\n box['ok']=7\n raise ValueError()\nexcept ValueError: pass\nfinally: print(box['ok'])", "7\n" },
        { "box={'ok':1}\nalias=box\ntry:\n del alias['ok']\n raise ValueError()\nexcept ValueError:\n try: print(box['ok'])\n except KeyError: print('missing')", "missing\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExceptionalPathsForgetPreTryMappingContents(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("box={}\nbox['missing']")]
    [InlineData("try: raise ValueError()\nexcept ValueError:\n box={}\n print(box['missing'])")]
    public void FreshMissingKeyFactsRemainDiagnosed(string source)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3157");
    }
}
