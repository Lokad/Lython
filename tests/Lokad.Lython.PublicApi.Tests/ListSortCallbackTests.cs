using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ListSortCallbackTests
{
    [Theory]
    [InlineData("items.append(9)", "ValueError [1, 2, 3]\n")]
    [InlineData("items.append(9);items.pop()", "ValueError [1, 2, 3]\n")]
    [InlineData("items.clear()", "ok [1, 2, 3]\n")]
    [InlineData("items.reverse()", "ok [1, 2, 3]\n")]
    [InlineData("items.sort()", "ok [1, 2, 3]\n")]
    [InlineData("items.extend([])", "ok [1, 2, 3]\n")]
    [InlineData("items[:]=[]", "ok [1, 2, 3]\n")]
    [InlineData("items *= 2", "ok [1, 2, 3]\n")]
    public async Task KeyCallbackMutationsFollowPython(string action, string expected)
        => await AssertBoth("""
            items=[3,1,2]
            changed=False
            def key(value):
             global items, changed
             if not changed:
              changed=True
              ACTION
             return value
            try:
             items.sort(key=key)
             print('ok',items)
            except ValueError as error:print(type(error).__name__,items)
            """.Replace("ACTION", action), expected);

    [Fact]
    public async Task KeysSeeAnEmptyReceiverAndFailuresRestoreOriginalItems()
        => await AssertBoth("""
            items=[3,1,2]
            seen=[]
            def key(value):seen.append(len(items));return value
            items.sort(key=key)
            print(items,seen)
            items=[3,1,2]
            def bad(value):
             items.append(9)
             raise LookupError('key')
            try:items.sort(key=bad)
            except LookupError as error:print(str(error),items)
            """, "[1, 2, 3] [0, 0, 0]\nkey [3, 1, 2]\n");

    [Fact]
    public async Task ComparisonsSeeAnEmptyReceiverAndCannotKeepTheirMutations()
        => await AssertBoth("""
            seen=[]
            class Item:
             def __init__(self,value):self.value=value
             def __lt__(self,other):
              seen.append(len(items))
              if not items:items.append(9)
              return self.value<other.value
            items=[Item(3),Item(1),Item(2)]
            try:items.sort()
            except ValueError:print('modified',[item.value for item in items],seen[0])
            """, "modified [1, 2, 3] 0\n");

    private const string HostedSource = """
        items=[3,1,2]
        def key(value):
         assert items==[]
         with open('lookup.txt','a') as target:target.write(str(value))
         return value
        items.sort(key=key)
        return items
        """;

    [Fact]
    public async Task PendingKeysKeepStorageFundedAndResultsSurviveCleanup()
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(HostedSource, budget);
        var baseline = budget.ReservedBytes;
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/lookup.txt", "");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/lookup.txt", "");
            using (var result = mode == "sync" ? script.Run(immediate) : await script.RunAsync(mode == "delayed" ? delayed : immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object[] { BigInteger.One, new BigInteger(2), new BigInteger(3) }, Assert.IsType<List<object?>>(result.ReturnValue));
                Assert.Equal("312", mode == "delayed" ? delayed.ReadText("/lookup.txt") : immediate.ReadText("/lookup.txt"));
                Assert.True(budget.ReservedBytes > baseline);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancellationAwaitsPendingKeyCleanupAndReturnsTheBudget()
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(HostedSource, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/lookup.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/lookup.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellation.Token);
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/lookup.txt"));
        }
        finally { cancellation.Cancel(); using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusalDuringAKeyIsTerminalEvenWhenTheGuestCatchesIt()
    {
        using var script = new LythonEngine().Compile("""
            items=[3,1,2]
            def key(value):
             try:unused=[0]*4096
             except BaseException:pass
             return value
            try:items.sort(key=key)
            except BaseException:print('continued')
            """);
        var budget = new LythonMemoryBudget(131072);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static async Task AssertBoth(string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
