using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceMutationExhaustionTests
{
    [Fact]
    public async Task LiveListCursorSeesReplacementAfterAnAsynchronousKeyCallback()
    {
        const string source = """
            items=[1,2,3]
            def key(value):
                with open('offset.txt') as source: offset=int(source.read())
                if value==1: items[:]=[1,9,3,7]
                return value+offset
            print(max(items,key=key),items)
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/offset.txt", "2");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/offset.txt", "2");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("9 [1, 9, 3, 7]\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("9 [1, 9, 3, 7]\n", result.StandardOutput);
            }
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(4, immediate.ClosedFileCount);
            Assert.Equal(4, delayed.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string Source = """
        items = list(range(8))
        cursor = iter(items)
        print(next(cursor))
        items[:] = list(range(10, 22))
        print(list(cursor))
        items.append(99)
        print(next(cursor, 'exhausted'))
        fresh = iter(items)
        items.clear()
        items.append(101)
        print(list(fresh))
        print([x for x in (3, 4, 5)])
        class Special:
            def __iter__(self):
                yield 'override'
        print(list(Special()))
        events = []
        def values():
            try:
                yield 1
                yield 2
            finally:
                events.append('closed')
        generator = values()
        for value in generator:
            print(value)
            break
        print(events)
        print(next(generator))
        generator.close()
        print(events)
        """;

    [Fact]
    public async Task LiveReplacementExhaustionAndRetainedGeneratorsFollowPython()
    {
        const string expected = "0\n[11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]\nexhausted\n[101]\n[3, 4, 5]\n['override']\n1\n[]\n2\n['closed']\n";
        using var script = new LythonEngine().Compile(Source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
