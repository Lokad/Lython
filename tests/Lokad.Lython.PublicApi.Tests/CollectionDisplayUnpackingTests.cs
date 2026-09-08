using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CollectionDisplayUnpackingTests
{
    private const string CompatibilitySource = """
events = []

def mark(value):
    events.append(value)
    return value

class Mapping:
    def keys(self):
        return ["custom"]

    def __getitem__(self, key):
        return 9

left = {"x": 1, "same": 0}
right = {"same": 2, "y": 3}
listed = [0, *mark([1, 2]), mark(3), *(x for x in [4, 5])]
tupled = (*mark((6, 7)), mark(8))
setted = {0, *mark([1, 2]), *mark({2, 3})}
mapped = {**mark(left), "z": mark(4), **mark(right), **Mapping()}
returned = *[10, 11], 12
return [str(listed), str(tupled), str(setted), str(mapped), str(returned), str(events)]
""";

    [Fact]
    public void Run_CollectionDisplaysUnpackIterablesAndMappingsLeftToRight()
    {
        var result = new LythonEngine().Run(CompatibilitySource, new MockLythonHost());

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal(
            new object?[]
            {
                "[0, 1, 2, 3, 4, 5]",
                "(6, 7, 8)",
                "{0, 1, 2, 3}",
                "{'x': 1, 'same': 2, 'z': 4, 'y': 3, 'custom': 9}",
                "(10, 11, 12)",
                "[[1, 2], 3, (6, 7), 8, [1, 2], {2, 3}, {'x': 1, 'same': 0}, 4, {'same': 2, 'y': 3}]"
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public async Task RunAsync_CollectionDisplaysMatchSynchronousExecution()
    {
        var result = await new LythonEngine().RunAsync(CompatibilitySource, new MockLythonHost());

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("{'x': 1, 'same': 2, 'z': 4, 'y': 3, 'custom': 9}", Assert.IsType<List<object?>>(result.ReturnValue)[3]);
    }

    [Theory]
    [InlineData("return [*1]\n", "TypeError")]
    [InlineData("return {**[(\"x\", 1)]}\n", "TypeError")]
    [InlineData("return {*[[]]}\n", "TypeError")]
    public void Run_InvalidDisplayUnpackingRaisesPythonShapedErrors(string source, string exceptionType)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
    }

    [Fact]
    public void Compile_RejectsBareStarredExpressions()
    {
        var result = new LythonEngine().Compile("return (*[1, 2])\n");

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "LA2000" && diagnostic.Message.Contains("bare starred expression", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("return {1, 'key': 2}\n", "Cannot mix set items with dictionary entries")]
    [InlineData("return {'key': 1, *[2]}\n", "set unpacking in dictionary display")]
    [InlineData("return {1, **{'key': 2}}\n", "dictionary unpacking in set display")]
    public void Compile_RejectsMixedBraceDisplayKinds(string source, string messageFragment)
    {
        var result = new LythonEngine().Compile(source);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void Run_ExpandedDisplaysRespectCollectionLimits()
    {
        var result = new LythonEngine().Run(
            "return [*range(10)]\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 3 });

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}

