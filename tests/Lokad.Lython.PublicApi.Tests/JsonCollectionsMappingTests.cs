using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonCollectionsMappingTests
{
    [Theory]
    [InlineData(false, "dumps", false)]
    [InlineData(false, "dumps", true)]
    [InlineData(false, "dump", false)]
    [InlineData(false, "dump", true)]
    [InlineData(false, "encode", false)]
    [InlineData(false, "encode", true)]
    [InlineData(false, "iterencode", false)]
    [InlineData(false, "iterencode", true)]
    [InlineData(true, "dumps", false)]
    [InlineData(true, "dumps", true)]
    [InlineData(true, "dump", false)]
    [InlineData(true, "dump", true)]
    [InlineData(true, "encode", false)]
    [InlineData(true, "encode", true)]
    [InlineData(true, "iterencode", false)]
    [InlineData(true, "iterencode", true)]
    public async Task RootsAndNestedAliasesUseDictionaryEncoding(bool useDefaultDict, string route, bool nested)
    {
        var source = Prelude + "\nvalue = " + (useDefaultDict
            ? "defaultdict(factory, {'b': 2, 'a': 1})" : "Counter({'b': 2, 'a': 1})") + "\n" +
            (nested ? "value = {'outer': value, 'alias': value}\n" : "") +
            Encode(route, "value", "sort_keys=True, default=bad") + "\nprint(encoded, len(calls))\n";
        var expectedJson = nested ? "{\"alias\": {\"a\": 1, \"b\": 2}, \"outer\": {\"a\": 1, \"b\": 2}}" : "{\"a\": 1, \"b\": 2}";
        await AssertBothModes(source, expectedJson + " 0\n", requireSuspension: route == "dump");
    }

    [Fact]
    public async Task NestedDefaultDictCounterKeepsFactoriesIdleAndPreservesOrder()
    {
        await AssertBothModes(Prelude + """

            value = defaultdict(Counter)
            value['sample']['x'] += 2
            print(json.dumps(value))
            value = Counter('aba')
            print(json.dumps(value, sort_keys=True))
            print(''.join(json.JSONEncoder(indent=2).iterencode(value)))
            """, "{\"sample\": {\"x\": 2}}\n{\"a\": 2, \"b\": 1}\n{\n  \"a\": 2,\n  \"b\": 1\n}\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyConversionSkippingAndSortingMatchDictionaries(bool useDefaultDict)
    {
        var source = Prelude + "\nvalue = " + (useDefaultDict ? "defaultdict(factory)" : "Counter()") + "\n" +
            """
            value[1] = 2
            value[None] = 3
            value[(1, 2)] = 4
            print(json.dumps(value, skipkeys=True))
            print(''.join(json.JSONEncoder(skipkeys=True).iterencode(value)))
            for encoder in [lambda: json.dumps(value), lambda: json.dumps(value, skipkeys=True, sort_keys=True),
                            lambda: ''.join(json.JSONEncoder(skipkeys=True, sort_keys=True).iterencode(value))]:
                try:
                    encoder()
                except TypeError:
                    print('bad key')
            print(len(calls))
            """;
        await AssertBothModes(source, "{\"1\": 2, \"null\": 3}\n{\"1\": 2, \"null\": 3}\nbad key\nbad key\nbad key\n0\n");
    }

    [Theory]
    [InlineData(false, "dumps")]
    [InlineData(false, "dump")]
    [InlineData(false, "encode")]
    [InlineData(false, "iterencode")]
    [InlineData(true, "dumps")]
    [InlineData(true, "dump")]
    [InlineData(true, "encode")]
    [InlineData(true, "iterencode")]
    public async Task CyclesAreDetectedWithoutInvokingFactoriesOrDefault(bool useDefaultDict, string route)
    {
        var source = Prelude + "\nvalue = " + (useDefaultDict ? "defaultdict(factory)" : "Counter()") + "\n" +
            "value['self'] = value\ntry:\n" +
            string.Join("\n", Encode(route, "value", "default=bad").Split('\n').Select(line => "    " + line)) +
            "\nexcept ValueError:\n    print('cycle', len(calls))\n";
        await AssertBothModes(source, "cycle 0\n");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EncodingGrowthRemainsGoverned(bool useDefaultDict, bool incremental)
    {
        var source = "import json\nfrom collections import Counter, defaultdict\n" +
            "value = " + (useDefaultDict ? "defaultdict(None)" : "Counter()") + "\n" +
            "value['large'] = 'x' * 50000\nprint(len(value['large']))\n" +
            (incremental ? "list(json.JSONEncoder().iterencode(value))" : "json.dumps(value)");
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("50000\n", result.StandardOutput);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
    }

    [Fact]
    public async Task AbandonedIncrementalMappingEncodersReclaimTheirOwnership()
    {
        await AssertBothModes("""
            import json
            from collections import Counter, defaultdict
            for i in range(3000):
                value = defaultdict(None, {'a': Counter({'x': i})})
                iterator = json.JSONEncoder(sort_keys=True).iterencode(value)
                next(iterator)
            print('done')
            """, "done\n", maxMemory: 1048576);
    }

    private const string Prelude = """
        import json
        from collections import Counter, defaultdict
        calls = []
        def factory():
            calls.append('factory')
            raise AssertionError('factory must not run')
        def bad(value):
            calls.append('default')
            raise AssertionError('default must not run')
        """;

    private static string Encode(string route, string value, string options)
        => route switch
        {
            "dumps" => "encoded = json.dumps(" + value + ", " + options + ")",
            "encode" => "encoded = json.JSONEncoder(" + options + ").encode(" + value + ")",
            "iterencode" => "encoded = ''.join(json.JSONEncoder(" + options + ").iterencode(" + value + "))",
            "dump" => "with open('/map.json', 'w') as handle:\n    json.dump(" + value + ", handle, " + options + ")\n" +
                "with open('/map.json') as handle:\n    encoded = handle.read()",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };

    private static async Task AssertBothModes(string source, string expected, bool requireSuspension = false, long? maxMemory = null)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = maxMemory };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        var asynchronous = await script.RunAsync(delayed, options);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
        if (requireSuspension)
        {
            Assert.True(delayed.CompletedAsynchronously > 0);
        }
    }
}
