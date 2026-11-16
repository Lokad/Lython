using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FinallyHelperExecutionTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["fallthrough-and-early-return", """
            def compute(n):
                if n < 0: return 7
                try:
                    value = n * 3 + 1
                finally: print('closed', n)
                return value
            print(compute(-1), compute(2))
            def empty():
                try: value = 3
                finally: print('empty')
            print(empty())
            """, "closed 2\n7 7\nempty\nNone\n"];
        yield return ["nested-finally-and-fault", """
            def compute(n):
                try:
                    try: return 12 // n
                    finally: print('inner', n)
                finally: print('outer', n)
            print(compute(3))
            try: compute(0)
            except ZeroDivisionError: print('zero')
            """, "inner 3\nouter 3\n4\ninner 0\nouter 0\nzero\n"];
        yield return ["tuple-return-retains-alias-through-cleanup", """
            def compute(n, kept):
                try: return n + 1000, kept
                finally: kept.append(n)
            kept = []
            value, alias = compute(2, kept)
            print(value, alias is kept, kept)
            """, "1002 True [2]\n"];
        yield return ["cleanup-overrides-return-and-fault", """
            def compute(n):
                try: return 12 // n, n
                finally: return 'replaced', n + 1
            first, second = compute(0)
            print(first, second, compute(3))
            """, "replaced 1 ('replaced', 4)\n"];
        yield return ["prefix-effects-not-replayed", """
            def outer():
                count = 0
                def compute(n):
                    nonlocal count
                    try:
                        count += 1
                        return count, 12 // n
                    finally: print('closed', count)
                print(compute(3))
                try: compute(0)
                except ZeroDivisionError: print('zero', count)
                print(compute(2))
            outer()
            """, "closed 1\n(1, 4)\nclosed 2\nzero 2\nclosed 3\n(3, 6)\n"];
        yield return ["abrupt-loop-exits-own-the-cursor", """
            def compute(n):
                total = 0
                for value in range(n):
                    try:
                        total += value + 1
                        if value % 2 == 0: continue
                        if value > 4: break
                    finally: print('closed', value)
                return total
            print(compute(8))
            """, "closed 0\nclosed 1\nclosed 2\nclosed 3\nclosed 4\nclosed 5\n21\n"];
        yield return ["cleanup-fault-preserves-active-exception", """
            def compute(n):
                try: return 12 // n
                finally: int('broken')
            for n in [0, 3]:
                try: compute(n)
                except ValueError as error:
                    print(None if error.__context__ is None else type(error.__context__).__name__)
            """, "ZeroDivisionError\nNone\n"];
        yield return ["wide-owned-arguments-and-finally", """
            def compute(a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r):
                try: return (a + r) * 3 + 7
                finally: print('closed', a, r)
            print(compute(0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17))
            """, "closed 0 17\n58\n"];
        yield return ["nested-handler-and-finally", """
            def compute(n):
                try:
                    try: return 12 // n
                    except ZeroDivisionError: return 'zero'
                finally: print('closed', n)
            print(compute(3), compute(0))
            """, "closed 3\nclosed 0\n4 zero\n"];
        yield return ["iterated-text-return-survives-cleanup", """
            def words(text):
                try: return text.lower().split()
                finally: print('closed')
            for word in words('A β C'): print(word)
            """, "closed\na\nβ\nc\n"];
        yield return ["cleanup-replaces-private-text-return", """
            def words(text):
                try: return text.lower().split()
                finally: return ['replacement']
            for word in words('A β C'): print(word)
            """, "replacement\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task FinallyHelpersPreservePythonSemantics(string name, string source, string expected)
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

    private const string ReadingFinally = """
        def outer():
            count = 0
            def compute(n):
                nonlocal count
                try:
                    count += 1
                    return 'x' * 4096, n + count
                finally:
                    with open('input.txt') as source: source.read()
            return compute(2), count
        return outer()
        """;

    [Fact]
    public async Task ReturnAndPrefixEffectsSurviveSuspendedCleanup()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingFinally, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var outer = Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue);
                var returned = Assert.IsAssignableFrom<IReadOnlyList<object>>(outer[0]);
                Assert.Equal(new string('x', 4096), returned[0]);
                Assert.Equal(new BigInteger(3), returned[1]);
                Assert.Equal(BigInteger.One, outer[1]);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(1, host.ClosedFileCount);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationInCleanupJoinsIoAndReturnsAllowance()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingFinally, budget))
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
    public async Task EmptyFinallyProtectedLoopRemainsCancellable(bool asynchronous)
    {
        const string source = """
            def spin():
                try:
                    while True: pass
                finally: return 'must not suppress cancellation'
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
    public async Task FinallyReturnCannotSuppressRefusedAdmission()
    {
        const string source = """
            import time
            chunk = 'x' * 65536
            def build():
                try: return chunk + chunk
                finally: return 'must not suppress refusal'
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
