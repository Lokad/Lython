using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: reassigned ZipInfo date_time tuples commit through the info-owned
/// governor like factory payloads, so mutated infos accumulate instead of
/// riding the assignment budget-free. Other fields alias caller values.
/// </summary>
public sealed class ZipInfoAssignAccountingScenarioTests
{
    // 20k reassigned tuples own 128B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix. The hoisted info and source tuple
    // isolate assignment charges; same-site rereads stay fresh because
    // mutable infos never join the member cache.
    private const long AssignBudgetBytes = 1572864;

    [Fact]
    public async Task ManyReassignedInfosStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            z = zipfile.ZipInfo("a")
            t = (2021, 2, 3, 4, 5, 6)
            objs = []
            i = 0
            while i < 20000:
                z.date_time = t
                objs.append(z.date_time)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = AssignBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= AssignBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= AssignBudgetBytes);
    }

    [Fact]
    public async Task ZipInfoAssignBehaves()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            z = zipfile.ZipInfo("a")
            z.date_time = (2021, 2, 3, 4, 5, 6)
            z.filename = "b"
            z.comment = b"hi"
            return [z.filename, z.date_time, z.comment == b"hi"]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "b",
            new List<object?> { new BigInteger(2021), new BigInteger(2), new BigInteger(3), new BigInteger(4), new BigInteger(5), new BigInteger(6) },
            true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
