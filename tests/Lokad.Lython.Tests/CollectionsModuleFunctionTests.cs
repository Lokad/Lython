using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CollectionsModuleFunctionTests
{
    [Fact]
    public void Collections_DefaultDict_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import defaultdict

grouped = defaultdict(list)
grouped["a"].append(1)
grouped["b"].append(2)
grouped["a"].append(3)

vals = []
vals.append(str(grouped["a"]))
vals.append(str(grouped.get("missing")))
vals.append(str(grouped.setdefault("c", [4])))
vals.append(str(sorted(grouped.keys())))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 3]|None|[4]|[a, b, c]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DefaultDict_MethodSurface_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import defaultdict

d = defaultdict(list)
d["a"].append(1)
d["b"].append(2)
copy = d.copy()

vals = []
vals.append(str(d.default_factory is list))
vals.append(str(list(d.values())))
vals.append(str(list(d.items())))
vals.append(str(list(copy.values())))
d.clear()
vals.append(str(list(d.items())))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|[[1], [2]]|[(a, [1]), (b, [2])]|[[1], [2]]|[]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_Counter_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter

counter = Counter("abca")
counter.update({"a": 2})
counter.subtract("bc")

vals = []
vals.append(str(counter["a"]))
vals.append(str(counter["z"]))
vals.append(str(counter.most_common(2)))
vals.append(str(sorted(counter.elements())))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4|0|[(a, 4), (b, 0)]|[a, a, a, a]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_Counter_MethodSurface_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter

counter = Counter({"a": 2, "b": 1})
copy = counter.copy()

vals = []
vals.append(str(list(counter.keys())))
vals.append(str(list(counter.values())))
vals.append(str(list(counter.items())))
vals.append(str(list(copy.items())))
counter.clear()
vals.append(str(list(counter.items())))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[a, b]|[2, 1]|[(a, 2), (b, 1)]|[(a, 2), (b, 1)]|[]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_Deque_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

items = deque([1, 2])
items.append(3)
items.appendleft(0)
left = items.popleft()
right = items.pop()
items.extend([4, 5])
items.extendleft([-1, -2])
items.rotate(1)

vals = []
vals.append(str(left))
vals.append(str(right))
vals.append(str(items))
vals.append(str(items.count(4)))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("0|3|deque([5, -2, -1, 1, 2, 4])|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_Deque_CopyClearAndReverse_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

items = deque([1, 2, 3])
copy = items.copy()
items.reverse()
vals = []
vals.append(str(items))
vals.append(str(copy))
items.clear()
vals.append(str(items))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deque([3, 2, 1])|deque([1, 2, 3])|deque([])", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
from collections import defaultdict
defaultdict(1)["a"]
""",
        "compile",
        "default_factory must be callable or None")]
    [InlineData(
        """
from collections import Counter
Counter().update(1)
""",
        "TypeError",
        "iterable or mapping")]
    [InlineData(
        """
from collections import deque
deque().pop()
""",
        "IndexError",
        "empty deque")]
    public void Collections_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
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
}
