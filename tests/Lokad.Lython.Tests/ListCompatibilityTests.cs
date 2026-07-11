using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ListCompatibilityTests
{
    [Fact]
    public void ReverseSortIsStableAndExtendSelfSnapshotsSource()
    {
        var result = new LythonEngine().Run(
            """
rows = [("a", 1), ("b", 1), ("c", 2)]
ordered = sorted(rows, key=lambda row: row[1], reverse=True)
items = [1, 2]
items.extend(items)
return "".join(row[0] for row in ordered) + "|" + str(items)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("cab|[1, 2, 1, 2]", result.ReturnValue);
    }

    [Fact]
    public void ListMethods_FollowCommonPythonSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = ["a", "b", "a", "c"]
values = []
values.append(str(items.index("a")))
values.append(str(items.index("a", 1)))
values.append(str(items.index("a", -4, -1)))
values.append(str(items.count("a")))
items.insert(1, "x")
removed = items.pop(2)
tail = items.pop(-1)
items.remove("a")
reverse_result = items.reverse()
values.append(str(items))
values.append(removed + tail + str(reverse_result))

rows = [(2, "b"), (1, "a"), (2, "a")]
sort_result = rows.sort()
values.append(str(rows))
values.append(str(sort_result))

words = ["bbb", "a", "cc"]
words.sort(key=len, reverse=True)
values.append(str(words))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("0|2|0|2|['a', 'x']|bcNone|[(1, 'a'), (2, 'a'), (2, 'b')]|None|['bbb', 'cc', 'a']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListMethods_ReportPythonShapedMissingValueAndIndexFailures()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []
try:
    [1].index(2)
except ValueError as err:
    values.append(err.message)
try:
    [1].remove(2)
except ValueError as err:
    values.append(err.message)
try:
    [].pop()
except IndexError as err:
    values.append(err.message)
try:
    [1].pop(3)
except IndexError as err:
    values.append(err.message)
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("list.index(value): value is not in list|list.remove(value): value is not in list|pop from empty list|Index is out of range.", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListRepetitionAndAugmentedRepetition_ArePythonShaped()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [0] * 3
left = 2 * [1, 2]
empty = [9] * 0
negative = [9] * -2
alias = ["x"]
same = alias
alias *= 3
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(left) + "|" + str(empty) + "|" + str(negative) + "|" + str(alias) + "|" + str(same is alias))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("[0, 0, 0]|[1, 2, 1, 2]|[]|[]|['x', 'x', 'x']|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListSliceAssignmentAndDeletion_MatchOrdinaryPythonCases()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [0, 1, 2, 3, 4]
items[1:3] = ["a", "b", "c"]
items[:1] = []
items[4:4] = ["x"]
first = str(items)

items = [0, 1, 2, 3, 4, 5]
items[1:5:2] = [10, 30]
del items[::2]
second = str(items)

items = [1, 2, 3]
items[10:1] = [9]
third = str(items)

__lython_file = open("/out.txt", "w")
__lython_file.write(first + "|" + second + "|" + third)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("['a', 'b', 'c', 3, 'x', 4]|[10, 30, 5]|[1, 2, 3, 9]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListSliceAssignment_RejectsMismatchedExtendedSliceLength()
    {
        var result = new LythonEngine().Run(
            """
replacement = []
replacement.append(9)
items = [0, 1, 2, 3]
items[::2] = replacement
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("extended slice", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListAndTupleOrdering_AreLexicographic()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
rows = [(2, "b"), (1, "a"), (2, "a")]
values = []
values.append(str([1, 2] < [1, 3]))
values.append(str([1, 2, 0] > [1, 2]))
values.append(str((1, "a") < (2, "a")))
values.append(str(sorted(rows)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Equal("True|True|True|[(1, 'a'), (2, 'a'), (2, 'b')]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListStaticDiagnostics_CoverNewMemberContracts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
a = [3, 1, 2]
a.insert("0", 1)
b = [3, 1, 2]
b.pop("0")
c = [3, 1, 2]
c.index(1, "0")
d = [3, 1, 2]
d.sort(key=1)
e = [3, 1, 2]
e.sort(reverse="yes")
f = [3, 1, 2]
f.sort(1)
g = [0, 1, 2, 3]
g[::2] = [9]
__lython_file = open("/out.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("insert", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("pop", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("index", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3034" && d.Message.Contains("sort", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3123" && d.Message.Contains("keyword-only", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("Extended slice assignment", StringComparison.Ordinal));
        Assert.False(host.Exists("/out.txt"));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ??
           string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
}
