using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening4RegressionTests
{

    [Fact]
    public void CallUnpackingRespectsMemoryBudget()
    {
        using var compiledAdmission1 = new LythonEngine(Utf8RegexProvider.Instance).Compile(            """
def take(*args):
    return len(args)
take(*range(100000))
""");
using         var result = compiledAdmission1.Run(
            new MockLythonHost(),            new LythonRunOptions
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
        using var compiledAdmission2 = new LythonEngine(Utf8RegexProvider.Instance).Compile(            """
def take(*args):
    return len(args)
take(*range(100000))
""");
using         var result = await compiledAdmission2.RunAsync(
            new MockLythonHost(),            new LythonRunOptions
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
        // Exact-sized source capacity fits; source plus a dense copy must not.
        // Relinquish the success control before measuring the copy failure.
        var budget = new LythonMemoryBudget(262144);
        var options = new LythonRunOptions { MemoryBudget = budget };
        const string source = "values = [0] * 2000\n";
        using (var baseline = new LythonEngine(Utf8RegexProvider.Instance).Run(
            source + "return len(values)\n", new MockLythonHost(), options))
        {
            Assert.True(baseline.Success, baseline.Failure?.Message);
        }
        Assert.Equal(0, budget.ReservedBytes);

        using (var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            source + "print(len(values))\nevery = values[::1]\nreturn len(every)\n",
            new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("2000\n", result.StandardOutput);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}


