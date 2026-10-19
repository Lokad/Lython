using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetDefaultKeywordTests
{
    [Theory]
    [InlineData(false, "direct")]
    [InlineData(false, "exposed")]
    [InlineData(false, "expanded")]
    [InlineData(false, "unbound")]
    [InlineData(true, "direct")]
    [InlineData(true, "exposed")]
    [InlineData(true, "expanded")]
    public async Task KeywordsFailAfterArgumentEvaluationBeforeKeyProtocolsOrMutation(bool defaultDictionary, string form)
    {
        var call = form switch
        {
            "direct" => "d.setdefault(key=mark('key', Key()), default=mark('default', 7))",
            "exposed" => "method(key=mark('key', Key()), default=mark('default', 7))",
            "expanded" => "d.setdefault(**{'key': mark('key', Key()), 'default': mark('default', 7)})",
            _ => "dict.setdefault(d, key=mark('key', Key()), default=mark('default', 7))",
        };
        var source = """
            import collections
            events = []
            class Key:
                def __hash__(self):
                    events.append('hash')
                    return 1
            def mark(name, value):
                events.append(name)
                return value
            d = DICTIONARY
            method = d.setdefault
            try:
                CALL
                print('accepted')
            except TypeError:
                print('rejected')
            print(events, len(d))
            """.Replace("DICTIONARY", defaultDictionary ? "collections.defaultdict(list)" : "{}")
            .Replace("CALL", call);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("rejected\n['key', 'default'] 0\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CustomMembersStillAcceptTheirDeclaredKeywords()
    {
        using var script = new LythonEngine().Compile("""
            class Custom:
                def setdefault(self, key, default=9):
                    return key, default
            print(Custom().setdefault(key='x', default=7))
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("('x', 7)\n", result.StandardOutput);
        }
    }
}
