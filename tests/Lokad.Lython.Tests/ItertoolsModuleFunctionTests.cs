using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ItertoolsModuleFunctionTests
{
    [Fact]
    public void ItertoolsModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import itertools

vals = []
vals.append(str(list(itertools.chain([1, 2], [3]))))
vals.append(str(list(itertools.chain.from_iterable([[1], [2, 3]]))))
vals.append(str(list(itertools.islice(range(10), 2, 8, 3))))
it = itertools.chain([1], [2, 3])
vals.append(str(next(it)))
vals.append(str(list(it)))
vals.append(str(list(itertools.product([1, 2], repeat=2))))
vals.append(str(list(itertools.zip_longest([1], [2, 3], fillvalue=0))))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 2, 3]|[1, 2, 3]|[2, 5]|1|[2, 3]|[(1, 1), (1, 2), (2, 1), (2, 2)]|[(1, 2), (0, 3)]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ItertoolsModule_ExhaustionAndBoundaryShapes_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import itertools

vals = []
vals.append(str(list(itertools.islice(range(5), 3))))
vals.append(str(list(itertools.product([1, 2], repeat=0))))
vals.append(str(list(itertools.product([], [1]))))

it = itertools.zip_longest([1], [2, 3])
vals.append(str(next(it)))
vals.append(str(next(it)))
vals.append(str(next(it, "done")))

write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 1, 2]|[()]|[]|(1, 2)|(None, 3)|done", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import itertools
itertools.islice([1, 2], -1)
""",
        "TypeError",
        "non-negative integer")]
    [InlineData(
        """
import itertools
itertools.product([1], other=2)
""",
        "TypeError",
        "repeat")]
    [InlineData(
        """
import itertools
itertools.chain(a=[1])
""",
        "TypeError",
        "does not accept keyword arguments")]
    public void ItertoolsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
