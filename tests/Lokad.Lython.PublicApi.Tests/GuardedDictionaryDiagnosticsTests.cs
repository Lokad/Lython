using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GuardedDictionaryDiagnosticsTests
{
    [Theory]
    [InlineData("'x'", "if key in d:\n    print(d[key])")]
    [InlineData("('x', 1)", "if key in d:\n    print(d[key])")]
    [InlineData("'x'", "assert key not in d or d[key] == 3")]
    [InlineData("('x', 1)", "assert key not in d or d[key] == 3")]
    [InlineData("'x'", "assert not (key in d and d[key] != 3)")]
    [InlineData("('x', 1)", "assert not (key in d and d[key] != 3)")]
    [InlineData("'x'", "value = d[key] if key in d else 3\nassert value == 3")]
    [InlineData("('x', 1)", "value = 3 if key not in d else d[key]\nassert value == 3")]
    public async Task AbsentKeysDoNotDiagnoseUnreachedReads(string key, string guarded)
    {
        await AssertBothModes("d = {}\nkey = " + key + "\n" + guarded +
            "\nd[key] = 3\nprint(d[key])", "3\n");
    }

    [Theory]
    [InlineData("'x'")]
    [InlineData("('x', 1)")]
    [InlineData("b'x'")]
    [InlineData("None")]
    public async Task PresentKeysRemainAccessibleThroughGuards(string key)
    {
        await AssertBothModes("key = " + key + "\nd = {key: 3}\n" +
            "if key in d:\n    assert d[key] == 3\n" +
            "assert key in d and d[key] == 3\n" +
            "print(d[key] if key in d else 0)", "3\n");
    }

    [Theory]
    [InlineData("alias['x'] = 3")]
    [InlineData("alias.update({'x': 3})")]
    [InlineData("alias.setdefault('x', 3)")]
    [InlineData("alias |= {'x': 3}")]
    [InlineData("import operator\noperator.setitem(alias, 'x', 3)")]
    [InlineData("def change():\n    alias['x'] = 3\nchange()")]
    [InlineData("def change(value):\n    value['x'] = 3\nchange(alias)")]
    [InlineData("def change():\n    alias['x'] = 3\n    return True\nflag = False or change()")]
    [InlineData("def change():\n    alias['x'] = 3\n    return True\nflag = change() if True else False")]
    public async Task MutationsAndCallsInvalidateAliasedKeyFacts(string mutation)
    {
        await AssertBothModes("d = {}\nalias = d\n" + mutation + "\n" +
            "if 'x' in d:\n    print(d['x'])", "3\n");
    }

    [Fact]
    public async Task FunctionReturnsAndNestedAliasesDoNotRetainStaleKeyFacts()
    {
        await AssertBothModes("""
            d = {}
            holder = (d,)
            def get():
                return d
            holder[0]['x'] = 3
            print(get()['x'])
            """, "3\n");
    }

    [Theory]
    [InlineData("value = flag and change()", true, "3\n")]
    [InlineData("value = flag and change()", false, "missing\n")]
    [InlineData("value = flag or change()", true, "missing\n")]
    [InlineData("value = flag or change()", false, "3\n")]
    [InlineData("value = change() if flag else False", true, "3\n")]
    [InlineData("value = change() if flag else False", false, "missing\n")]
    public async Task ConditionalCallEffectsJoinBeforeLaterReads(string expression, bool flag, string expected)
    {
        var source = "import sys\nflag = bool(sys.argv)\nd = {}\n" +
            "def change():\n    d['x'] = 3\n    return True\n" + expression + "\n" +
            "try:\n    print(d['x'])\nexcept KeyError:\n    print('missing')\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var options = new LythonRunOptions { Args = flag ? ["sample.py"] : [] };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task RemovedKeysRemainCatchableAfterGuardAndAliasMutation()
    {
        await AssertBothModes("""
            d = {'x': 3}
            alias = d
            assert 'x' in d
            alias.clear()
            try:
                print(d['x'])
            except KeyError:
                print('missing')
            """, "missing\n");
    }

    [Theory]
    [InlineData("d = {}\nd['x']")]
    [InlineData("d = {}\nd[('x', 1)]")]
    [InlineData("d = {'x': 1}\nd['missing']")]
    [InlineData("d = {}\nif 'x' not in d:\n    d['x']")]
    [InlineData("d = {}\n'x' not in d and d['x']")]
    public void ReachedLiteralMissesKeepTheirDiagnostics(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3157");
    }

    [Theory]
    [InlineData("{1: 3}", "True")]
    [InlineData("{1: 3}", "1.0")]
    [InlineData("{1.0: 3}", "1.00")]
    [InlineData("{1_0: 3}", "10")]
    [InlineData("{0x10: 3}", "16")]
    [InlineData("{(1,): 3}", "(True,)")]
    public async Task LiteralKeyFactsDoNotContradictPythonEquality(string dictionary, string key)
    {
        await AssertBothModes("d = " + dictionary + "\nkey = " + key + "\n" +
            "print(d[key], key in d)", "3 True\n");
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
