using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SharedIntegerChainTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "a=12345\nb=6789\nvalues=[]\nfor c in [-7,-1,1,7]:\n    value=(a*b+5000)//c\n    alias=value\n    values.append((value,alias is value,(a-b+17)%c))\nprint(values)\n", "[(-11973601, True, -6), (-83815205, True, 0), (83815205, True, 0), (11973600, True, 1)]\n" },
        { "def values(a,b):\n    result=[]\n    try:\n        for c in [-7,-1,1,7]: result.append(((a*b+5)//c,(a-b+17)%c))\n    finally: print('finished')\n    return result\nfor a in [-9223372036854775808,-9,0,9,9223372036854775807]:\n    print(values(a,7))\n", "finished\n[(9223372036854775807, -5), (64563604257983430651, 0), (-64563604257983430651, 0), (-9223372036854775808, 2)]\nfinished\n[(8, -6), (58, 0), (-58, 0), (-9, 1)]\nfinished\n[(-1, -4), (-5, 0), (5, 0), (0, 3)]\nfinished\n[(-10, -2), (-68, 0), (68, 0), (9, 5)]\nfinished\n[(-9223372036854775808, -4), (-64563604257983430654, 0), (64563604257983430654, 0), (9223372036854775807, 3)]\n" },
        { "class Values:\n    a=50000\n    b=50000\n    result=(a*b+5)//3\n    alias=result\n    observed=locals()['result']\nprint(Values.result,Values.alias is Values.result,Values.observed is Values.result)\n", "833333335 True True\n" },
        { "events=[]\ndef values(a,b):\n    try:\n        for n in [1,2,3]:\n            value=(a*b+5000)//10000+n\n            reply=yield value\n            if reply is not None: a=reply\n    finally: events.append('finished')\ng=values(12345,6789)\nprint(next(g),g.send(30000))\ntry: g.throw(ValueError('stop'))\nexcept ValueError: print(events)\n", "8382 20369\n['finished']\n" },
        { "def outer(a,b):\n    result=0\n    def read(): return result\n    for n in [1,2]:\n        result=(a*b+5000)//10000+n\n        assert read()==result\n    return read\nread=outer(12345,6789)\nprint(read(),read())\n", "8383 8383\n" },
        { "def value(a,b):\n    if False: missing=1\n    try: return (a//b)+missing\n    finally: print('finally')\nfor divisor in [0,1]:\n    try: value(2,divisor)\n    except ZeroDivisionError: print('zero first')\n    except UnboundLocalError: print('local later')\nclass Value:\n    def __mul__(self,other):\n        print('multiply',other)\n        return 11\ndef calculate(a,b):\n    for n in [1]: return a*b+7\nprint(calculate(Value(),3))\n", "finally\nzero first\nfinally\nlocal later\nmultiply 3\n18\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ArithmeticPreservesPublishedValuesScopesProtocolsAndUnwind(string source, string expected)
    {
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingNumericProtocolIsNotReplayedAndKeepsOwnersUntilCleanup(bool cancel)
    {
        const string source = """
            class Value:
                def __mul__(self,other):
                    with open('input.txt') as source: return int(source.read())
            def compute(value):
                for n in [1]: return (value*3+5000)//10000
            return compute(Value())
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "12345");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = cancel ? host.PauseReadUntilCancellation("/input.txt") : Task.CompletedTask;
            var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancel) host.BeforeFileClose = () => { closing.TrySetResult(); return new ValueTask(release.Task); };
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            LythonExecutionResult? completed = null;
            try
            {
                if (cancel)
                {
                    try
                    {
                        await started.WaitAsync(cancellation.Token);
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > baseline);
                        cancellation.Cancel();
                        await closing.Task.WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > baseline);
                    }
                    finally { cancellation.Cancel(); release.TrySetResult(); completed = await pending; }
                    Assert.False(completed.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
                }
                else
                {
                    completed = await pending;
                    Assert.True(completed.Success, completed.Failure?.Message);
                    Assert.Equal(new System.Numerics.BigInteger(1), completed.ReturnValue);
                }
            }
            finally { completed?.Dispose(); }
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryPromotedArithmeticCannotSuppressARefusedAdmission(bool asynchronous)
    {
        const string source = """
            import time
            def compute():
                a=1<<65536
                for b in [7]:
                    time.time()
                    try: return (a*b+5)//3
                    except MemoryError: print('must not suppress')
                    finally: return 'must not replace refusal'
            return compute()
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(new AdmissionGateHost(budget, 0), options)
            : script.Run(new AdmissionGateHost(budget, 0), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonallocatingControlAroundArithmeticRemainsCancellable(bool asynchronous)
    {
        const string source = """
            def compute(flag):
                with open('input.txt') as source:
                    value=0
                    while flag: value=(value+1+2+3)%10000
                return value
            return compute(flag)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var warmHost = new MockLythonHost(); warmHost.SeedFile("/input.txt", "kept");
            using (var warm = script.Run(warmHost, new LythonRunOptions { Globals = new Dictionary<string, object?> { ["flag"] = false } }))
                Assert.True(warm.Success, warm.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
            var host = new MockLythonHost(); host.SeedFile("/input.txt", "kept");
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions { CancellationToken = cancellation.Token, Globals = new Dictionary<string, object?> { ["flag"] = true } };
            var pending = asynchronous ? script.RunAsync(host, options) : Task.Run(() => script.Run(host, options));
            LythonExecutionResult? completed = null;
            try
            {
                try { await Task.Delay(50); Assert.False(pending.IsCompleted); }
                finally { cancellation.Cancel(); completed = await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
                Assert.False(completed.Success);
                Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
            }
            finally { completed?.Dispose(); }
            Assert.Equal(1, host.OpenFileCount);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
