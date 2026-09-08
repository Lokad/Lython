using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening4RegressionTests
{
    [Fact]
    public void CallUnpackingRespectsCollectionLimit()
    {
        var result = new LythonEngine().Run(
            """
def take(*args):
    return len(args)
take(*range(1000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxCollectionSize = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallUnpackingRespectsCollectionLimitAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
def take(*args):
    return len(args)
take(*range(1000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxCollectionSize = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CallUnpackingRespectsMemoryBudget()
    {
        var result = new LythonEngine().Run(
            """
def take(*args):
    return len(args)
take(*range(100000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 4096
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallUnpackingRespectsMemoryBudgetAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
def take(*args):
    return len(args)
take(*range(100000))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 4096
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SortedInputRespectsStepBudget()
    {
        var result = new LythonEngine().Run(
            "sorted(range(100000))",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionSteps = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncSortedInputRespectsCollectionLimit()
    {
        var result = await new LythonEngine().RunAsync(
            "sorted(range(5000))",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxCollectionSize = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GzipReadLinesRespectsCollectionLimit()
    {
        var host = new MockLythonHost("/repo");
        var seed = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/lines.gz", "wb") as writer:
    for i in range(500):
        writer.write(b"x\n")
""",
            host);
        Assert.True(seed.Success, seed.Failure?.Message);

        var result = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/lines.gz", "rb") as reader:
    reader.readlines()
return "done"
""",
            host,
            new LythonRunOptions
            {
                MaxCollectionSize = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceMaterializationStaysCorrectUnderBudget()
    {
        var result = new LythonEngine().Run(
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
                MaxExecutionMemoryBytes = 1048576
            });

        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void DenseSliceCopyRespectsMemoryBudget()
    {
        // The source alone must fit; adding a full-size copy must not.
        var options = new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 65536
        };
        var baseline = new LythonEngine().Run(
            """
values = list(range(2000))
return len(values)
""",
            new MockLythonHost(),
            options);
        Assert.True(baseline.Success, baseline.Failure?.Message);

        var result = new LythonEngine().Run(
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
        Assert.Contains("execution memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }
}


