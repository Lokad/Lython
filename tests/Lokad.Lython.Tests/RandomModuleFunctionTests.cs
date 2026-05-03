using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RandomModuleFunctionTests
{
    [Fact]
    public void RandomModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import random

def snapshot():
    vals = []
    vals.append(str(random.random()))
    vals.append(str(random.randrange(10)))
    vals.append(str(random.randint(3, 7)))
    vals.append(str(random.choice(["a", "b", "c"])))
    vals.append(str(random.choices([10, 20, 30], k=3)))
    vals.append(str(random.choices([10, 20], [0, 1], None, 2)))
    items = [1, 2, 3]
    random.shuffle(items)
    vals.append(str(items))
    vals.append(str(random.sample([1, 2, 3, 4], 2)))
    vals.append(str(random.getrandbits(5)))
    return "|".join(vals)

random.seed(123)
first = snapshot()
random.seed(123)
second = snapshot()
write_text("/out.txt", str(first == second) + "|" + first)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.StartsWith("True|", host.ReadText("/out.txt"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        """
import random
random.choice([])
""",
        "IndexError",
        "empty sequence")]
    [InlineData(
        """
import random
random.randrange(0, 0)
""",
        "ValueError",
        "empty range")]
    [InlineData(
        """
import random
random.choices([1, 2], [0, 0])
""",
        "ValueError",
        "greater than zero")]
    public void RandomModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
