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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 3]|None|[4]|['a', 'b', 'c']", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|[[1], [2]]|[('a', [1]), ('b', [2])]|[[1], [2]]|[]", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4|0|[('a', 4), ('b', 0)]|['a', 'a', 'a', 'a']", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a', 'b']|[2, 1]|[('a', 2), ('b', 1)]|[('a', 2), ('b', 1)]|[]", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deque([3, 2, 1])|deque([1, 2, 3])|deque([])", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_NamedTuple_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import namedtuple

Point = namedtuple("Point", "x y", defaults=[10])
p = Point(1)
q = p._replace(y=3)
made = Point._make([4, 5])
r = Point(1, y=20)

vals = []
vals.append(str(p.x))
vals.append(str(p.y))
vals.append(str(p[0]))
vals.append(str(list(p)))
vals.append(str(p._fields))
vals.append(str(p._field_defaults))
vals.append(str(p._asdict()))
vals.append(str(q))
vals.append(str(made))
vals.append(str(isinstance(p, tuple)))
vals.append(str(isinstance(p, Point)))
vals.append(type(p).__name__)
vals.append(str(p == (1, 10)))
vals.append(str(r))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1|10|1|[1, 10]|('x', 'y')|{'y': 10}|{'x': 1, 'y': 10}|Point(x=1, y=3)|Point(x=4, y=5)|True|True|Point|True|Point(x=1, y=20)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_OrderedDictChainMapAndAbc_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import ChainMap, OrderedDict, UserDict, abc
from collections.abc import Iterable, Mapping

ordered = OrderedDict([("b", 2)], a=1)
chain = ChainMap({"a": 1}, {"a": 0, "b": 2})
chain["c"] = 3
before = chain["a"]
del chain["a"]
child = chain.new_child({"d": 4})

vals = []
vals.append(str(list(ordered.items())))
vals.append(str(before))
vals.append(str(chain["a"]))
vals.append(str(chain["c"]))
vals.append(str(sorted(chain.keys())))
vals.append(str(sorted(chain.items())))
vals.append(str(child["d"]))
vals.append(str(len(child.maps)))
vals.append(str(abc.Sequence))
vals.append(str(Iterable))
vals.append(str(Mapping[str, int]))
try:
    UserDict()
except NotImplementedError as ex:
    vals.append(ex.type)
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[('b', 2), ('a', 1)]|1|0|3|['a', 'b', 'c']|[('a', 0), ('b', 2), ('c', 3)]|4|3|Sequence|Iterable|Mapping[str, int]|NotImplementedError", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DefaultDictKeywordInitializationAndFactoryMutation_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import defaultdict

d = defaultdict(default_factory=list, a=1)
d["b"].append(2)
d.default_factory = None
vals = []
vals.append(str(list(d.items())))
vals.append(str(d.default_factory is None))
try:
    d["missing"]
except KeyError as ex:
    vals.append(ex.type)
d.default_factory = list
d["c"].append(3)
vals.append(str(list(d.items())))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[('a', 1), ('b', [2])]|True|KeyError|[('a', 1), ('b', [2]), ('c', [3])]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_CounterArithmeticAndKeywordUpdates_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter

left = Counter("abb")
left.update(c=3)
left.subtract(b=2, d=1)
right = Counter(a=1, c=1, d=2)

vals = []
vals.append(str(left.total()))
vals.append(str(+left))
vals.append(str(-left))
vals.append(str(left + right))
vals.append(str(left - right))
vals.append(str(left & right))
vals.append(str(left | right))
vals.append(str(Counter(a=1) == Counter({"a": 1, "b": 0})))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3|Counter({'a': 1, 'c': 3})|Counter({'d': 1})|Counter({'a': 2, 'c': 4, 'd': 1})|Counter({'c': 2})|Counter({'a': 1, 'c': 1})|Counter({'a': 1, 'c': 3, 'd': 2})|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_CounterRetainsNumericCountsAndSupportsUnaryFiltering()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter

numeric = Counter(a=1.5, b=2)
numeric.update(a=0.25)
filtered = Counter(a=2, b=-1, c=0)
vals = [str(numeric.total()), str(numeric), str(+filtered), str(-filtered)]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3.75|Counter({'a': 1.75, 'b': 2})|Counter({'a': 2})|Counter({'b': 1})", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DequeBoundedIndexInsertRemoveAndEquality_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

full = deque([1, 2, 3], maxlen=3)
full.append(4)
full.appendleft(1)
full[1] = 9
bounded = deque([1, 2], 3)
bounded.insert(1, 8)
bounded.remove(2)
bounded.extend([3, 4])

vals = []
vals.append(str(full))
vals.append(str(full.index(9)))
try:
    full.insert(1, 8)
except IndexError as ex:
    vals.append(ex.type)
vals.append(str(bounded))
vals.append(str(bounded.maxlen))
vals.append(str(deque([8, 3, 4], maxlen=3) == bounded))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deque([1, 9, 3], maxlen=3)|1|IndexError|deque([8, 3, 4], maxlen=3)|3|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_StaticDiagnosticsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Compile(
            """
from collections import ChainMap, Counter, OrderedDict, abc, defaultdict, deque, namedtuple
from collections.abc import Iterable, Mapping

Point = namedtuple("Point", "x y", defaults=[0])
p = Point(1)
d = defaultdict(list, a=1)
d.default_factory
d.keys()
c = Counter(a=1)
c.update(b=2)
c.subtract({"b": 1})
c.total()
q = deque([1], maxlen=2)
q.append(2)
q.appendleft(0)
q.index(2)
q.insert(1, 9)
q.remove(9)
q.rotate()
q.maxlen
o = OrderedDict([("a", 1)], b=2)
o.keys()
m = ChainMap({"a": 1})
m.maps
m.parents
m.new_child().keys()
abc.Sequence
Iterable
Mapping[str, int]
""");

        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var invalid = new LythonEngine().Compile(
            """
from collections import ChainMap, Counter, OrderedDict, defaultdict, deque, namedtuple

defaultdict(1)
Counter().total(1)
deque().index()
deque().bogus()
ChainMap(a=1)
OrderedDict(1, 2)
namedtuple("T", "a", False, None, None, None)
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Message.Contains("default_factory must be callable or None", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3114" && d.Message.Contains("Counter.total", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3114" && d.Message.Contains("deque.index", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3113" && d.Message.Contains("bogus", StringComparison.Ordinal));
        Assert.True(invalid.Diagnostics.Count(d => d.Code == "LA3151") >= 3);
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
