using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeleteExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "module-name", "value=7\ndel value\ntry:print(value)\nexcept NameError:print('missing')", "missing\n" },
        { "helper-local", "def clear(flag):\n value=7\n if flag:del value\n try:return value\n except UnboundLocalError:return 'missing'\nprint(clear(False),clear(True))", "7 missing\n" },
        { "global-name", "value=7\ndef clear():\n global value\n del value\nclear()\ntry:print(value)\nexcept NameError:print('missing')", "missing\n" },
        { "member-protocol", "events=[]\nclass Box:\n def __delattr__(self,name):events.append(name)\nbox=Box()\ndef receiver():events.append('base');return box\ndel receiver().value\nprint(events)", "['base', 'value']\n" },
        { "item-protocol-order", "events=[]\nclass Box:\n def __delitem__(self,key):events.append(('delete',key))\nbox=Box()\ndef receiver():events.append('base');return box\ndef key():events.append('key');return 7\ndel receiver()[key()]\nprint(events)", "['base', 'key', ('delete', 7)]\n" },
        { "slice", "values=[0,1,2,3]\ndel values[1:3]\nprint(values)", "[0, 3]\n" },
        { "partial-deletion-cleanup", "events=[]\nfirst={0:7};second={}\ntry:\n try:del first[0],second[0]\n finally:events.append('cleanup')\nexcept KeyError:print(first,second,events)", "{} {} ['cleanup']\n" },
        { "complete-json-edit", "import json\nconfiguration=json.loads('{\"name\":\"job\",\"debug\":true,\"limit\":7}')\ndel configuration['debug']\nconfiguration['limit']+=1\nprint(json.dumps(configuration,sort_keys=True))", "{\"limit\": 8, \"name\": \"job\"}\n" },
        { "nonlocal-name", "def outer(flag):\n value=7\n def clear():\n  nonlocal value\n  if flag:del value\n clear()\n try:return value\n except UnboundLocalError:return 'missing'\nprint(outer(False),outer(True))", "7 missing\n" },
        { "unrelated-locals", "def clear():\n first=7\n gone=9\n last=11\n del gone\n return first+last\nprint(clear())", "18\n" },
        { "captured-cell", "def make():\n kept=7\n gone=9\n def read():return kept\n del gone\n return read\nprint(make()())", "7\n" },
        { "callback-rebinds-cell", "def clear():\n kept=7\n values={0:9}\n def key():\n  nonlocal kept\n  kept=11\n  return 0\n del values[key()]\n return kept,values\nprint(clear())", "(11, {})\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DeletionPreservesBindingsAndTargetOrder(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
}
