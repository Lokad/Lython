using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonReaderReentrancyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReentrantHooksPreserveOuterContainersAndRetainedAliases(bool asynchronous, bool pairs)
    {
        var argument = pairs ? "object_pairs_hook=hook" : "object_hook=hook";
        var construction = pairs ? "dict(item)" : "item";
        var capture = pairs ? "seen[1][0][1]" : "seen[1]['a']";
        var source = $$"""
            import json
            seen=[]
            lengths=[]
            def number(text):
                with open('value.txt') as file:
                    return int(text)+json.loads(file.read())
            def hook(item):
                seen.append(item)
                lengths.append(len(item))
                nested=json.loads('{"extra":[1,2]}')
                value={{construction}}
                with open('value.txt') as file:
                    value['extra']=len(nested['extra'])+int(file.read())
                return value
            value=json.loads('[{"a":[1,{"a":2}]},{"a":3,"a":4}]',parse_int=number,{{argument}})
            captured={{capture}}
            assert captured is value[0]['a']
            value[0]['a'].append(6)
            assert captured[-1]==6
            return json.dumps(value,sort_keys=True)+'|'+str(lengths)
            """;
        const string expected = "[{\"a\": [2, {\"a\": 3, \"extra\": 3}, 6], \"extra\": 3}, {\"a\": 5, \"extra\": 3}]|";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var batch = 0; batch < 2; batch++)
            {
                var delayed = asynchronous ? new DelayedLythonHost() : null;
                var mock = asynchronous ? null : new MockLythonHost();
                if (delayed is not null) delayed.SeedFile("/value.txt", "1");
                else mock!.SeedFile("/value.txt", "1");
                ILythonHost host = (ILythonHost?)delayed ?? mock!;
                using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected + (pairs ? "[1, 1, 2]" : "[1, 1, 1]"), result.ReturnValue);
                    Assert.True(budget.ReservedBytes > baseline);
                    if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
