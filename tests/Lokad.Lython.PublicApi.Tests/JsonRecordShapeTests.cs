using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonRecordShapeTests
{
    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ChangingRecordsPreservePythonValuesOrderAndOwnedResults(string name, string source, string expected, bool readsFile, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            async Task<LythonExecutionResult> Run()
            {
                var delayed = asynchronous ? new DelayedLythonHost() : null;
                var immediate = asynchronous ? null : new MockLythonHost();
                if (delayed is not null) delayed.SeedFile("/value.txt", "1");
                else immediate!.SeedFile("/value.txt", "1");
                ILythonHost host = (ILythonHost?)delayed ?? immediate!;
                var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
                try
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.ReturnValue);
                    if (readsFile && delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
            using (var first = await Run())
            {
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                using (var second = await Run())
                {
                    Assert.True(budget.ReservedBytes > retained);
                    Assert.Equal(expected, first.ReturnValue);
                }
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Independent isolated CPython 3.13 results, including full key order.
    public static IEnumerable<object[]> Programs()
    {
        yield return ["shapes", "import json\nrows=json.loads('[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k16\":16,\"k15\":15,\"k14\":14,\"k13\":13,\"k12\":12,\"k11\":11,\"k10\":10,\"k09\":9,\"k08\":8,\"k07\":7,\"k06\":6,\"k05\":5,\"k04\":4,\"k03\":3,\"k02\":2,\"k01\":1,\"k00\":0},{\"k03\":300,\"k00\":-1,\"k03\":301},{},{\"é😀\":[{\"k00\":7},{}]}]')\nrows[0]['k00']=999\nassert rows[1]['k00']==100\nreturn json.dumps([rows,[list(row) for row in rows]],ensure_ascii=False,separators=(',',':'))", "[[{\"k00\":999,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k16\":16,\"k15\":15,\"k14\":14,\"k13\":13,\"k12\":12,\"k11\":11,\"k10\":10,\"k09\":9,\"k08\":8,\"k07\":7,\"k06\":6,\"k05\":5,\"k04\":4,\"k03\":3,\"k02\":2,\"k01\":1,\"k00\":0},{\"k03\":301,\"k00\":-1},{},{\"é😀\":[{\"k00\":7},{}]}],[[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\"],[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\"],[\"k16\",\"k15\",\"k14\",\"k13\",\"k12\",\"k11\",\"k10\",\"k09\",\"k08\",\"k07\",\"k06\",\"k05\",\"k04\",\"k03\",\"k02\",\"k01\",\"k00\"],[\"k03\",\"k00\"],[],[\"é😀\"]]]", false, false];
        yield return ["shapes", "import json\nrows=json.loads('[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k16\":16,\"k15\":15,\"k14\":14,\"k13\":13,\"k12\":12,\"k11\":11,\"k10\":10,\"k09\":9,\"k08\":8,\"k07\":7,\"k06\":6,\"k05\":5,\"k04\":4,\"k03\":3,\"k02\":2,\"k01\":1,\"k00\":0},{\"k03\":300,\"k00\":-1,\"k03\":301},{},{\"é😀\":[{\"k00\":7},{}]}]')\nrows[0]['k00']=999\nassert rows[1]['k00']==100\nreturn json.dumps([rows,[list(row) for row in rows]],ensure_ascii=False,separators=(',',':'))", "[[{\"k00\":999,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k16\":16,\"k15\":15,\"k14\":14,\"k13\":13,\"k12\":12,\"k11\":11,\"k10\":10,\"k09\":9,\"k08\":8,\"k07\":7,\"k06\":6,\"k05\":5,\"k04\":4,\"k03\":3,\"k02\":2,\"k01\":1,\"k00\":0},{\"k03\":301,\"k00\":-1},{},{\"é😀\":[{\"k00\":7},{}]}],[[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\"],[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\"],[\"k16\",\"k15\",\"k14\",\"k13\",\"k12\",\"k11\",\"k10\",\"k09\",\"k08\",\"k07\",\"k06\",\"k05\",\"k04\",\"k03\",\"k02\",\"k01\",\"k00\"],[\"k03\",\"k00\"],[],[\"é😀\"]]]", false, true];
        yield return ["hooks", "import json\nseen=[]\ndef hook(item):\n seen.append(item)\n nested=json.loads('{\"probe\":1,\"data\":[]}')\n item['alias']=[item['k00'],len(nested['data'])]\n with open('value.txt') as file:\n  item['alias'].append(int(file.read()))\n return item\nrows=json.loads('[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k00\":-1,\"k00\":-2}]',object_hook=hook)\nassert seen[0] is rows[0] and seen[1] is rows[1]\nrows[0]['alias'].append(rows[-1]['k00'])\nreturn json.dumps([rows,[list(row) for row in rows]],ensure_ascii=False,separators=(',',':'))\n", "[[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16,\"alias\":[0,0,1,-2]},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116,\"alias\":[100,0,1]},{\"k00\":-2,\"alias\":[-2,0,1]}],[[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\",\"alias\"],[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\",\"alias\"],[\"k00\",\"alias\"]]]", true, false];
        yield return ["hooks", "import json\nseen=[]\ndef hook(item):\n seen.append(item)\n nested=json.loads('{\"probe\":1,\"data\":[]}')\n item['alias']=[item['k00'],len(nested['data'])]\n with open('value.txt') as file:\n  item['alias'].append(int(file.read()))\n return item\nrows=json.loads('[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116},{\"k00\":-1,\"k00\":-2}]',object_hook=hook)\nassert seen[0] is rows[0] and seen[1] is rows[1]\nrows[0]['alias'].append(rows[-1]['k00'])\nreturn json.dumps([rows,[list(row) for row in rows]],ensure_ascii=False,separators=(',',':'))\n", "[[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16,\"alias\":[0,0,1,-2]},{\"k00\":100,\"k01\":101,\"k02\":102,\"k03\":103,\"k04\":104,\"k05\":105,\"k06\":106,\"k07\":107,\"k08\":108,\"k09\":109,\"k10\":110,\"k11\":111,\"k12\":112,\"k13\":113,\"k14\":114,\"k15\":115,\"k16\":116,\"alias\":[100,0,1]},{\"k00\":-2,\"alias\":[-2,0,1]}],[[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\",\"alias\"],[\"k00\",\"k01\",\"k02\",\"k03\",\"k04\",\"k05\",\"k06\",\"k07\",\"k08\",\"k09\",\"k10\",\"k11\",\"k12\",\"k13\",\"k14\",\"k15\",\"k16\",\"alias\"],[\"k00\",\"alias\"]]]", true, true];
        yield return ["errors", "import json\nfirst=json.loads('{\"k00\": 0, \"k01\": 1, \"k02\": 2, \"k03\": 3, \"k04\": 4, \"k05\": 5, \"k06\": 6, \"k07\": 7, \"k08\": 8, \"k09\": 9, \"k10\": 10, \"k11\": 11, \"k12\": 12, \"k13\": 13, \"k14\": 14, \"k15\": 15, \"k16\": 16}')\nerrors=[]\nfor document in ['{\"k00\":','{\"k00\":0,}','{\"k00\":0,\"k01\": [1,2,]}']:\n try:json.loads(document)\n except json.JSONDecodeError as error:errors.append([error.pos,error.lineno,error.colno,error.doc==document])\nreturn json.dumps([first,errors],separators=(',',':'))", "[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},[[7,1,8,true],[8,1,9,true],[20,1,21,true]]]", false, false];
        yield return ["errors", "import json\nfirst=json.loads('{\"k00\": 0, \"k01\": 1, \"k02\": 2, \"k03\": 3, \"k04\": 4, \"k05\": 5, \"k06\": 6, \"k07\": 7, \"k08\": 8, \"k09\": 9, \"k10\": 10, \"k11\": 11, \"k12\": 12, \"k13\": 13, \"k14\": 14, \"k15\": 15, \"k16\": 16}')\nerrors=[]\nfor document in ['{\"k00\":','{\"k00\":0,}','{\"k00\":0,\"k01\": [1,2,]}']:\n try:json.loads(document)\n except json.JSONDecodeError as error:errors.append([error.pos,error.lineno,error.colno,error.doc==document])\nreturn json.dumps([first,errors],separators=(',',':'))", "[{\"k00\":0,\"k01\":1,\"k02\":2,\"k03\":3,\"k04\":4,\"k05\":5,\"k06\":6,\"k07\":7,\"k08\":8,\"k09\":9,\"k10\":10,\"k11\":11,\"k12\":12,\"k13\":13,\"k14\":14,\"k15\":15,\"k16\":16},[[7,1,8,true],[8,1,9,true],[20,1,21,true]]]", false, true];
    }
}
