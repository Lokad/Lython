using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ForHelperExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuiltinForLoopRemainsCooperativelyCancellable(bool asynchronous)
    {
        const string source = """
            def wait(values):
                for item in values: pass
                return 7
            return wait(range(length))
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var warm = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = new Dictionary<string, object?> { ["length"] = 0 } }))
                Assert.True(warm.Success, warm.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions
            {
                CancellationToken = cancellation.Token,
                Globals = new Dictionary<string, object?> { ["length"] = System.Numerics.BigInteger.One << 100 }
            };
            var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
                : Task.Run(() => script.Run(new MockLythonHost(), options));
            LythonExecutionResult? completed = null;
            try
            {
                try { await Task.Delay(50); Assert.False(pending.IsCompleted); }
                finally { cancellation.Cancel(); completed = await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
                Assert.False(completed.Success);
                Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
            }
            finally { completed?.Dispose(); }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReturnedLoopAliasesStayFundedAcrossSharedExecutions()
    {
        const string source = """
            def take(values):
                first=None
                for item in values:
                    if first is None: first=item
                return first,item
            return take([['a'*4096],['b'*4096]])
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var first = script.Run(new MockLythonHost()))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var values = Assert.IsAssignableFrom<IReadOnlyList<object>>(first.ReturnValue);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                using (var second = await script.RunAsync(new MockLythonHost()))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.True(budget.ReservedBytes > retained);
                    Assert.Equal(new string('a', 4096), Assert.IsAssignableFrom<IReadOnlyList<object>>(values[0])[0]);
                }
                Assert.Equal(retained, budget.ReservedBytes);
                Assert.Equal(new string('b', 4096), Assert.IsAssignableFrom<IReadOnlyList<object>>(values[1])[0]);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string ReadingLoop = """
        def read_value():
            with open('input.txt') as source: return int(source.read())
        def compute(values):
            total=0
            for item in values:
                total+=item
                if item==3: total+=read_value()
            return total,item
        print(compute((1,2,3,4,5)))
        """;

    [Fact]
    public async Task ActiveBuiltinCursorTransfersOnceThroughRealIo()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingLoop, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "7");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "7");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("(22, 5)\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("(22, 5)\n", result.StandardOutput);
            }
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(1, immediate.ClosedFileCount);
            Assert.Equal(1, delayed.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationJoinsIoWithAnActiveBuiltinCursor()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingLoop, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/input.txt");
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            LythonExecutionResult? completed = null;
            try
            {
                try
                {
                    await started.WaitAsync(cancellation.Token);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                finally { cancellation.Cancel(); completed = await pending; }
                Assert.False(completed.Success);
                Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
                Assert.Equal("", completed.StandardOutput);
            }
            finally { completed?.Dispose(); }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
            var retry = new MockLythonHost(); retry.SeedFile("/input.txt", "7");
            using (var result = script.Run(retry))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("(22, 5)\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterUserIteratorSuspendsWithoutReplayingTheFirstLoop(bool nested)
    {
        var source = """
            class Values:
                def __iter__(self):
                    with open('input.txt') as source: value=int(source.read())
                    return iter((value,value+1))
            def total(first,second):
                result=0
                for item in first: result+=item
                for item in second: result+=item
                return result,item
            """ + "\n" + (nested ? """
            def outer(values,second):
                result=0
                for item in values: result+=total((item,item+1),second)[0]
                return result,item
            print(outer((1,3),Values()))
            """ : "print(total((1,2,3),Values()))\n");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(nested ? "(40, 3)\n" : "(21, 8)\n", result.StandardOutput);
            }
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(nested ? 2 : 1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> PythonControls()
    {
        // Escaped LF output stays identical to the Python golden under CRLF checkout.
        yield return ["loop-control", """
            def count(values):
                total=0
                item=None
                for item in values:
                    if item==2: continue
                    if item==5: break
                    total+=item
                else: total+=100
                return item,total
            print([count(values) for values in [[],[1],[1,2,3],[1,2,3,4,5,6],(1,2,3),range(3),b'123']])
            def missing(values):
                for item in values: pass
                return item
            try: missing([])
            except UnboundLocalError: print('unbound')
            """, "[(None, 100), (1, 101), (3, 104), (5, 8), (3, 104), (2, 101), (51, 250)]\nunbound\n"];
        yield return ["nested-exits-and-words", """
            def compute(values):
                total=0
                for i in values:
                    for j in (1,2,3):
                        if j==2: continue
                        total+=i*10+j
                        if i==2: break
                    else: total+=100
                return total
            print(compute([0,1,2,3]))
            def words(value):
                first=None
                for word in value.upper().split():
                    if first is None: first=word
                    if word=='STOP': break
                return first,word
            print(words('ßé café stop later'))
            def select(values):
                for item in values:
                    if item>2: return item
                return None
            print(select((1,2,3,4)),select(()))
            """, "413\n('SSÉ', 'STOP')\n3 None\n"];
        yield return ["mutation-and-aliases", """
            def take(values):
                total=0
                for item in values:
                    if item==1: values.append(4)
                    total+=item
                return item,total
            values=[1,2,3]
            print(take(values),values)
            def change(values):
                for item in values:
                    values['new']=9
                return item
            try: change({'a':1,'b':2})
            except RuntimeError: print('changed')
            def keep(values):
                first=None
                for item in values:
                    if first is None: first=item
                return first,item
            values=[[1],[2]]
            first,last=keep(values)
            first.append(9)
            print(first,last,first is values[0],last is values[1])
            """, "(4, 10) [1, 2, 3, 4]\nchanged\n[1, 9] [2] True True\n"];
        yield return ["unicode-and-promotion", """
            def collect(text):
                total=0
                for character in text:
                    total+=len(character)
                return character,total
            print(collect('α😀é𐐨'))
            def grow(values):
                total=9223372036854775807
                for value in values: total=total*3+value
                alias=total
                return total,alias is total
            print(grow((1,2,3)))
            def count(values):
                total=0
                for value in values: total+=1
                return total
            print(count({'a':1,'b':2}),count({1,2,3}))
            """, "('𐐨', 4)\n(249031044995078946807, True)\n2 3\n"];
        yield return ["iterator-and-body-transition", """
            events=[]
            class Values:
                def __iter__(self):
                    events.append('iter')
                    return iter((1,2,3))
            def total(values):
                result=0
                for item in values: result+=item
                return result
            print(total(Values()),events)
            class Number:
                def __radd__(self,other):
                    events.append(other)
                    return other+7
            print(total([1,2,Number(),4]),events)
            """, "6 ['iter']\n14 ['iter', 3]\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task OrdinaryForHelpersPreservePythonBehavior(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
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
        Assert.Equal(0, budget.ReservedBytes);
    }
}
