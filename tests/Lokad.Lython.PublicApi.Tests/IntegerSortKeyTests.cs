using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerSortKeyTests
{
    [Fact]
    public async Task ArbitraryPrecisionKeysKeepEqualItemsStableInBothDirections()
    {
        const string source = """
            h = 2 ** 130
            rows = [(h + 1, 'x'), (h, 'a'), (-h, 'n'), (h, 'b')]
            calls = []
            def key(row):
                calls.append(row[1])
                return row[0]
            print([row[1] for row in sorted(rows, key=key)])
            rows.sort(key=key, reverse=True)
            print([row[1] for row in rows])
            print(calls)
            print(sorted([h + 1, h, -h]) == [-h, h, h + 1])
            """;
        await AssertBothModes(source,
            "['n', 'a', 'b', 'x']\n['x', 'a', 'b', 'n']\n['x', 'a', 'n', 'b', 'x', 'a', 'n', 'b']\nTrue\n");
    }

    [Fact]
    public async Task MixedNumericKeysPreservePythonComparisonAndStability()
    {
        const string source = """
            print(sorted([True, 0, 1.5, 1, False]))
            values = [9007199254740993, 9007199254740992.0, 9007199254740992]
            values.sort()
            print(values)
            """;
        await AssertBothModes(source,
            "[0, False, True, 1, 1.5]\n[9007199254740992.0, 9007199254740992, 9007199254740993]\n");
    }

    private static async Task AssertBothModes(string source, string output)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(output, sync.StandardOutput);
        var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(output, asynchronous.StandardOutput);
    }
}
