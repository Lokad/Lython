using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexFindAllListTests
{
    public static IEnumerable<object[]> Shapes()
    {
        foreach (var compiled in new[] { false, true })
        {
            yield return [compiled, "'z'", "'ab'", "[]"];
            yield return [compiled, "'a.'", "'abac'", "['ab', 'ac']"];
            yield return [compiled, "'(a)?b'", "'b ab b'", "['', 'a', '']"];
            yield return [compiled, "'(a)?(b)'", "'b ab b'", "[('', 'b'), ('a', 'b'), ('', 'b')]"];
            yield return [compiled, "'.*?'", "'ab'", "['', 'a', '', 'b', '']"];
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task ResultsHaveOrdinaryListIdentityAndMutation(bool compiled, string pattern, string text, string expected)
    {
        await AssertBothModes("import re\nvalue = " + FindAll(compiled, pattern, text) + "\nexpected = " + expected + "\n" +
            """
            print(type(value) is list, isinstance(value, list), value == expected)
            alias = value
            copied = value.copy()
            value.append('tail')
            value[-1] = 'changed'
            print(alias is value, copied == expected, value[-1])
            value.pop()
            print(value == expected, value[::-1] == expected[::-1], value + [] == expected, value * 2 == expected * 2)
            """, "True True True\nTrue True changed\nTrue True True True\n");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task PublicProjectionUsesClrListsAndCaptureArrays(bool compiled, string pattern, string text, string expected)
    {
        var script = Compile("import re\nreturn [" + FindAll(compiled, pattern, text) + ", " + expected + "]");
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            var outer = Assert.IsType<List<object?>>(result.ReturnValue);
            var actual = Assert.IsType<List<object?>>(outer[0]);
            var control = Assert.IsType<List<object?>>(outer[1]);
            Assert.Equal(control.Count, actual.Count);
            for (var i = 0; i < actual.Count; i++)
            {
                if (control[i] is object?[] tuple)
                {
                    Assert.Equal(tuple, Assert.IsType<object?[]>(actual[i]));
                }
                else
                {
                    Assert.Equal(Assert.IsType<string>(control[i]), Assert.IsType<string>(actual[i]));
                }
            }
        }
    }

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
    public async Task JsonEncodesResultsAndNestedAliasesWithoutDefault(bool compiled, string route, bool nested)
    {
        var source = Prelude + "\nvalue = " + FindAll(compiled, "'(a)?(b)'", "'b ab b'") + "\n" +
            (nested ? "value = {'alias': value, 'outer': value}\n" : "") +
            Encode(route) + "\nprint(encoded, len(calls))";
        const string json = "[[\"\", \"b\"], [\"a\", \"b\"], [\"\", \"b\"]]";
        var expected = nested ? "{\"alias\": " + json + ", \"outer\": " + json + "}" : json;
        await AssertBothModes(source, expected + " 0\n", requireSuspension: route == "dump");
    }

    [Theory]
    [InlineData("dumps")]
    [InlineData("dump")]
    [InlineData("encode")]
    [InlineData("iterencode")]
    public async Task MutatedResultsUseOrdinaryJsonCycleDetection(string route)
    {
        var source = Prelude + "\nvalue = re.findall('a', 'a')\nvalue.append(value)\ntry:\n" +
            string.Join("\n", Encode(route).Split('\n').Select(line => "    " + line)) +
            "\nexcept ValueError:\n    print('cycle', len(calls))";
        await AssertBothModes(source, "cycle 0\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaterializedResultsStillRespectCollectionLimits(bool compiled)
    {
        var script = Compile("import re\nprint('ready')\nreturn " + FindAll(compiled, "'a'", "'aaaa'"));
        var options = new LythonRunOptions { MaxCollectionSize = 3 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("ready\n", result.StandardOutput);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("maximum collection size", result.Failure?.Message);
        }
    }

    [Fact]
    public async Task LazyDotStarDiscardedListsReclaimOwnedStringsAndStorage()
    {
        await AssertBothModes("""
            import re
            pattern = re.compile('.*?')
            for i in range(10000):
                value = pattern.findall('abcdefgh')
            print(type(value) is list, len(value))
            """, "True 17\n", maxMemory: 1048576);
    }

    [Fact]
    public async Task LazyDotStarRetainedListsRemainCharged()
    {
        var script = Compile("""
            import re
            pattern = re.compile('.*?')
            retained = []
            for i in range(10000):
                retained.append(pattern.findall('abcdefgh'))
            print('unexpected')
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 1048576);
        }
    }

    [Fact]
    public async Task CaptureListProjectionStillRequiresFunding()
    {
        var script = Compile("import re\nreturn re.findall('(a)(b)', 'ab')");
        foreach (var budget in new[] { 32L, 1024L })
        {
            var options = new LythonRunOptions { MaxProjectionMemoryBytes = budget };
            foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
            {
                if (budget == 32)
                {
                    Assert.False(result.Success);
                    Assert.Equal("ProjectionError", result.Failure?.ExceptionType);
                    Assert.Null(result.ReturnValue);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var captures = Assert.IsType<object?[]>(Assert.Single(Assert.IsType<List<object?>>(result.ReturnValue)));
                    Assert.Equal(new object?[] { "a", "b" }, captures);
                }
            }
        }
    }

    [Fact]
    public async Task JsonStillRejectsArbitraryIterators()
        => await AssertBothModes("""
            import json
            try:
                json.dumps(iter(['a']))
            except TypeError:
                print('unsupported')
            """, "unsupported\n");

    private const string Prelude = """
        import re, json
        calls = []
        def bad(value):
            calls.append('default')
            raise AssertionError('default must not run')
        """;

    private static string FindAll(bool compiled, string pattern, string text)
        => compiled ? $"re.compile({pattern}).findall({text})" : $"re.findall({pattern}, {text})";

    private static string Encode(string route)
        => route switch
        {
            "dumps" => "encoded = json.dumps(value, sort_keys=True, default=bad)",
            "encode" => "encoded = json.JSONEncoder(sort_keys=True, default=bad).encode(value)",
            "iterencode" => "encoded = ''.join(json.JSONEncoder(sort_keys=True, default=bad).iterencode(value))",
            "dump" => "with open('/findall.json', 'w') as handle:\n    json.dump(value, handle, sort_keys=True, default=bad)\n" +
                "with open('/findall.json') as handle:\n    encoded = handle.read()",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return script;
    }

    private static async Task AssertBothModes(string source, string expected, bool requireSuspension = false, long? maxMemory = null)
    {
        var script = Compile(source);
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
