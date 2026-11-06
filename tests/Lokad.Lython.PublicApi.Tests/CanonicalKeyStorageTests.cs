using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CanonicalKeyStorageTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["custom hash ignores unhashable field", "from dataclasses import dataclass\n@dataclass(frozen=True)\nclass Key:\n values:list\n def __hash__(self):return 7\nk=Key([1])\nvalues={k:10}\nprint(values[k],len(values))\ncopy=values.copy()\nprint(copy[k],len(copy))\n", "10 1\n10 1\n"];
        yield return ["compound custom hash", "from dataclasses import dataclass\n@dataclass(frozen=True)\nclass Key:\n values:list\n def __hash__(self):return 7\nk=Key([1])\nvalues={(k,):10}\nprint(values[(k,)],len(values))\n", "10 1\n"];
        yield return ["mutable equality slot", "class Key:pass\nk=Key()\nnumber=hash(k)\nvalues={k:10}\ndef equal(self,other):return True\nKey.__eq__=equal\nprint(values[number])\n", "10\n"];
        yield return ["compound mutable equality slot", "class Key:pass\nk=Key()\nnumber=hash(k)\nvalues={(k,):10}\ndef equal(self,other):return True\nKey.__eq__=equal\nprint(values[(number,)])\n", "10\n"];
        yield return ["one hash per global query", "scope=globals()\ncalls=[]\nclass Key:\n def __hash__(self):calls.append('hash');return 42\n def __eq__(self,other):return self is other\nk=Key()\nscope[k]=7\nprint(calls)\ncalls.clear()\nprint(scope[k],calls)\ncalls.clear()\nprint(scope.pop(k),calls)\n", "['hash']\n7 ['hash']\n7 ['hash']\n"];
        yield return ["object namespace equal key", "class Box:value=1\nbox=Box()\nbox.value=2\nview=box.__dict__\nclass Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):return other=='value'\nk=Key()\nview[k]=3\nprint(box.value,view['value'])\nprint(view.pop(k))\nprint('value' in view,box.value)\n", "3 3\n3\nFalse 1\n"];
        yield return ["set custom hash over unhashable field", "from dataclasses import dataclass\n@dataclass(frozen=True)\nclass Key:\n values:list\n def __hash__(self):return 7\nk=Key([1])\nvalues={k}\nprint(k in values,len(values))\ncopy=values.copy()\nprint(k in copy,values==copy)\n", "True 1\nTrue True\n"];
        yield return ["set copies and relations reuse hashes", "calls=[]\nclass Key:\n def __hash__(self):calls.append('hash');return 17\n def __eq__(self,other):return isinstance(other,Key)\nk=Key()\nleft={k}\nright={k}\ncalls.clear()\nprint(left==right,left<=right,len(left.copy()),calls)\n", "True True 1 []\n"];
        yield return ["set mutable class equality", "class Key:pass\nk=Key()\nnumber=hash(k)\nvalues={k}\ndef equal(self,other):return True\nKey.__eq__=equal\nprint(number in values)\n", "True\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StoredHashesAndCurrentSlotsMatchPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
