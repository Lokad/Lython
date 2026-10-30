using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ProjectionBoundaryContractTests
{
    [Theory]
    [InlineData("{None:1}")]
    [InlineData("{float('nan'): 1, float('nan'): 2}")]
    [InlineData("{float('nan'), float('nan')}")]
    [InlineData("timedelta(days=999999999)")]
    [InlineData("datetime(2024,1,1,tzinfo=timezone(timedelta(hours=20)))")]
    [InlineData("time(12,0,tzinfo=timezone(timedelta(hours=20)))")]
    [InlineData("time(12,0,tzinfo=timezone(timedelta(seconds=30)))")]
    [InlineData("datetime(1,1,1,tzinfo=timezone(timedelta(hours=1)))")]
    public async Task UnsupportedClrRepresentationsReturnAttributedFailure(string expression)
    {
        var budget = new LythonMemoryBudget(262144);
        foreach (var nested in new[] { false, true })
        {
            using var script = new LythonEngine().Compile("from datetime import datetime,time,timedelta,timezone\nreturn " + (nested ? "[" + expression + "]" : expression));
            Assert.True(script.IsValid);
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }) : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                { Assert.False(result.Success); Assert.Equal(LythonFailureReason.ProjectionRejected, result.FailureAttribution?.Reason); }
                Assert.Equal(0, budget.ReservedBytes);
            }
        }
        using var recovery = new LythonEngine().Compile("return 7");
        using (var result = recovery.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })) Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task MidnightPositiveOffsetHasARepresentableAnchor()
    {
        using var script = new LythonEngine().Compile("from datetime import time,timedelta,timezone\nreturn time(0,0,tzinfo=timezone(timedelta(hours=1)))");
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            var value = Assert.IsType<DateTimeOffset>(result.ReturnValue);
            Assert.Equal(TimeSpan.Zero, value.TimeOfDay);
            Assert.Equal(TimeSpan.FromHours(1), value.Offset);
            Assert.Equal(new DateTime(1, 1, 2), value.Date);
        }
    }

    [Fact]
    public async Task SingleNanAndOrdinaryContainerEntriesRemainRepresentable()
    {
        using var script = new LythonEngine().Compile("return ({float('nan'): 'nan', 'other': 2}, {float('nan'), 7})");
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsType<object[]>(result.ReturnValue);
            Assert.Equal(2, Assert.IsType<Dictionary<object, object?>>(values[0]).Count);
            Assert.Equal(2, Assert.IsType<HashSet<object?>>(values[1]).Count);
        }
    }
}
