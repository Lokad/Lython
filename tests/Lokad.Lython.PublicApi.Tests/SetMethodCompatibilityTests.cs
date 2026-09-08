using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetMethodCompatibilityTests
{
    private const string NonMutatingSource = """
source = {1, 2, 3}
same = source
intersection = source.intersection([2, 3, 4], (3, 4))
union = source.union([4], (5,), (x for x in [6]), {7: "seven"})
difference = source.difference([2], (3,))
symmetric = source.symmetric_difference([3, 4])
truthy_numeric = {True}.union([1])
names = dir(source)
required = [
    "difference", "difference_update", "intersection", "intersection_update",
    "isdisjoint", "issubset", "issuperset", "pop", "symmetric_difference",
    "symmetric_difference_update", "union", "update"
]
return "|".join([
    str(sorted(intersection)),
    str(sorted(union)),
    str(sorted(difference)),
    str(sorted(symmetric)),
    str(source is same),
    str(sorted(source)),
    str(len(truthy_numeric)),
    str(source.isdisjoint([4, 5])),
    str(source.isdisjoint((x for x in [0, 2]))),
    str(source.issubset([0, 1, 2, 3, 4])),
    str(source.issuperset({1: "one", 2: "two"})),
    str(source.union() is source),
    str(sorted(source.union())),
    str(all(name in names for name in required))
])
""";

    [Fact]
    public void NonMutatingSetMethods_AcceptOrdinaryIterablesAndPreserveReceiver()
    {
        var result = new LythonEngine().Run(NonMutatingSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[3]|[1, 2, 3, 4, 5, 6, 7]|[1]|[1, 2, 4]|True|[1, 2, 3]|1|True|False|True|True|False|[1, 2, 3]|True",
            result.ReturnValue);
    }

    [Fact]
    public void MutatingSetMethods_SharePythonUpdateAndPopSemantics()
    {
        var result = new LythonEngine().Run(
            """
items = {1, 2, 3}
results = []
results.append(items.update([4], (x for x in [5])))
results.append(items.intersection_update([2, 3, 4, 5], (3, 4, 5)))
results.append(items.difference_update([4]))
results.append(items.symmetric_difference_update([3, 8, 9]))
before_empty_calls = sorted(items)
results.append(items.update())
results.append(items.intersection_update())
results.append(items.difference_update())
popped = items.pop()
return "|".join([
    str(all(value is None for value in results)),
    str(before_empty_calls),
    str(popped + sum(items)),
    str(len(items))
])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|[5, 8, 9]|22|2", result.ReturnValue);
    }

    [Fact]
    public void SetPredicatesAndIntersection_StopWhenTheAnswerIsKnown()
    {
        var result = new LythonEngine().Run(
            """
intersection_items = iter([1, 9])
subset_items = iter([1, 9])
intersection = {1}.intersection(intersection_items)
subset = {1}.issubset(subset_items)
return "|".join([
    str(intersection),
    str(list(intersection_items)),
    str(subset),
    str(list(subset_items)),
    str({1}.intersection([1, []])),
    str({1}.issubset([1, []])),
    str({1}.isdisjoint([1, []])),
    str({1}.issuperset([2, []]))
])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("{1}|[9]|True|[9]|{1}|True|False|False", result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_SetMethodsUseTheSameCompatibilitySurface()
    {
        var result = await new LythonEngine().RunAsync(NonMutatingSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[3]|[1, 2, 3, 4, 5, 6, 7]|[1]|[1, 2, 4]|True|[1, 2, 3]|1|True|False|True|True|False|[1, 2, 3]|True",
            result.ReturnValue);
    }

    [Theory]
    [InlineData("return {1}.union([[2]])\n", "hashable")]
    [InlineData("return {1}.intersection(2)\n", "iterable")]
    [InlineData("return {1}.symmetric_difference()\n", "expects")]
    [InlineData("return {1}.symmetric_difference([2], [3])\n", "expects")]
    [InlineData("return {1}.isdisjoint()\n", "expects")]
    [InlineData("return {1}.union(other={2})\n", "positional")]
    public void InvalidSetMethodOperandsAndCalls_FailExplicitly(string source, string expectedFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        var message = result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
        Assert.Contains(expectedFragment, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetMethodGrowth_ObservesCollectionLimit()
    {
        var result = new LythonEngine().Run(
            "return {1}.union(range(10))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 3 });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size", result.Failure?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PopFromEmptySet_RaisesKeyError()
    {
        var result = new LythonEngine().Run("return set().pop()\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("KeyError", result.Failure?.ExceptionType);
    }
}

