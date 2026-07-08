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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
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

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 1, 2]|[()]|[]|(1, 2)|(None, 3)|done", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ItertoolsModule_ExpandedFiniteIterators_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import itertools

def add(a, b):
    return a + b

def less_than_three(value):
    return value < 3

def is_even(value):
    return value % 2 == 0

vals = []
vals.append(str(list(itertools.islice(itertools.count(1, 2), 5))))
vals.append(str(list(itertools.repeat(7, 3))))
vals.append(str(list(itertools.islice(itertools.cycle([1, 2]), 5))))
vals.append(str(list(itertools.combinations([1, 2, 3], 2))))
vals.append(str(list(itertools.combinations_with_replacement([1, 2], 2))))
vals.append(str(list(itertools.permutations([1, 2, 3], 2))))
vals.append(str(list(itertools.accumulate([1, 2, 3]))))
vals.append(str(list(itertools.accumulate([1, 2], initial=10))))
vals.append(str(list(itertools.compress([10, 20, 30, 40], [1, 0, 1, 0]))))
vals.append(str(list(itertools.filterfalse(None, [0, 1, 0, 2]))))
vals.append(str(list(itertools.dropwhile(less_than_three, [1, 2, 3, 2]))))
vals.append(str(list(itertools.takewhile(less_than_three, [1, 2, 3, 2]))))
vals.append(str(list(itertools.starmap(add, [(2, 3), (4, 5)]))))
vals.append(str(list(itertools.pairwise([1, 2, 3, 4]))))
vals.append(str(list(itertools.batched([1, 2, 3, 4, 5], 2))))
vals.append(str(list(itertools.batched([1, 2, 3, 4], n=2, strict=True))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "[1, 3, 5, 7, 9]|[7, 7, 7]|[1, 2, 1, 2, 1]|[(1, 2), (1, 3), (2, 3)]|[(1, 1), (1, 2), (2, 2)]|[(1, 2), (1, 3), (2, 1), (2, 3), (3, 1), (3, 2)]|[1, 3, 6]|[10, 11, 13]|[10, 30]|[0, 0]|[3, 2]|[1, 2]|[5, 9]|[(1, 2), (2, 3), (3, 4)]|[(1, 2), (3, 4), (5,)]|[(1, 2), (3, 4)]",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void ItertoolsModule_GroupByAndTee_ShareConsumptionLikeCPython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import itertools

groups = []
for key, group in itertools.groupby([1, 1, 2, 2, 1]):
    groups.append((key, list(group)))

source = itertools.count(1)
left, right = itertools.tee(source, 2)

vals = []
vals.append(str(groups))
vals.append(str(next(left)))
vals.append(str(next(left)))
vals.append(str(next(right)))
vals.append(str(next(right)))
vals.append(str(next(right)))
vals.append(str(next(left)))

grouped = itertools.groupby([1, 1, 2])
first_key, first_group = next(grouped)
second_key, second_group = next(grouped)
vals.append(str(first_key))
vals.append(str(list(first_group)))
vals.append(str(second_key))
vals.append(str(list(second_group)))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[(1, [1, 1]), (2, [2, 2]), (1, [1])]|1|2|1|2|3|3|1|[]|2|[2]", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task ItertoolsModule_RunAsync_AwaitsAsyncIteratorSourcesAndCallbacks()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");
        host.SeedFile("/repo/marker.txt", "m");

        var result = await new LythonEngine().RunAsync(
            """
import itertools
import os

def root(row):
    return row[0]

def describe(root, dirs, files):
    open("/repo/marker.txt").read()
    return root

def key(row):
    open("/repo/marker.txt").read()
    return len(row[2])

vals = []
vals.append(",".join([row[0] for row in itertools.islice(os.walk("/repo/docs"), 2)]))
vals.append(",".join(list(map(root, itertools.chain(os.walk("/repo/docs"))))))
vals.append(str(len(list(itertools.product(os.walk("/repo/docs"), [1])))))
vals.append(",".join(list(itertools.starmap(describe, os.walk("/repo/docs")))))

groups = []
for key_value, group in itertools.groupby(os.walk("/repo/docs"), key=key):
    groups.append(str(key_value) + ":" + str(len(list(group))))
vals.append(",".join(groups))

left, right = itertools.tee(os.walk("/repo/docs"), 2)
vals.append(next(left)[0] + ":" + next(right)[0])

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(
            "/repo/docs,/repo/docs/a|/repo/docs,/repo/docs/a,/repo/docs/b|3|/repo/docs,/repo/docs/a,/repo/docs/b|1:3|/repo/docs:/repo/docs",
            host.ReadText("/out.txt"));
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
        "compile",
        "itertools.chain")]
    [InlineData(
        """
import itertools
list(itertools.combinations([1], -1))
""",
        "ValueError",
        "non-negative")]
    [InlineData(
        """
import itertools
list(itertools.batched([1, 2, 3], 2, True))
""",
        "compile",
        "itertools.batched")]
    [InlineData(
        """
import itertools
list(itertools.batched([1], 0))
""",
        "ValueError",
        "at least one")]
    [InlineData(
        """
import itertools
list(itertools.batched([1], 2, strict=True))
""",
        "ValueError",
        "incomplete batch")]
    public void ItertoolsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure!.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ItertoolsModule_StaticDiagnosticsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Compile(
            """
import itertools

def power(a, b):
    return a ** b

itertools.count(start=1, step=2)
itertools.repeat(object=1, times=2)
itertools.cycle([1])
itertools.islice([1, 2, 3], 1, 3)
itertools.product([1], repeat=2)
itertools.zip_longest([1], [2], fillvalue=0)
itertools.combinations(iterable=[1, 2], r=1)
itertools.combinations_with_replacement([1, 2], 2)
itertools.permutations([1, 2], r=1)
itertools.accumulate([1], initial=0)
itertools.compress([1], [1])
itertools.filterfalse(None, [0, 1])
itertools.dropwhile(bool, [0, 1])
itertools.takewhile(bool, [1, 0])
itertools.starmap(power, [(2, 3)])
itertools.pairwise([1, 2])
itertools.groupby([1, 1])
itertools.tee([1], 2)
itertools.batched([1, 2], n=1, strict=True)
""");

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var invalid = new LythonEngine().Compile(
            """
import itertools

itertools.nope()
itertools.count(1, 2, 3)
itertools.repeat()
itertools.cycle()
itertools.combinations([1])
itertools.accumulate([1], None, 0)
itertools.batched([1], 1, True)
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Message.Contains("module 'itertools' has no member 'nope'", StringComparison.Ordinal));
        Assert.True(invalid.Diagnostics.Count(d => d.Code == "LA3151") >= 6);
    }
}
