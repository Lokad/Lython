using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonNumberExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "int-boundaries", "import json\nvalues=json.loads('[-9223372036854775808,9223372036854775807,9223372036854775808,-9223372036854775809]')\nprint(json.dumps(values))", "[-9223372036854775808, 9223372036854775807, 9223372036854775808, -9223372036854775809]\n" },
        { "large-int", "import json\nfor text in ['1'+'0'*200,'-1'+'0'*200]:\n value=json.loads(text)\n print(len(str(value)),str(value)==text)", "201 True\n202 True\n" },
        { "floats", "import json,math\nv=json.loads('[1.25,-0.0,1e-300,1e300]')\nprint(v[0]==1.25,math.copysign(1.0,v[1])==-1.0,0<v[2]<1,v[3]>10**299)", "True True True True\n" },
        { "overflow", "import json,math\nfor source in ['1e9999','-1e9999']:\n value=json.loads(source)\n print(math.isinf(value),value>0)", "True True\nTrue False\n" },
        { "hooks", "import json\nevents=[]\ndef integer(s):events.append(('int',s));return s\ndef floating(s):events.append(('float',s));return s\nvalue=json.loads('[-0,1.20e+03,9223372036854775808]',parse_int=integer,parse_float=floating)\nprint(json.dumps(value));print(json.dumps(events))", "[\"-0\", \"1.20e+03\", \"9223372036854775808\"]\n[[\"int\", \"-0\"], [\"float\", \"1.20e+03\"], [\"int\", \"9223372036854775808\"]]\n" },
        { "prefix", "import json\nd=json.JSONDecoder()\nfor source in ['01','-01','1.','1e','1e+','1e2tail']:print(json.dumps(d.raw_decode(source)))", "[0, 1]\n[0, 2]\n[1, 1]\n[1, 1]\n[1, 1]\n[100.0, 3]\n" },
        { "malformed", "import json\nfor source in ['[1e]','[01]','--1']:\n try:json.loads(source)\n except json.JSONDecodeError as e:print(e.pos)", "2\n2\n0\n" },
        { "empty-suffix", "import json\nfor source in ['0','-0','0.0','-0.0','1e0']:print(json.dumps(json.JSONDecoder().raw_decode(source)))", "[0, 1]\n[0, 2]\n[0.0, 3]\n[-0.0, 4]\n[1.0, 3]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NumericTokensFollowPython(string name, string source, string expected)
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

    public static IEnumerable<object?[]> HookCases()
    {
        foreach (var form in new[] { "loads", "decode", "raw" })
        foreach (var pause in new[] { null, "/int.txt", "/float.txt" }) yield return [form, pause];
    }

    [Theory]
    [MemberData(nameof(HookCases))]
    public async Task RawHookTextSurvivesSuspensionWithoutReplay(string form, string? pause)
    {
        var parse = form switch
        {
            "loads" => "json.loads(source,parse_int=integer,parse_float=floating)",
            "decode" => "json.JSONDecoder(parse_int=integer,parse_float=floating).decode(source)",
            _ => "json.JSONDecoder(parse_int=integer,parse_float=floating).raw_decode(source+' tail')[0]"
        };
        var source = "import json\nseen=[]\ndef read(kind,value):\n seen.append(value)\n with open(kind+'.txt','a') as output: output.write('x')\n return value\ndef integer(value):return read('int',value)\ndef floating(value):return read('float',value)\nvalue="
            + parse + "\nassert value==['-0','1.2300e+04']\nassert value[0] is seen[0] and value[1] is seen[1]\nprint('ok')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var paths = new[] { "/int.txt", "/float.txt" };
        foreach (var path in paths) host.SeedFile(path, "before");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation(pause);
        using var cancellation = new CancellationTokenSource();
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = "[-0,1.2300e+04]" } };
        var pending = script.RunAsync(host, options, cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Numeric hook completed before controlled host suspension.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? "ok\n" : "", result.StandardOutput);
            var completed = pause is null ? paths.Length : Array.IndexOf(paths, pause);
            for (var i = 0; i < paths.Length; i++) Assert.Equal("before" + (i < completed ? "x" : ""), host.ReadText(paths[i]));
            if (entered is null) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered.IsCompletedSuccessfully);
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
    public async Task RetainedRawHookTextKeepsItsAllowance(bool asynchronous, bool floating)
    {
        var raw = floating ? "1." + new string('2', 256) + "e+04" : new string('9', 256);
        var source = "import json\nseen=[]\ndef hook(value):seen.append(value);return value\nvalue=json.loads(source," + (floating ? "parse_float" : "parse_int") + "=hook)\nassert value is seen[0]\nreturn value";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = raw } };
        using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            Assert.Equal(raw, Assert.IsType<string>(retained.ReturnValue));
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
    public async Task NumericHostBatchesReturnAllowanceToBaseline(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nrows=json.loads(source)\nreturn [sum(row['amount'] for row in rows),sum(row['ratio'] for row in rows)]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var document = "[" + string.Join(",", Enumerable.Repeat("{\"amount\":2,\"ratio\":1.25}", 64)) + "]";
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        for (var batch = 0; batch < 8; batch++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>().ToArray();
                Assert.Equal(new object?[] { new BigInteger(128), 80.0 }, values);
                Assert.True(result.PeakExecutionMemoryBytes < budget.LimitBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
