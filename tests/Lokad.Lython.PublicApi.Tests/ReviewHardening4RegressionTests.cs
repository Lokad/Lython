using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening4RegressionTests
{

    [Fact]
    public void CallUnpackingRespectsMemoryBudget()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
def take(*args):
    return len(args)
take(*range(100000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MemoryBudget = new LythonMemoryBudget(4096)
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("shared memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallUnpackingRespectsMemoryBudgetAsync()
    {
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
def take(*args):
    return len(args)
take(*range(100000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MemoryBudget = new LythonMemoryBudget(4096)
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("shared memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceMaterializationStaysCorrectUnderBudget()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
values = list(range(2000))
every = values[::1]
sparse = values[::100]
letters = tuple(range(200))[10:190:7]
return [len(every), len(sparse), len(letters), every[1999], sparse[0], letters[0]]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MemoryBudget = new LythonMemoryBudget(1048576)
            });

        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void DenseSliceCopyRespectsMemoryBudget()
    {
        // The source alone must fit; adding a full-size copy must not. N06: the
        // 2000 distinct ints adopt one 64 B coupon each beside storage, so the
        // source needs ~166 KiB and the budget moved 64 KiB -> 256 KiB; the copy
        // still denies on its own coupons.
        var options = new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(262144)
        };
        var baseline = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
values = list(range(2000))
return len(values)
""",
            new MockLythonHost(),
            options);
        Assert.True(baseline.Success, baseline.Failure?.Message);

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
values = list(range(2000))
every = values[::1]
return len(every)
""",
            new MockLythonHost(),
            options);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("shared memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }
}


