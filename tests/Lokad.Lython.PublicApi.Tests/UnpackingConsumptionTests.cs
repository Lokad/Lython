using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnpackingConsumptionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "events=[]\ndef values():\n for i in range(5):\n  events.append(i)\n  yield i\ntry: a,b=values()\nexcept ValueError: print(events)", "[0, 1, 2]\n" },
        { "def values():\n yield 1\n yield 2\n yield 3\n raise RuntimeError('later')\nsource=values()\ntry: a,b=source\nexcept Exception as error: print(type(error).__name__)\ntry: next(source)\nexcept Exception as error: print(type(error).__name__)", "ValueError\nRuntimeError\n" },
        { "values=iter(range(5))\ntry: a,b=values\nexcept ValueError: print(next(values,'end'))", "3\n" },
        { "values=iter(range(5))\ntry: a,=values\nexcept ValueError: print(next(values,'end'))", "2\n" },
        { "values=iter(range(1))\na=b='old'\ntry: a,b=values\nexcept ValueError: print(a,b,next(values,'end'))", "old old end\n" },
        { "values=iter(range(2))\na,b=values\nprint(a,b,next(values,'end'))", "0 1 end\n" },
        { "values=iter(range(5))\na=b='old'\ntry: a,(b,c)=[9,values]\nexcept ValueError: print(a,b,next(values,'end'))", "9 old 3\n" },
        { "values=iter(range(5))\na=b='old'\ntry:\n for a,b in [values]: print('body')\nexcept ValueError: print(a,b,next(values,'end'))", "old old 3\n" },
        { "values=iter(range(5))\ntry: result=[(a,b) for a,b in [values]]\nexcept ValueError: print(next(values,'end'))", "3\n" },
        { "values=iter(range(5))\nresult=((a,b) for a,b in [values])\ntry: next(result)\nexcept ValueError: print(next(values,'end'))", "3\n" },
        { "values=iter(range(5))\ndef generate():\n (yield 'receiver').value,other=values\ng=generate()\ntry: next(g)\nexcept ValueError: print(next(values,'end'))", "3\n" },
        { "values=iter(range(5))\na,*rest=values\nprint(a,rest,next(values,'end'))", "0 [1, 2, 3, 4] end\n" },
        { "try: a,b=3\nexcept TypeError: print('not iterable')", "not iterable\n" },
        { "values=iter(range(100000))\ntry: a,b=values\nexcept ValueError: print(next(values,'end'))", "3\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task AssignmentConsumptionMatchesPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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

    [Fact]
    public async Task AliasesRetainedAfterArityFailureRemainFunded()
    {
        const string source = "seen=[]\ndef values():\n for i in range(5):\n  value=['x'*4096]\n  seen.append(value)\n  yield value\ntry: left,right=values()\nexcept ValueError: return seen";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>().ToArray();
                Assert.Equal(3, rows.Length);
                foreach (var row in rows)
                {
                    var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(row).Cast<object?>();
                    Assert.Equal(new string('x', 4096), Assert.IsType<string>(Assert.Single(values)));
                }
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.True(held > baseline);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/next1.txt")]
    [InlineData("/next3.txt")]
    public async Task AsyncNextStopsAtExcessValueAndCancelsWithoutReplay(string? pausedPath)
    {
        const string source = "from pathlib import Path\nclass Values:\n def __init__(self): self.index=0\n def __iter__(self): return self\n def __next__(self):\n  self.index+=1\n  Path('next'+str(self.index)+'.txt').write_text(str(self.index))\n  if self.index>5: raise StopIteration()\n  return self.index\nvalues=Values()\ntry: left,right=values\nexcept ValueError: print('too many',values.index)";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var paths = Enumerable.Range(1, 6).Select(i => "/next" + i + ".txt").ToArray();
        foreach (var path in paths) host.SeedFile(path, "before");
        var entered = pausedPath is null ? null : host.PauseWriteUntilCancellation(pausedPath);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Unpacking finished before its controlled next call suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "too many 3\n" : "", result.StandardOutput);
            var completed = pausedPath is null ? 3 : Array.IndexOf(paths, pausedPath);
            for (var i = 0; i < paths.Length; i++)
                Assert.Equal(i < completed ? (i + 1).ToString() : "before", host.ReadText(paths[i]));
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
}
