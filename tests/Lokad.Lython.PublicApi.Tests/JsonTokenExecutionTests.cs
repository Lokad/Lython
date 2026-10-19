using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonTokenExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "plain", "import json\nprint(json.dumps(json.loads('\"café😀\"'),ensure_ascii=True))", "\"caf\\u00e9\\ud83d\\ude00\"\n" },
        { "empty", "import json\nprint(json.dumps(json.loads('[\"\",\"a\",\"😀\"]')))", "[\"\", \"a\", \"\\ud83d\\ude00\"]\n" },
        { "escaped", "import json\nprint(json.dumps(json.loads(r'\"a\\nb\\u0041\"')))", "\"a\\nbA\"\n" },
        { "duplicate-keys", "import json\nprint(json.dumps(json.loads('{\"kind\":\"red\",\"note\":\"café😀\",\"kind\":\"blue\"}'),sort_keys=True))", "{\"kind\": \"blue\", \"note\": \"caf\\u00e9\\ud83d\\ude00\"}\n" },
        { "pairs-hook", "import json\nprint(json.dumps(json.loads('{\"a\":\"x\",\"a\":\"y\"}',object_pairs_hook=lambda pairs:pairs)))", "[[\"a\", \"x\"], [\"a\", \"y\"]]\n" },
        { "raw-decode", "import json\nprint(json.dumps(json.JSONDecoder().raw_decode('xx\"café😀\"tail',2)))", "[\"caf\\u00e9\\ud83d\\ude00\", 9]\n" },
        { "lenient", "import json\nprint(json.dumps(json.loads('\"a'+chr(1)+'b\"',strict=False)))", "\"a\\u0001b\"\n" },
        { "errors", "import json\nfor source in ['\"café😀\" trailing','{\"café\":\"😀\",\"x\":}','\"unterminated',r'\"bad\\q\"']:\n try:json.loads(source)\n except json.JSONDecodeError as e:print(e.pos,e.doc==source)", "8 True\n16 True\n0 True\n4 True\n" },
        { "roundtrip", "import json\nvalue=['café😀','',{'a':'b','note':'line\\nquoted\\\"'}]\nprint(json.dumps(json.loads(json.dumps(value,ensure_ascii=False)),sort_keys=True))", "[\"caf\\u00e9\\ud83d\\ude00\", \"\", {\"a\": \"b\", \"note\": \"line\\nquoted\\\"\"}]\n" },
        { "literal-values", "import json\nprint(json.loads('[true,false,null]'))", "[True, False, None]\n" },
        { "literal-prefix", "import json\nd=json.JSONDecoder()\nfor text in ['truetrash','falsex','nullx']:print(d.raw_decode(text))", "(True, 4)\n(False, 5)\n(None, 4)\n" },
        { "literal-unicode-prefix", "import json\nd=json.JSONDecoder()\nfor text in ['😀true','éfalse','λnull']:print(d.raw_decode(text,1))", "(True, 5)\n(False, 6)\n(None, 5)\n" },
        { "literal-errors", "import json\nfor text in ['tru','fals','nul','True','FALSE','NULL','true false','[true,false,nullx]']:\n try:json.loads(text)\n except json.JSONDecodeError as error:print(error.msg,error.pos,error.lineno,error.colno)", "Expecting value 0 1 1\nExpecting value 0 1 1\nExpecting value 0 1 1\nExpecting value 0 1 1\nExpecting value 0 1 1\nExpecting value 0 1 1\nExtra data 5 1 6\nExpecting ',' delimiter 16 1 17\n" },
        { "literal-ignored-hooks", "import json\nevents=[]\ndef hook(value):\n events.append(value)\n return value\nprint(json.loads('[true,false,null]',parse_int=hook,parse_float=hook,parse_constant=hook))\nprint(events)", "[True, False, None]\n[]\n" },
        { "literal-constant-hooks", "import json\nevents=[]\ndef hook(value):\n events.append(value)\n return value\nprint(json.loads('[NaN,Infinity,-Infinity]',parse_constant=hook))\nprint(events)", "['NaN', 'Infinity', '-Infinity']\n['NaN', 'Infinity', '-Infinity']\n" },
        { "literal-object-hooks", "import json\nevents=[]\ndef hook(pairs):\n events.append(pairs)\n return dict(pairs)\nprint(json.loads('{\"café\":[true,false,null],\"z\":null}',object_pairs_hook=hook))\nprint(events)", "{'café': [True, False, None], 'z': None}\n[[('café', [True, False, None]), ('z', None)]]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TokenDecodingPreservesPythonBehavior(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
    }

    public static IEnumerable<object[]> HookCases()
    {
        foreach (var pairs in new[] { false, true })
        foreach (var escaped in new[] { false, true })
        foreach (var cancel in new[] { false, true }) yield return [pairs, escaped, cancel];
    }

    [Theory]
    [MemberData(nameof(HookCases))]
    public async Task DecodedKeysAndValuesRemainOwnedThroughHookSuspension(bool pairs, bool escaped, bool cancel)
    {
        var document = escaped ? "{\"caf\\u00e9\\ud83d\\ude00\":\"va\\nlue\"}" : "{\"café😀\":\"value\"}";
        var source = "import json\ndef hook(value):\n with open('hook.txt','a') as output: output.write('x')\n if isinstance(value,list): value=dict(value)\n assert value['café😀']==expected\n return value\nresult=json.loads(source,"
            + (pairs ? "object_pairs_hook" : "object_hook") + "=hook)\nassert list(result)==['café😀']\nprint('ok')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/hook.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/hook.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document, ["expected"] = escaped ? "va\nlue" : "value" } };
        var pending = script.RunAsync(host, options, cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Hook finished before controlled host suspension.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "ok\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforex", host.ReadText("/hook.txt"));
            if (!cancel) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered!.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DecodedAliasesRemainFundedAcrossExecutions(bool asynchronous, bool mapping)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var source = "import json\nvalue=json.loads(source)\nreturn " + (mapping ? "[next(iter(value)),next(iter(value.values()))]" : "value");
        var expected = new string('a', 4096);
        var document = mapping ? "{\"" + expected + "\":\"" + expected + "\"}" : "\"" + expected + "\"";
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            if (mapping)
            {
                var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>().ToArray();
                Assert.Equal(2, values.Length);
                Assert.All(values, v => Assert.Equal(expected, Assert.IsType<string>(v)));
            }
            else Assert.Equal(expected, Assert.IsType<string>(retained.ReturnValue));
            var held = budget.ReservedBytes;
            using (var next = script.Run(new MockLythonHost(), options)) Assert.True(next.Success, next.Failure?.Message);
            Assert.True(held > baseline);
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostDrivenJsonBatchesReturnAllowanceToBaseline(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nrows=json.loads(source)\nreturn [len(rows),sum(len(row['note']) for row in rows)]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var document = "[" + string.Join(",", Enumerable.Repeat("{\"kind\":\"blue\",\"note\":\"café😀\"}", 64)) + "]";
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        for (var batch = 0; batch < 8; batch++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>().ToArray();
                Assert.Equal(new object?[] { new BigInteger(64), new BigInteger(320) }, values);
                Assert.True(result.PeakExecutionMemoryBytes < budget.LimitBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
