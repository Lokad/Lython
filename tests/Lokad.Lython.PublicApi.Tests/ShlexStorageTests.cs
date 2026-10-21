using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ShlexStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommentsBorrowExistingInputRatherThanRetainingDecodedCopy(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import shlex\nreturn len(shlex.split('#" + new string('x', 100000) + "',comments=True))");
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(BigInteger.Zero, result.ReturnValue);
            Assert.True(result.PeakExecutionMemoryBytes < 32768);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedQuotedResultsStayFundedAndShareAllowance(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("import shlex\nreturn shlex.quote(\"a'b \"*1024)", budget);
        var baseline = budget.ReservedBytes;
        LythonExecutionResult? first = null, second = null;
        try
        {
            first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(first.Success, first.Failure?.Message);
            var oneResult = budget.ReservedBytes;
            second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(second.Success, second.Failure?.Message);
            Assert.Equal(first.ReturnValue, second.ReturnValue);
            Assert.True(oneResult > baseline);
            Assert.True(budget.ReservedBytes > oneResult);
            first.Dispose();
            Assert.Equal(oneResult, budget.ReservedBytes);
            Assert.Contains("'\"'\"'", Assert.IsType<string>(second.ReturnValue));
        }
        finally { first?.Dispose(); second?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
