using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SliceStoreExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "native", "x=[0,1,2,3]; x[1:3]=[7,8,9]; print(x)", "[0, 7, 8, 9, 3]\n" },
        { "extended", "x=list(range(6)); x[::2]=[7,8,9]; print(x)", "[7, 1, 8, 3, 9, 5]\n" },
        { "self-alias", "x=[0,1,2,3]; x[1:3]=x; print(x)", "[0, 0, 1, 2, 3, 3]\n" },
        { "locals", "def run(start,stop,step,values):\n x=[0,1,2,3]\n x[start:stop:step]=values\n return x\nprint(run(0,4,2,[7,8]))", "[7, 1, 8, 3]\n" },
        { "chained", "first=[0,1]; second=[2,3]\ntry: first[:]=second[::0]=[7,8]\nexcept ValueError: print(first,second)", "[7, 8] [2, 3]\n" },
        { "loop", "x=[0,1,2,3]\nfor x[1:3] in [[7,8]]: pass\nprint(x)", "[0, 7, 8, 3]\n" },
        { "nested-loop", "x=[0,1,2,3]\nfor x[:],tail in [([7],9)]: pass\nprint(x,tail)", "[7] 9\n" },
        { "comprehension", "x=[0,1,2,3]\nprint([list(x) for x[1:3] in [[7,8]]]); print(x)", "[[0, 7, 8, 3]]\n[0, 7, 8, 3]\n" },
        { "scoped-bound", "x=[0,1,2,3]\nprint([(list(x),index) for index,x[index:] in [(1,[7,8])]])", "[([0, 7, 8], 1)]\n" },
        { "generator", "def run():\n x=[0,1,2]\n x[(yield \"start\"):]=[7]\n yield x\ng=run(); print(next(g),g.send(1))", "start [0, 7]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SliceHeadersPreservePythonValuesAndOrder(string name, string source, string expected)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, name);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/rhs.txt")]
    [InlineData("/receiver.txt")]
    [InlineData("/start.txt")]
    [InlineData("/stop.txt")]
    [InlineData("/step.txt")]
    [InlineData("/bind.txt")]
    [InlineData("/set.txt")]
    public async Task HeadersAndSetterSuspendAndCancelWithoutReplay(string? pausedPath)
    {
        const string source = """
            def mark(path,label,value):
             with open(path,'a') as output: output.write(label)
             return value
            payload=['value']
            start=[]
            stop=[]
            step=[]
            class Box:
             @property
             def __setitem__(self):
              mark('bind.txt','B',None)
              def assign(key,value):
               assert key.start is start and key.stop is stop and key.step is step
               assert value is payload
               mark('set.txt','S',None)
               self.saved=value
              return assign
            box=Box()
            def receiver(): return mark('receiver.txt','R',box)
            receiver()[mark('start.txt','A',start):mark('stop.txt','O',stop):mark('step.txt','T',step)]=mark('rhs.txt','V',payload)
            print(box.saved is payload)
            """;
        var paths = new[] { "/rhs.txt", "/receiver.txt", "/start.txt", "/stop.txt", "/step.txt", "/bind.txt", "/set.txt" };
        var labels = new[] { "V", "R", "A", "O", "T", "B", "S" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
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
                    Assert.Fail(early.Failure?.Message ?? "Slice callback completed before the controlled operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pausedPath is null, result.Success);
            Assert.Equal(pausedPath is null ? "True\n" : "", result.StandardOutput);
            var completed = pausedPath is null ? paths.Length : Array.IndexOf(paths, pausedPath);
            for (var i = 0; i < paths.Length; i++)
                Assert.Equal("before" + (i < completed ? labels[i] : ""), host.ReadText(paths[i]));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task SliceAliasesRemainFundedAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("payload=['x'*4096]\ntarget=[None]\ntarget[:]=[payload]\nassert target[0] is payload\nreturn target", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>();
            var payload = Assert.IsAssignableFrom<System.Collections.IEnumerable>(Assert.Single(rows)).Cast<object?>();
            Assert.Equal(new string('x', 4096), Assert.IsType<string>(Assert.Single(payload)));
            var held = budget.ReservedBytes;
            using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
            Assert.True(held > baseline);
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
