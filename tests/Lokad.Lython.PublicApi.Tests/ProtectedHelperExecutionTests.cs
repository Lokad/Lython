using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ProtectedHelperExecutionTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["normal-else-and-live-defaults", """
            def compute(n, *, divisor=2):
                try: value=n//divisor
                except ZeroDivisionError: return 'zero'
                else: return value+1
            print(compute(8),compute(8,divisor=0))
            compute.__kwdefaults__['divisor']=4
            print(compute(8))
            """, "5 zero\n3\n"];
        yield return ["fault-after-effect-is-not-replayed", """
            def outer():
                count=0
                def compute(n):
                    nonlocal count
                    try:
                        count+=1
                        value=12//n
                        return count,value
                    except ZeroDivisionError:
                        return count,'zero'
                print(compute(3),compute(0),compute(2),count)
            outer()
            """, "(1, 4) (2, 'zero') (3, 6) 3\n"];
        yield return ["nested-child-fault-and-handler-context", """
            def inner(row):
                try: return row['missing']
                except ValueError: return 'wrong'
            def outer(row):
                try: return inner(row)
                except KeyError as error:
                    print(type(error).__name__)
                    try: raise
                    except KeyError: return 'key'
            print(outer({}))
            """, "KeyError\nkey\n"];
        yield return ["working-integers-and-observed-aliases", """
            def compute(n):
                first=n+1000
                alias=first
                try:
                    total=first*3+7
                    result=total//n
                    return first,alias is first,total,result
                except ZeroDivisionError:
                    return first,alias is first,total,'zero'
            print(compute(2),compute(0))
            """, "(1002, True, 3013, 1506) (1000, True, 3007, 'zero')\n"];
        yield return ["loop-fault-and-nested-handler", """
            def compute(values):
                total=0
                try:
                    for value in values: total+=12//value
                except ZeroDivisionError:
                    try: return total//0
                    except ZeroDivisionError: return total
                return total
            print(compute([3,2]),compute([3,0,2]))
            """, "10 4\n"];
        yield return ["handler-header-runs-only-after-the-fault", """
            def selected():
                print('selected')
                return ZeroDivisionError
            def compute(n):
                try: return 12//n
                except selected(): return 'zero'
            print(compute(3))
            print(compute(0))
            """, "4\nselected\nzero\n"];
        yield return ["else-fault-is-outside-the-preceding-handler", """
            def inner(n):
                try: value=12//n
                except ZeroDivisionError: return 'inner'
                else: return value//(n-2)
            def outer(n):
                try: return inner(n)
                except ZeroDivisionError: return 'outer'
            print(outer(0),outer(2),outer(3))
            """, "inner outer 4\n"];
        yield return ["handler-variable-is-cleared-after-transfer", """
            def compute(n):
                error='before'
                try: value=12//n
                except ZeroDivisionError as error: value=3
                try: return value,error
                except UnboundLocalError: return value,'cleared'
            print(compute(2),compute(0))
            """, "(6, 'before') (3, 'cleared')\n"];
        yield return ["handler-error-preserves-exception-context", """
            def compute(n):
                try: return 12//n
                except ZeroDivisionError: return int('broken')
            try: compute(0)
            except ValueError as error:
                print(type(error.__context__).__name__)
            """, "ZeroDivisionError\n"];
        yield return ["wide-helper-fault-keeps-working-values", """
            def compute(p0,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15,p16,p17,divisor=1):
                try:
                    total=(p0+p17)*3+7
                    return total//divisor
                except ZeroDivisionError: return total,p0,p17
            print(compute(0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17))
            print(compute(0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,divisor=0))
            """, "58\n(58, 0, 17)\n"];
        yield return ["finally-retains-return-and-cleanup-semantics", """
            def compute(n):
                try:
                    try: return 12//n
                    except ZeroDivisionError: return 'zero'
                finally: print('closed',n)
            print(compute(3),compute(0))
            """, "closed 3\nclosed 0\n4 zero\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ProtectedHelpersPreservePythonSemantics(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
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

    private const string ReadingHandler = """
        def read_value():
            with open('input.txt') as source: return int(source.read())
        def outer():
            count=0
            def compute(n):
                nonlocal count
                try:
                    count+=1
                    return 12//n
                except ZeroDivisionError: return read_value()+count
            return compute(0),count
        return outer()
        """;

    [Fact]
    public async Task FaultTransferCanSuspendInTheHandlerWithoutReplayingThePrefix()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingHandler, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object[] { new System.Numerics.BigInteger(8), System.Numerics.BigInteger.One },
                    Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue));
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(1, host.ClosedFileCount);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationInTheHandlerJoinsIoBeforeReturningAllowance()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingHandler, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/input.txt");
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal(1, host.ClosedFileCount);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyProtectedLoopRemainsCancellable(bool asynchronous)
    {
        const string source = """
            def spin():
                try:
                    while True: pass
                except Exception: return 'must not suppress cancellation'
            return spin()
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var options = new LythonRunOptions { CancellationToken = cancellation.Token };
            var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
                : Task.Run(() => script.Run(new MockLythonHost(), options));
            LythonExecutionResult? completed = null;
            try
            {
                try
                {
                    await Task.Delay(50);
                    Assert.False(pending.IsCompleted);
                }
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
    public async Task RefusedAdmissionCannotBeSuppressedByTheHelperHandler()
    {
        const string source = """
            import time
            chunk='x'*65536
            def build():
                try: return chunk+chunk
                except MemoryError: return 'must not suppress refusal'
            time.time()
            build()
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
}
