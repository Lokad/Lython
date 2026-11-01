using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceMutationExhaustionTests
{
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
