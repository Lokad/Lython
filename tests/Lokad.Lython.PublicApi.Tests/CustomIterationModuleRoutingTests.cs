using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R08: module callables must resolve user-defined <c>__iter__</c> through the
/// context-aware iteration entry point, in both execution modes. Previously
/// these call sites used the context-free overload and failed with
/// <c>TypeError: Object is not iterable</c> for instances with
/// <c>__iter__</c> (e.g. <c>collections.deque(It())</c>).
/// </summary>
public sealed class CustomIterationModuleRoutingTests
{
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    private const string ModuleRoutingSource = """
import collections
import math
import operator
import os
import statistics

class It:
    def __iter__(self):
        return iter([2, 1])

class StrIt:
    def __iter__(self):
        return iter(["/a/b", "/a/c"])

P = collections.namedtuple("P", ["x", "y"], defaults=It())
checks = [
    list(collections.deque(It())),
    list(collections.deque(It(), maxlen=5)),
    sorted(collections.Counter(It()).elements()),
    (P(1).y, P(1, 2).y),
    math.dist(It(), [0, 0]),
    operator.countOf(It(), 1),
    os.path.commonpath(StrIt()),
    statistics.mean(It()),
]
return checks
""";

    private static void CheckRoutingResult(object? returnValue)
    {
        var outer = Assert.IsAssignableFrom<IReadOnlyList<object?>>(returnValue);
        Assert.Equal(8, outer.Count);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, outer[0]);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, outer[1]);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(2) }, outer[2]);
        var point = Assert.IsAssignableFrom<IReadOnlyList<object?>>(outer[3]);
        Assert.Equal(new BigInteger(1), point[0]);
        Assert.Equal(new BigInteger(2), point[1]);
        Assert.Equal(new BigInteger(1), outer[5]);
        Assert.Equal("/a", outer[6]);
        Assert.Equal(1.5, Convert.ToDouble(outer[7]));
    }

    [Fact]
    public void ModuleCallablesResolveCustomIterationSync()
    {
        var result = new LythonEngine().Run(ModuleRoutingSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckRoutingResult(result.ReturnValue);
    }

    private const string UnpackingAndMemberSource = """
import collections
import csv

class It:
    def __iter__(self):
        return iter([2, 1])

class StrIt:
    def __iter__(self):
        return iter(["a", "b"])

a, *b = It()
d = collections.deque([0])
d.extend(It())
d.extendleft(It())
out = open("/out.csv", "w")
w = csv.writer(out)
w.writerow(It())
w.writerows([It(), It()])
out.close()
checks = [
    [a, b],
    list(d),
    ", ".join(StrIt()),
    list(bytes(It())),
    open("/out.csv").read(),
]
return checks
""";

    private static void CheckUnpackingResult(object? returnValue)
    {
        var outer = Assert.IsAssignableFrom<IReadOnlyList<object?>>(returnValue);
        Assert.Equal(5, outer.Count);
        var unpacked = Assert.IsAssignableFrom<IReadOnlyList<object?>>(outer[0]);
        Assert.Equal(new BigInteger(2), unpacked[0]);
        Assert.Equal(new List<object?> { new BigInteger(1) }, unpacked[1]);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(0), new BigInteger(2), new BigInteger(1) },
            outer[1]);
        Assert.Equal("a, b", outer[2]);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, outer[3]);
        Assert.Equal("2,1\n2,1\n2,1\n", outer[4]);
    }

    [Fact]
    public void UnpackingAndMembersResolveCustomIterationSync()
    {
        var result = new LythonEngine().Run(UnpackingAndMemberSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckUnpackingResult(result.ReturnValue);
    }

    [Fact]
    public async Task UnpackingAndMembersResolveCustomIterationAsync()
    {
        var result = await new LythonEngine().RunAsync(UnpackingAndMemberSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckUnpackingResult(result.ReturnValue);
    }

    private const string StatisticsAndDifflibSource = """
import difflib
import statistics

class It:
    def __iter__(self):
        return iter([2, 1])

class StrIt:
    def __iter__(self):
        return iter(["a", "b"])

checks = [
    statistics.correlation(It(), It()),
    list(difflib.unified_diff(StrIt(), StrIt())),
    statistics.fmean(It(), weights=It()),
]
return checks
""";

    private static void CheckStatisticsResult(object? returnValue)
    {
        var outer = Assert.IsAssignableFrom<IReadOnlyList<object?>>(returnValue);
        Assert.Equal(3, outer.Count);
        Assert.Equal(1.0, Convert.ToDouble(outer[0]));
        Assert.Equal(new List<object?>(), outer[1]);
        Assert.Equal(1.6666666666666667, Convert.ToDouble(outer[2]));
    }

    [Fact]
    public void StatisticsAndDifflibResolveCustomIterationSync()
    {
        var result = new LythonEngine().Run(StatisticsAndDifflibSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckStatisticsResult(result.ReturnValue);
    }

    [Fact]
    public async Task StatisticsAndDifflibResolveCustomIterationAsync()
    {
        var result = await new LythonEngine().RunAsync(StatisticsAndDifflibSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckStatisticsResult(result.ReturnValue);
    }

    private const string LazyIteratorSource = """
import itertools

class It:
    def __iter__(self):
        return iter([2, 1])

def show(name, fn):
    try:
        return [name, fn()]
    except TypeError:
        return [name, "TypeError"]

checks = [
    show("zip", lambda: list(zip(It(), [9, 8]))),
    show("map", lambda: list(map(str, It()))),
    show("filter", lambda: list(filter(None, It()))),
    show("enumerate", lambda: list(enumerate(It()))),
    show("iter", lambda: list(iter(It()))),
    show("chain", lambda: list(itertools.chain(It(), [3]))),
    show("from_iterable", lambda: list(itertools.chain.from_iterable([It(), [3]]))),
    show("islice", lambda: list(itertools.islice(It(), 1))),
    show("cycle", lambda: list(itertools.islice(itertools.cycle(It()), 3))),
    show("starmap", lambda: list(itertools.starmap(pow, [It()]))),
    show("compress", lambda: list(itertools.compress(It(), [1, 0]))),
    show("dropwhile", lambda: list(itertools.dropwhile(lambda x: x > 1, It()))),
    show("takewhile", lambda: list(itertools.takewhile(lambda x: x > 1, It()))),
    show("pairwise", lambda: list(itertools.pairwise(It()))),
    show("groupby", lambda: [(k, list(g)) for k, g in itertools.groupby(It())]),
    show("product", lambda: list(itertools.product(It(), repeat=2))[0]),
    show("combinations", lambda: list(itertools.combinations(It(), 2))),
    show("batched", lambda: list(itertools.batched(It(), 2))),
]
return checks
""";

    private static void CheckLazyResult(object? returnValue)
    {
        var outer = Assert.IsAssignableFrom<IReadOnlyList<object?>>(returnValue);
        Assert.Equal(18, outer.Count);
        foreach (var entry in outer)
        {
            var pair = Assert.IsAssignableFrom<IReadOnlyList<object?>>(entry);
            Assert.Equal(2, pair.Count);
            Assert.NotEqual("TypeError", pair[1] as string);
        }
        var names = new List<string>();
        foreach (var entry in outer)
        {
            names.Add(Assert.IsType<string>(Assert.IsAssignableFrom<IReadOnlyList<object?>>(entry)[0]));
        }
        Assert.Equal(new List<string> { "zip", "map", "filter", "enumerate", "iter", "chain", "from_iterable", "islice", "cycle", "starmap", "compress", "dropwhile", "takewhile", "pairwise", "groupby", "product", "combinations", "batched" }, names);
    }

    [Fact]
    public void LazyIteratorsResolveCustomIterationSync()
    {
        var result = new LythonEngine().Run(LazyIteratorSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckLazyResult(result.ReturnValue);
    }

    [Fact]
    public async Task LazyIteratorsResolveCustomIterationAsync()
    {
        var result = await new LythonEngine().RunAsync(LazyIteratorSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckLazyResult(result.ReturnValue);
    }

    [Fact]
    public async Task ModuleCallablesResolveCustomIterationAsync()
    {
        var result = await new LythonEngine().RunAsync(ModuleRoutingSource, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        CheckRoutingResult(result.ReturnValue);
    }
}
