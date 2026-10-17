using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonEscapeRenderingTests
{
    // Independent isolated CPython 3.13 output; includes DEL and all short controls.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "dumps_default", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps(text))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
        { "dumps_true", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps(text,ensure_ascii=True))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
        { "dumps_false", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps(text,ensure_ascii=False))", "\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\"\n" },
        { "dumps_none", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps(text,ensure_ascii=None))", "\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\"\n" },
        { "encoder_default", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.JSONEncoder().encode(text))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
        { "encoder_false", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.JSONEncoder(ensure_ascii=False).encode(text))", "\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\"\n" },
        { "iterencode_default", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(\"\".join(json.JSONEncoder().iterencode(text)))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
        { "iterencode_false", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(\"\".join(json.JSONEncoder(ensure_ascii=False).iterencode(text)))", "\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\"\n" },
        { "keys_default", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps({text:[text]},sort_keys=True))", "{\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\": [\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"]}\n" },
        { "keys_false", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps({text:[text]},ensure_ascii=False,separators=(\",\",\":\")))", "{\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\":[\"Aéλ😀\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\u007f\u2028\u2029\"]}\n" },
        { "supplementary", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\ntext=chr(0xbeef)+chr(0x10000)+chr(0x10ffff)\nprint(json.dumps(text))", "\"\\ubeef\\ud800\\udc00\\udbff\\udfff\"\n" },
        { "dump_writer", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nclass W:\n def __init__(self):self.parts=[]\n def write(self,s):self.parts.append(s)\nw=W()\njson.dump(text,w)\nprint(\"\".join(w.parts))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
        { "default_hook", "import json\ntext=\"Aéλ😀\"+\"\".join(chr(i) for i in range(32))+chr(127)+chr(0x2028)+chr(0x2029)\nprint(json.dumps({1},default=lambda value:text))", "\"A\\u00e9\\u03bb\\ud83d\\ude00\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\b\\t\\n\\u000b\\f\\r\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f\\u007f\\u2028\\u2029\"\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task EscapesMatchPythonAcrossEncodingRoutes(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
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
