using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonModuleCallBindingTests
{
    // Independent CPython 3.13 call results, including supported custom classes.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "loads_keyword", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\nprint(call(s=\"7\"))", "7\n" },
        { "loads_none", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\nprint(call(**{\"s\":\"7\",\"strict\":None,\"parse_int\":None}))", "7\n" },
        { "dumps_keyword", "import json\ndef call(*args,**kw):return json.dumps(*args,**kw)\nprint(call(obj={\"é\":1},ensure_ascii=None))", "{\"é\": 1}\n" },
        { "loads_duplicate", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\ntry:call(\"7\",s=\"8\")\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "loads_too_many", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\ntry:call(\"7\",None)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "loads_missing", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\ntry:call(strict=None)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "loads_unknown", "import json\ndef call(*args,**kw):return json.loads(*args,**kw)\ntry:call(\"7\",unknown=1)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "dumps_duplicate", "import json\ndef call(*args,**kw):return json.dumps(*args,**kw)\ntry:call(7,obj=8)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "dumps_missing", "import json\ndef call(*args,**kw):return json.dumps(*args,**kw)\ntry:call(ensure_ascii=None)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "dumps_unknown", "import json\ndef call(*args,**kw):return json.dumps(*args,**kw)\ntry:call(7,unknown=1)\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "decoder_extras", "import json\nclass D(json.JSONDecoder):\n def __init__(self,tag,**kw):\n  self.tag=tag\n  super().__init__(**kw)\n def decode(self,s):return [self.tag,super().decode(s)]\nprint(json.loads(\"7\",cls=D,tag=\"X\",strict=None))", "['X', 7]\n" },
        { "encoder_extras", "import json\nclass E(json.JSONEncoder):\n def __init__(self,tag,**kw):\n  self.tag=tag\n  super().__init__(**kw)\n def default(self,o):return self.tag\nprint(json.dumps({1},cls=E,tag=\"é\",ensure_ascii=None))", "\"é\"\n" },
        { "reader_known", "import json\nclass R:\n def read(self):return \"7\"\nprint(json.load(fp=R(),strict=None))", "7\n" },
        { "writer_known", "import json\nclass W:\n def __init__(self):self.parts=[]\n def write(self,s):self.parts.append(s)\nw=W()\njson.dump(obj={\"é\":1},fp=w,ensure_ascii=None)\nprint(\"\".join(w.parts))", "{\"é\": 1}\n" },
        { "known_effect_order", "import json\neffects=[]\ndef mark(name,value):\n effects.append(name)\n return value\nprint(json.loads(s=mark(\"s\",\"7\"),strict=mark(\"strict\",None)))\nprint(effects)", "7\n['s', 'strict']\n" },
        { "duplicate_keyword", "import json\ndef call(**options):return json.loads(\"7\",**options)\ntry:call(**{\"strict\":None},**{\"strict\":False})\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ModuleCallsPreserveBindingAndConstructorForwarding(string label, string source, string expected)
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
