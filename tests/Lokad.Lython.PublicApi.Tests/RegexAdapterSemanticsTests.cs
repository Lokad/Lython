using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterSemanticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReversedSubstitutionExtensionRejectsBeforeCallingReplacement(bool template)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import re\n" +
            "def rewrite(m):\n print('must not run')\n return 'x'\n" +
            "return re.compile(pattern).sub(" + (template ? "'x'" : "rewrite") + ",'ab',pos=2,endpos=0)");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = "" } };
        using var result = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task WindowSubstitutionAndSplitKeepAssertionContextAndAbsoluteCallbackCaptures()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            text='é😀尾'
            p=re.compile('(?<=(é))😀')
            def rewrite(m):
                with open('marker.txt') as marker:marker.read()
                return m.group(1)+':'+str(m.span(1))
            print(p.sub(r'<\1>',text,pos=1,endpos=2))
            print(p.sub(rewrite,text,pos=1,endpos=2))
            print(p.split(text,pos=1,endpos=2))
            print(re.sub('^😀','x',text,pos=1,endpos=2))
            print(re.split('(?<=é)😀',text,pos=1,endpos=2))
            """, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var host = new MockLythonHost(); host.SeedFile("/marker.txt", "ok");
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/marker.txt", "ok");
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("é<é>尾\néé:(0, 1)尾\n['', 'é', '']\né😀尾\n['', '']\n", result.StandardOutput);
                    if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task NativeExpansionKeepsCapturesAndNamesAcrossHostSuspensionAndPurge()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            m=re.match(r'(?P<é>.)(?P<opt>b)?',text)
            with open('marker.txt') as marker:marker.read()
            re.purge()
            print(m.groupdict(),m.lastgroup)
            return m.expand(template)
            """, budget))
        {
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var host = new MockLythonHost(); host.SeedFile("/marker.txt", "ok");
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/marker.txt", "ok");
                var options = new LythonRunOptions
                {
                    Globals = new Dictionary<string, object?> { ["text"] = "😀", ["template"] = @"<\g<é>-\2>\&\377" },
                };
                using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(host, options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("<😀->\\&ÿ", result.ReturnValue);
                    Assert.Equal("{'é': '😀', 'opt': None} é\n", result.StandardOutput);
                    Assert.True(budget.ReservedBytes > baseline);
                    if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Independent frozen CPython 3.13.2 output, not backend-derived expectations.
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RegexAdapter", "cases.json");
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task CaptureProgressionFlagsAndUnicodeMatchPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                    Assert.Empty(result.StandardError);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
