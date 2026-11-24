using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterSemanticsTests
{
    [Fact]
    public async Task ReplacementGroupScanPreservesEscapesAndPositionsAcrossChunks()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            match = re.match('(x)', 'x')
            print(match.expand(r'\g<' + '0' * 2048 + '1>'))
            prefix = r'\g<' + 'a' * 1020
            for suffix in (r'\>tail>', '\\'):
                try:
                    match.expand(prefix + suffix)
                except re.PatternError as error:
                    print(error.msg[:27], error.msg[-8:], error.pos)
            try:
                match.expand(r'\g<尾>')
            except IndexError as error:
                print(str(error))
            """);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("x\nbad character in group name \\\\>tail' 3\nbad escape (end of pattern) pattern) 1023\nunknown group name '尾'\n",
                result.StandardOutput);
        }
    }

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

    [Fact]
    public async Task SavedPatternMethodKeepsItsReceiverAndSparseArguments()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            p=re.compile('(😀)')
            saved=p.search
            re.purge()
            text='é😀尾'
            print(saved.__self__ is p, saved.__name__, saved(text, endpos=2).span())
            print(p.match(text,1,2).group(1), p.fullmatch(text,pos=1,endpos=2).group(1))
            print(p.findall(text,endpos=2), [m.group(1) for m in p.finditer(text,1,2)])
            for values in ({'pos':None}, {'endpos':None}, {'unknown':1}, {'string':text}):
                try:
                    saved(text, **values)
                except TypeError:
                    print('TypeError')
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True search (1, 2)\n😀 😀\n['😀'] ['😀']\nTypeError\nTypeError\nTypeError\nTypeError\n",
                        result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("re.compile('é').search('aéz',Index('pos')).group()", "pos\né")]
    [InlineData("re.compile(',').split('a,b,c',Index('maxsplit'))", "maxsplit\n['a', 'b,c']")]
    [InlineData("re.compile(',').split('a,b,c',Index('maxsplit', 1 << 40))", "maxsplit\n['a', 'b', 'c']")]
    [InlineData("re.compile(',').sub('/', 'a,b,c',Index('count'),pos=Index('pos'),endpos=Index('endpos',4))", "count\npos\nendpos\na/b,c")]
    [InlineData("re.search('é','aéz',pos=Index('pos')).group()", "pos\né")]
    [InlineData("re.split(',','a,b,c',Index('maxsplit'))", "maxsplit\n['a', 'b,c']")]
    [InlineData("re.sub(',','/','a,b,c',Index('count'))", "count\na/b,c")]
    [InlineData("re.subn(',','/','a,b,c',count=Index('count', 1 << 40))", "count\n('a/b/c', 2)")]
    public async Task RegexBoundsAwaitGuestIndexIo(string expression, string expected)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($"""
            import re
            class Index:
                def __init__(self, name, offset=0):
                    self.name = name
                    self.offset = offset
                def __index__(self):
                    with open('index.txt') as source:
                        value = int(source.read())
                    print(self.name)
                    return value + self.offset
            print('entry')
            print({expression})
            """);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var host = new DelayedLythonHost();
        host.SeedFile("/index.txt", "1");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal($"entry\n{expected}\n", result.StandardOutput);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SplitAndSubstitutionAcceptSigned64BitCounts()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            p = re.compile('a')
            for limit in (9223372036854775807, -9223372036854775808, 1 << 63):
                for apply in (lambda: re.sub('a', 'b', 'aaa', count=limit),
                              lambda: p.subn('b', 'aaa', limit),
                              lambda: re.split('a', 'aba', maxsplit=limit),
                              lambda: p.split('aba', limit)):
                    try:
                        print(apply())
                    except OverflowError as error:
                        print(error)
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("bbb\n('bbb', 3)\n['', 'b', '']\n['', 'b', '']\n" +
                        "aaa\n('aaa', 0)\n['aba']\n['aba']\n" +
                        string.Concat(Enumerable.Repeat("Python int too large to convert to C ssize_t\n", 4)),
                        result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("re.compile('é').search('aéz', Index())")]
    [InlineData("re.search('é', 'aéz', pos=Index())")]
    [InlineData("re.compile('é', Flag())")]
    [InlineData("re.compile(re.compile('é'), Flag())")]
    public async Task CancellingARegexArgumentAwaitsFileCleanupAndRefunds(string expression)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($"""
            import re
            class Index:
                def __index__(self):
                    with open('index.txt') as source:
                        return int(source.read())
            class Flag:
                def __bool__(self):
                    return bool(Index().__index__())
                def __and__(self, other):
                    return Index().__index__() & other
            print({expression})
            """, budget);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/index.txt", "1");
        var reading = host.PauseReadUntilCancellation("/index.txt");
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeFileClose = () =>
        {
            closing.TrySetResult();
            return new ValueTask(release.Task);
        };
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            Assert.Same(reading, await Task.WhenAny(reading, pending).WaitAsync(TimeSpan.FromSeconds(10)));
            cancellation.Cancel();
            Assert.Same(closing.Task, await Task.WhenAny(closing.Task, pending).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
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
