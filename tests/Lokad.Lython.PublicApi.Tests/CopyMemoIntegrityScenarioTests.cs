using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Stable semantic ids keep distinct live objects separate. The Python memo
/// remains authoritative for aliases, cycles and explicit caller entries.
/// </summary>
public sealed class CopyMemoIntegrityScenarioTests
{
    private const string CheckCopies =
        """
        import copy
        src = [[i] for i in range(20000)]
        dst = copy.deepcopy(src)
        bad = 0
        i = 0
        while i < 20000:
            if dst[i][0] != i:
                bad = bad + 1
            i = i + 1
        return [len(dst), bad]
        """;

    private const string CheckExternalCopies =
        """
        import copy
        src = [[i] for i in range(20000)]
        memo = {}
        a = copy.deepcopy(src, memo)
        b = copy.deepcopy(src, memo)
        bad = 0
        i = 0
        while i < 20000:
            if b[i][0] != i:
                bad = bad + 1
            i = i + 1
        return [len(b), bad, a is b]
        """;

    [Theory]
    [InlineData(false), InlineData(true)]
    public Task DistinctCopiesAndExternalReuse(bool external) => AssertCopies(external, 16);

    [Theory]
    [InlineData(false), InlineData(true)]
    [Trait("Category", "Stress")]
    public Task OriginalCollisionScale(bool external) => AssertCopies(external, 20000);

    private static async Task AssertCopies(bool external, int count)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            (external ? CheckExternalCopies : CheckCopies).Replace("20000", count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(count), BigInteger.Zero };
        if (external) expected.Add(true);
        var budget = new LythonMemoryBudget();
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task UserMemoIdEntryIsHonored()
    {
        // MG03: a pre-seeded memo entry keyed by id() behaves like CPython:
        // the recorded replacement is returned as-is.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import copy\n"
            + "a = [1]\n"
            + "replacement = [\"replacement\"]\n"
            + "memo = {id(a): replacement}\n"
            + "return [copy.deepcopy(a, memo) is replacement]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
