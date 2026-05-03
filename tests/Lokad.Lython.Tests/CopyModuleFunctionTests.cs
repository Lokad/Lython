using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CopyModuleFunctionTests
{
    [Fact]
    public void CopyModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import copy

shared = [1]
source = [shared]
shallow = copy.copy(source)
deep = copy.deepcopy(source)
shared.append(2)

cyclic = []
cyclic.append(cyclic)
cycle_copy = copy.deepcopy(cyclic)

class Box:
    def __init__(self, value):
        self.value = value
    def __copy__(self):
        return Box(self.value + 1)
    def __deepcopy__(self, memo):
        return Box(self.value + 10)

box = Box(5)
box_copy = copy.copy(box)
box_deep = copy.deepcopy(box)

vals = []
vals.append(str(shallow[0]))
vals.append(str(deep[0]))
vals.append(str(cycle_copy[0] is cycle_copy))
vals.append(str(box_copy.value))
vals.append(str(box_deep.value))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 2]|[1]|True|6|15", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import copy
class Bad:
    __copy__ = 1
copy.copy(Bad())
""",
        "TypeError",
        "__copy__")]
    [InlineData(
        """
import copy
copy.deepcopy(1, 2)
""",
        "TypeError",
        "too many")]
    public void CopyModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
