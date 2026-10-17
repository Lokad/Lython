using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonBooleanOptionTests
{
    // Independent CPython 3.13.2 results; omitted values differ from explicit None.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "loads_strict_none", "import json\ntry:print(repr(json.loads('\\\"a'+chr(1)+'b\\\"',strict=None)))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "decoder_strict_none", "import json\ntry:print(repr(json.JSONDecoder(strict=None).decode('\\\"a'+chr(1)+'b\\\"')))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "loads_strict_false", "import json\ntry:print(repr(json.loads('\\\"a'+chr(1)+'b\\\"',strict=False)))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "loads_strict_default", "import json\ntry:print(repr(json.loads('\\\"a'+chr(1)+'b\\\"')))\nexcept Exception as error:print(type(error).__name__)", "JSONDecodeError\n" },
        { "dumps_ascii_none", "import json\ntry:print(repr(json.dumps('é',ensure_ascii=None)))\nexcept Exception as error:print(type(error).__name__)", "'\"é\"'\n" },
        { "encoder_ascii_none", "import json\ntry:print(repr(json.JSONEncoder(ensure_ascii=None).encode('é')))\nexcept Exception as error:print(type(error).__name__)", "'\"é\"'\n" },
        { "dumps_nan_none", "import json\ntry:print(repr(json.dumps(float('nan'),allow_nan=None)))\nexcept Exception as error:print(type(error).__name__)", "ValueError\n" },
        { "encoder_nan_none", "import json\ntry:print(repr(json.JSONEncoder(allow_nan=None).encode(float('nan'))))\nexcept Exception as error:print(type(error).__name__)", "ValueError\n" },
        { "dumps_sort_none", "import json\ntry:print(repr(json.dumps({'b':1,'a':2},sort_keys=None)))\nexcept Exception as error:print(type(error).__name__)", "'{\"b\": 1, \"a\": 2}'\n" },
        { "loads_strict_zero", "import json\ntry:print(repr(json.loads('\\\"a'+chr(1)+'b\\\"',strict=0)))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "encoder_raw_skipkeys", "import json\ne=json.JSONEncoder(**{'skipkeys':None})\nprint(getattr(e,'skipkeys') is None)", "True\n" },
        { "encoder_raw_ensure_ascii", "import json\ne=json.JSONEncoder(**{'ensure_ascii':None})\nprint(getattr(e,'ensure_ascii') is None)", "True\n" },
        { "encoder_raw_check_circular", "import json\ne=json.JSONEncoder(**{'check_circular':None})\nprint(getattr(e,'check_circular') is None)", "True\n" },
        { "encoder_raw_allow_nan", "import json\ne=json.JSONEncoder(**{'allow_nan':None})\nprint(getattr(e,'allow_nan') is None)", "True\n" },
        { "encoder_raw_sort_keys", "import json\ne=json.JSONEncoder(**{'sort_keys':None})\nprint(getattr(e,'sort_keys') is None)", "True\n" },
        { "decoder_raw_strict_none", "import json\ne=json.JSONDecoder(strict=None)\nprint(e.strict is None)", "True\n" },
        { "mutated_ascii_none", "import json\ne=json.JSONEncoder()\ne.ensure_ascii=None\nprint(repr(e.encode(\"é\")))", "'\"é\"'\n" },
        { "mutated_nan_none", "import json\ne=json.JSONEncoder()\ne.allow_nan=None\ntry:print(e.encode(float(\"nan\")))\nexcept Exception as error:print(type(error).__name__)", "ValueError\n" },
        { "decoder_captured_true", "import json\ne=json.JSONDecoder()\ne.strict=None\nprint(e.strict is None)\ntry:print(repr(e.decode(chr(34)+\"a\"+chr(1)+\"b\"+chr(34))))\nexcept Exception as error:print(type(error).__name__)", "True\nJSONDecodeError\n" },
        { "decoder_captured_none", "import json\ne=json.JSONDecoder(strict=None)\ne.strict=True\ntry:print(repr(e.decode(chr(34)+\"a\"+chr(1)+\"b\"+chr(34))))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "dump_ascii_none", "import json,io\noutput=io.StringIO()\njson.dump(\"é\",output,ensure_ascii=None)\nprint(repr(output.getvalue()))", "'\"é\"'\n" },
        { "dump_nan_none", "import json,io\ntry:json.dump(float(\"nan\"),io.StringIO(),allow_nan=None)\nexcept Exception as error:print(type(error).__name__)", "ValueError\n" },
        { "load_strict_none", "import json,io\ntry:print(repr(json.load(io.StringIO(chr(34)+\"a\"+chr(1)+\"b\"+chr(34)),strict=None)))\nexcept Exception as error:print(type(error).__name__)", "'a\\x01b'\n" },
        { "encoder_subclass_defaults", "import json\nclass Encoder(json.JSONEncoder):\n def __init__(self,**kw):\n  print(tuple(kw[k] for k in [\"skipkeys\",\"ensure_ascii\",\"check_circular\",\"allow_nan\",\"sort_keys\"]))\n  super().__init__(**kw)\nprint(json.dumps({},cls=Encoder))", "(False, True, True, True, False)\n{}\n" },
        { "encoder_subclass_none", "import json\nclass Encoder(json.JSONEncoder):\n def __init__(self,**kw):\n  print(kw[\"ensure_ascii\"] is None)\n  super().__init__(**kw)\nprint(repr(json.dumps(\"é\",cls=Encoder,ensure_ascii=None)))", "True\n'\"é\"'\n" },
        { "decoder_subclass_omitted", "import json\nclass Decoder(json.JSONDecoder):\n def __init__(self,**kw):\n  print(\"strict\" in kw)\n  super().__init__(**kw)\nprint(json.loads(\"7\",cls=Decoder))", "False\n7\n" },
        { "decoder_subclass_none", "import json\nclass Decoder(json.JSONDecoder):\n def __init__(self,**kw):\n  print(kw[\"strict\"] is None)\n  super().__init__(**kw)\n  print(self.strict is None)\ntry:print(repr(json.loads(chr(34)+\"a\"+chr(1)+\"b\"+chr(34),cls=Decoder,strict=None)))\nexcept Exception as error:print(type(error).__name__)", "True\nTrue\n'a\\x01b'\n" },
        { "cycle_none", "import json\nvalue=[]\nvalue.append(value)\ntry:json.dumps(value,check_circular=None)\nexcept Exception as error:print(type(error).__name__)", "RecursionError\n" },
        { "cycle_default", "import json\nvalue=[]\nvalue.append(value)\ntry:json.dumps(value)\nexcept Exception as error:print(type(error).__name__)", "ValueError\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExplicitNonePreservesPythonBooleanBehavior(string label, string source, string expected)
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
