using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeferredSplitBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SplitBorrowCopiesMutationsAndProjectionPreservePythonResults(bool asynchronous)
    {
        const string source = """
import copy
a = "é|λ|🙂".split("|")
sliced = a[1:]
a.clear()
b = "a|b|c".split("|")
cloned = copy.deepcopy(b)
b[::2] = ["x", "y"]
c = "left|right".rsplit("|")
aliased = c.copy()
c *= 2
c.pop()
d = "p\nq\n".splitlines()
joined = "/".join(d)
return [sliced, cloned, b, aliased, c, joined]
""";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        const long budget = 1048576;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new object?[] { "λ", "🙂" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new object?[] { "a", "b", "c" }, Assert.IsType<List<object?>>(values[1]));
        Assert.Equal(new object?[] { "x", "b", "y" }, Assert.IsType<List<object?>>(values[2]));
        Assert.Equal(new object?[] { "left", "right" }, Assert.IsType<List<object?>>(values[3]));
        Assert.Equal(new object?[] { "left", "right", "left" }, Assert.IsType<List<object?>>(values[4]));
        Assert.Equal("p/q", values[5]);
        Assert.True(result.PeakExecutionMemoryBytes <= budget);
    }
}
