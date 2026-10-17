using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonTrailingCommaTests
{
    // Independently captured CPython3.13 goldens; invalid-input diagnostics use
    // Python character offsets, including prefixed and multiline Unicode text.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "multiline-coordinates", "import json\nfor text in ['{\\n\"caf\u00e9\":\"\ud83d\ude00\",\\n}', '[\\n1,\\n]']:\n try:json.loads(text)\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Illegal trailing comma before end of object 12 2 11 True\nIllegal trailing comma before end of array 3 2 2 True\n" },
        { "object", "import json\nfor text in ['{\"a\":1,}', '{\"caf\u00e9\":\"\ud83d\ude00\", \\n\\t}', '{}', '{\"a\":1}']:\n try:print(json.loads(text))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Illegal trailing comma before end of object 6 1 7 True\nIllegal trailing comma before end of object 11 1 12 True\n{}\n{'a': 1}\n" },
        { "array", "import json\nfor text in ['[1,]', '[\"\ud83d\ude00\", \\n\\t]', '[]', '[1]']:\n try:print(json.loads(text))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Illegal trailing comma before end of array 2 1 3 True\nIllegal trailing comma before end of array 4 1 5 True\n[]\n[1]\n" },
        { "nested", "import json\nfor text in ['{\"outer\":[{\"caf\u00e9\":\"\ud83d\ude00\",}]}', '[{\"x\":1},]', '{\"x\":[1,]}']:\n try:print(json.loads(text))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Illegal trailing comma before end of object 21 1 22 True\nIllegal trailing comma before end of array 8 1 9 True\nIllegal trailing comma before end of array 7 1 8 True\n" },
        { "incomplete", "import json\nfor text in ['{\"a\":1,', '[1,', '{\"a\":1', '[1', '{\"a\":1 2}', '[1 2]']:\n try:print(json.loads(text))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Expecting property name enclosed in double quotes 7 1 8 True\nExpecting value 3 1 4 True\nExpecting ',' delimiter 6 1 7 True\nExpecting ',' delimiter 2 1 3 True\nExpecting ',' delimiter 7 1 8 True\nExpecting ',' delimiter 3 1 4 True\n" },
        { "pairs", "import json\nevents=[]\ndef pairs(items):\n events.append(items)\n return dict(items)\nfor text in ['{\"a\":1,}', '{\"caf\u00e9\":\"\ud83d\ude00\", \\n}', '{\"ok\":{}}']:\n events.clear()\n try:print(json.loads(text,object_pairs_hook=pairs))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n print(events)\n", "Illegal trailing comma before end of object 6 1 7 True\n[]\nIllegal trailing comma before end of object 11 1 12 True\n[]\n{'ok': {}}\n[[], [('ok', {})]]\n" },
        { "raw-prefix", "import json\nfor text,idx in [('\u03bb{\"caf\u00e9\":\"\ud83d\ude00\",}tail',1),('\u03bb[1,]tail',1),('0 {\"a\":1,}',0),('\u03bb{}tail',1)]:\n try:print(json.JSONDecoder().raw_decode(text,idx))\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno,error.doc==text)\n", "Illegal trailing comma before end of object 12 1 13 True\nIllegal trailing comma before end of array 3 1 4 True\n(0, 1)\n({}, 3)\n" },
        { "hook-order", "import json\nevents=[]\ndef integer(text):\n events.append('int:'+text)\n return int(text)\ndef hook(value):\n events.append('hook')\n return value\nfor text in ['{\"a\":1,}', '[{\"a\":1},]', '{\"a\":1}']:\n events.clear()\n try:print(json.loads(text,parse_int=integer,object_hook=hook))\n except json.JSONDecodeError as error:print(error.msg,error.pos)\n print(events)\n", "Illegal trailing comma before end of object 6\n['int:1']\nIllegal trailing comma before end of array 8\n['int:1', 'hook']\n{'a': 1}\n['int:1', 'hook']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task InvalidSeparatorsPreservePythonErrorsAndHookOrder(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
