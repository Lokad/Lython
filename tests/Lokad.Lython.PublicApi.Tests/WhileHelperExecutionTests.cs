using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WhileHelperExecutionTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["loop-control-and-carried-locals", """
            def count(n):
                i=0
                total=0
                while i<n:
                    i+=1
                    if i==2: continue
                    if i==5: break
                    total+=i
                else: total+=100
                return i,total
            print([count(n) for n in [-1,0,1,3,8]])
            """, "[(0, 100), (0, 100), (1, 101), (3, 104), (5, 8)]\n"];
        yield return ["nested-loops-and-return", """
            def compute(n):
                i=0
                total=0
                while i<n:
                    j=0
                    while j<i:
                        total+=i*10+j
                        j+=1
                    i+=1
                return total
            print([compute(n) for n in [0,1,2,4]])
            """, "[0, 0, 10, 144]\n"];
        yield return ["exact-promotion-and-aliases", """
            def grow(value,n):
                first=value
                i=0
                while i<n:
                    value=value*3+1
                    i+=1
                alias=value
                return first,value,alias is value
            print(grow(9223372036854775807,3))
            def divide(n):
                value=-17
                while n>0:
                    value=value//3
                    n-=1
                return value
            print(divide(3))
            """, "(9223372036854775807, 249031044995078946802, True)\n-1\n"];
        yield return ["nonlocal-writes-and-final-values", """
            def outer():
                total=3
                def add(n):
                    nonlocal total
                    while n>0:
                        total+=n
                        n-=1
                    return total
                print(add(3),add(2),total)
            outer()
            """, "9 12 12\n"];
        yield return ["late-locals-observation-and-error-order", """
            def observe(n):
                i=0
                total=0
                while i<n:
                    total+=i
                    i+=1
                return locals()
            values=observe(4)
            print(values['n'],values['i'],values['total'])
            def error(n,divisor):
                i=0
                while i<n:
                    i+=1
                    value=10//divisor
                return value
            for n,d in [(2,0),(0,0),(2,2)]:
                try: print(error(n,d))
                except ZeroDivisionError: print('zero')
                except UnboundLocalError: print('unbound')
            """, "4 4 6\nzero\nunbound\n5\n"];
        yield return ["loop-storage-aliases-survive-promotion", """
            def keep(n):
                i=0
                first=None
                while i<n:
                    items=[i,i+1]
                    if i==0: first=items
                    i+=1
                return first,items
            first,last=keep(4)
            print(first,last,first is last)
            first.append(99)
            print(first,last)
            """, "[0, 1] [3, 4] False\n[0, 1, 99] [3, 4]\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task WhileHelpersMatchPython(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var compiled = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(compiled, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string ReadingLoop = """
        def read_value():
            with open('input.txt') as source: return int(source.read())
        def compute(n):
            total=0
            i=0
            while i<n:
                i+=1
                total+=i
                if i==3: total+=read_value()
            return total
        print(compute(5))
        """;

    [Fact]
    public async Task ActualSuspensionAfterIterationsDoesNotReplayThePrefix()
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
                Assert.Equal("22\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("22\n", result.StandardOutput);
            }
            Assert.Equal(1, immediate.OpenFileCount);
            Assert.Equal(1, immediate.ClosedFileCount);
            Assert.Equal(1, delayed.ClosedFileCount);
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationAfterNativeIterationsJoinsPendingIoAndReturnsAllowance()
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
                Assert.Equal("22\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonallocatingLoopControlRemainsCooperativelyCancellable(bool asynchronous)
    {
        const string source = """
            def wait(flag):
                while flag: pass
                return 7
            return wait(flag)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var warm = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = new Dictionary<string, object?> { ["flag"] = false } }))
            {
                Assert.True(warm.Success, warm.Failure?.Message);
                Assert.Equal(new BigInteger(7), warm.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions { CancellationToken = cancellation.Token, Globals = new Dictionary<string, object?> { ["flag"] = true } };
            var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options) : Task.Run(() => script.Run(new MockLythonHost(), options));
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
    public async Task RefusalInsideALoopRemainsTerminalThroughGuestSuppression()
    {
        const string source = """
            import time
            chunk='x'*65536
            def build(n):
                value=''
                i=0
                while i<n:
                    value=value+chunk
                    i+=1
                return value
            time.time()
            try: build(8)
            except MemoryError: print('must not suppress')
            print('must not continue')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 100_000);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task LoopFormattingReturnsOnlyWorkingStorageAndKeepsRetainedResultsFunded()
    {
        const string source = """
            def make(n):
                i=0
                first=None
                while i<n:
                    value=f'row:{i}:é'
                    if i==0: first=value
                    i+=1
                return first,value
            return make(4)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var first = script.Run(new MockLythonHost()))
            {
                Assert.True(first.Success, first.Failure?.Message);
                Assert.Equal(new object[] { "row:0:é", "row:3:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(first.ReturnValue));
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                using (var second = await script.RunAsync(new MockLythonHost()))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.Equal(new object[] { "row:0:é", "row:3:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(second.ReturnValue));
                    Assert.Equal(new object[] { "row:0:é", "row:3:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(first.ReturnValue));
                    Assert.True(budget.ReservedBytes > retained);
                }
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RecursiveCallsInsideWhileUseTheExistingContinuationAndNestingPolicy()
    {
        const string source = """
            def descend(n):
                while n>0: return 1+descend(n-1)
                return 0
            return descend(100)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(100), result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
