using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImmediateCasedSplitIterationTests
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task LoopsComprehensionsGeneratorsAndDynamicCallsKeepPythonOrder(bool reverse)
    {
        var source = """
            events=[]
            def words(value=' A B C '):
                if value: return value.lower().split()
                return []
            for word in words():
                try:
                    if word=='b': continue
                    events.append(word)
                    if word=='c': break
                finally: events.append('finally:'+word)
            print(word,events)
            print([(prefix,word) for prefix in (1,2) for word in words(' D E ')])
            words.__defaults__=(' NEW VALUE ',)
            print([word for word in words()])
            class Text:
                def lower(self): events.append('lower'); return self
                def split(self): events.append('split'); return ['custom']
            for word in words(Text()): events.append(word)
            print(events[-3:])
            alias=words
            words=lambda value:['rebound']
            for word in words('ignored'): print(word)
            for word in alias(' H '): print(word)
            def generate():
                for word in alias(' X Y '): yield word
            iterator=generate()
            first=next(iterator)
            print(first,list(iterator),first)
            try: raise RuntimeError('outer')
            except RuntimeError as outer:
                for word in alias(' I '):
                    try: raise ValueError(word)
                    except ValueError as error: print(error.__context__ is outer)
            print(type(alias(' J ')).__name__)
            """;
        if (reverse) source = source.Replace(".split()", ".rsplit()").Replace("def split(", "def rsplit(");
        const string expected = "c ['a', 'finally:a', 'finally:b', 'c', 'finally:c']\n[(1, 'd'), (1, 'e'), (2, 'd'), (2, 'e')]\n['new', 'value']\n['lower', 'split', 'custom']\nrebound\nh\nx ['y'] x\nTrue\nlist\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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

    [Theory]
    [MemberData(nameof(CasedWhitespacePipelineTests.UnicodeCases), MemberType = typeof(CasedWhitespacePipelineTests))]
    public async Task DirectAndHelperLoopsRetainIndependentPythonWords(string operation, string text, string[] expected)
    {
        var source = $"""
            def words(value=text): return value.{operation}().split()
            direct=[]
            for word in text.{operation}().split(): direct.append(word)
            borrowed=[]
            for word in words(text): borrowed.append(word)
            keyword=[]
            for word in words(value=text): keyword.append(word)
            defaults=[]
            for word in words(): defaults.append(word)
            assert borrowed==direct and keyword==direct and defaults==direct
            return direct
            """;
        foreach (var program in new[] { source, source.Replace(".split()", ".rsplit()") })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var script = new LythonEngine().Compile(program, budget);
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["text"] = text } };
            using (var first = script.Run(new MockLythonHost(), options))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var retained = budget.ReservedBytes;
                using (var second = await script.RunAsync(new MockLythonHost(), options))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.True(budget.ReservedBytes > retained);
                    foreach (var result in new[] { first, second })
                        Assert.Equal(expected, Assert.IsType<List<object?>>(result.ReturnValue).Select(value => Assert.IsType<string>(value)));
                }
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task PendingBodyIoKeepsWordsFundedAndCleanupAwaited(bool helper)
    {
        var source = "def words(value):return value.upper().split()\nfor word in "
            + (helper ? "words(text)" : "text.upper().split()") + ":\n"
            + " with open('/pause') as source: source.read()\nprint('done')";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/pause", "pause");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/pause");
        var pending = script.RunAsync(host, new LythonRunOptions
        {
            MemoryBudget = budget, CancellationToken = cancellation.Token,
            Globals = new Dictionary<string, object?> { ["text"] = new string('ß', 256) + " next" },
        });
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.DoesNotContain("done", result.StandardOutput);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending;
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var reused = script.Run(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["text"] = "" } }))
            Assert.True(reused.Success, reused.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
