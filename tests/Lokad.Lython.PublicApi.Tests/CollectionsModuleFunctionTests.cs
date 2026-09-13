using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
    public void Collections_CounterAndDefaultDict_ViewsAreLive()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter, defaultdict

c = Counter("aab")
live_keys = c.keys()
c.update("c")

d = defaultdict(int, {"a": 1})
live_default_keys = d.keys()
d["b"] = 2

vals = []
vals.append(str(sorted(live_keys)))
vals.append(str(c.keys().isdisjoint(["x"])))
vals.append(str(c.keys().isdisjoint(["a"])))
vals.append(str(sorted(c.keys() & {"a", "b"})))
vals.append(str(c.items().isdisjoint([("a", 2)])))
vals.append(str(sorted(live_default_keys)))
vals.append(str(d.keys().isdisjoint(["x"])))
vals.append(str(d.items().isdisjoint([("a", 1)])))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a', 'b', 'c']|True|False|['a', 'b']|False|['a', 'b']|True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_Counter_UpdateAcceptsMappings()
    {
        // Counter.update/subtract take mapping values (not key counts)
        // from defaultdict and ChainMap sources like CPython.
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter, ChainMap, defaultdict

c = Counter()
c.update(ChainMap({"a": 2, "b": 3}))
c2 = Counter()
c2.update(defaultdict(int, {"x": 4}))
c3 = Counter({"a": 5})
c3.subtract(ChainMap({"a": 2}))
c4 = Counter("aab")
c4.update(Counter({"a": 1}))
vals = []
vals.append(str(c))
vals.append(str(c2))
vals.append(str(c3))
vals.append(str(c4))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Counter({'b': 3, 'a': 2})|Counter({'x': 4})|Counter({'a': 3})|Counter({'a': 3, 'b': 1})", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_ChainMap_ViewsAreLive()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import ChainMap

m = ChainMap({"a": 1}, {"b": 2})
live_keys = m.keys()
live_items = m.items()
m["c"] = 3

vals = []
vals.append(str(sorted(live_keys)))
vals.append(str(sorted(live_items)))
vals.append(str(m.keys().isdisjoint(["x"])))
vals.append(str(m.keys().isdisjoint(["a"])))
vals.append(str(m.items().isdisjoint([("a", 1)])))
vals.append(str(sorted(m.keys() & {"a", "c"})))
vals.append(str(sorted(m.keys() | {"z"})))
vals.append(str(sorted(m.keys() - {"a"})))
vals.append(str(m.keys() == {"a", "b", "c"}))
vals.append(str(m.keys()))
vals.append(str("isdisjoint" in dir(m.keys())))
vals.append(str(len(m.keys())))
vals.append(str("b" in m.keys()))
vals.append(str(sorted(m.values())))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a', 'b', 'c']|[('a', 1), ('b', 2), ('c', 3)]|True|False|False|['a', 'c']|['a', 'b', 'c', 'z']|['b', 'c']|True|KeysView(ChainMap({'a': 1, 'c': 3}, {'b': 2}))|True|3|True|[1, 2, 3]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_ChainMap_DelMiss_NamesKeyLikeCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import ChainMap

cm = ChainMap({"a": 1})
cm["b"] = 2
del cm["b"]

parts = []
parts.append(str(sorted(cm.keys())))
for k in ["zzz", 5, ("x",), chr(34) + "q" + chr(39)]:
    try:
        del cm[k]
        parts.append("no-error")
    except KeyError as e:
        parts.append(str(e))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a']|\"Key not found in the first mapping: 'zzz'\"|'Key not found in the first mapping: 5'|\"Key not found in the first mapping: ('x',)\"|'Key not found in the first mapping: \\'\"q\\\\\\'\\''", host.ReadText("/out.txt"));
    }


    [Fact]
    public void Collections_ViewAndDequeRendering_QuotesElements()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

d = {"a": "x"}
q = deque(["a", 1])

vals = []
vals.append(str(d.keys()))
vals.append(repr(d.keys()))
vals.append(f"{d.keys()}")
vals.append(str(d.values()))
vals.append(str(d.items()))
vals.append(str(q))
vals.append(repr(q))
vals.append(f"{q}")
vals.append(str(deque(["a"], maxlen=2)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("dict_keys(['a'])|dict_keys(['a'])|dict_keys(['a'])|dict_values(['x'])|dict_items([('a', 'x')])|deque(['a', 1])|deque(['a', 1])|deque(['a', 1])|deque(['a'], maxlen=2)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DequeConcat_BehavesLikeCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque
import operator

a = deque([1, 2])
b = deque([3])

parts = []
parts.append(str(a + b))
parts.append(str(deque([1, 2], maxlen=2) + deque([3])))
parts.append(str(deque([1]) + deque([2, 3], maxlen=5)))
parts.append(str(operator.concat(deque([1]), deque([2]))))
parts.append(str(a))

d = deque([1])
hold = d
d += deque([2])
parts.append(str(d))
parts.append(str(hold is d))
d += [3]
parts.append(str(d))
e = deque([1])
e += e
parts.append(str(e))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deque([1, 2, 3])|deque([2, 3], maxlen=2)|deque([1, 2, 3])|deque([1, 2])|deque([1, 2])|deque([1, 2])|True|deque([1, 2, 3])|deque([1, 1])", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DequeRepeat_BehavesLikeCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque
import operator

def mul(a, b):
    return a * b

parts = []
parts.append(str(deque([1, 2]) * 2))
parts.append(str(2 * deque([1])))
parts.append(str(deque([1, 2], maxlen=3) * 2))
parts.append(str(deque([1], maxlen=2) * 0))
parts.append(str(deque([1]) * True))
parts.append(str(operator.mul(deque([1]), 3)))
n = 0 - 1
parts.append(str(deque([1]) * n))

d = deque([1])
hold = d
d *= 2
parts.append(str(d))
parts.append(str(hold is d))
e = deque([1, 2], maxlen=3)
e *= 2
parts.append(str(e))

for pair in [(deque([1]), "a"), ("a", deque([1])), ([1], deque([2]))]:
    try:
        parts.append(str(mul(pair[0], pair[1])))
    except TypeError as ex:
        parts.append(str(ex))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("deque([1, 2, 1, 2])|deque([1, 1])|deque([2, 1, 2], maxlen=3)|deque([], maxlen=2)|deque([1])|deque([1, 1, 1])|deque([])|deque([1, 1])|True|deque([2, 1, 2], maxlen=3)|can't multiply sequence by non-int of type 'str'|can't multiply sequence by non-int of type 'collections.deque'|can't multiply sequence by non-int of type 'collections.deque'", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_DequeErrorTexts_NameDottedType()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

def add(a, b):
    return a + b
def neg(a):
    return -a

d = deque([1])
parts = []
try:
    parts.append(str(neg(d)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(hash(d)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(next(d)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(format(d, "d")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(add([1], d)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(add(d, [2])))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(add((1,), d)))
except TypeError as e:
    parts.append(str(e))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("bad operand type for unary -: 'collections.deque'|unhashable type: 'collections.deque'|'collections.deque' object is not an iterator|unsupported format string passed to collections.deque.__format__|can only concatenate list (not \"collections.deque\") to list|can only concatenate deque (not \"list\") to deque|can only concatenate tuple (not \"collections.deque\") to tuple", host.ReadText("/out.txt"));
    }


    [Fact]
    public void Collections_CounterLengthCountsStoredDistinctKeys()
    {
        var result = new LythonEngine().Run(
            """
from collections import ChainMap, Counter, defaultdict

counter = Counter([1, 1, 2])
lengths = [len(counter)]
counter[3] = 0
lengths.append(len(counter))
counter.subtract([4, 4])
lengths.append(len(counter))
copy = counter.copy()
del counter[2]
lengths.append(len(counter))
lengths.append(len(copy))
counter.clear()
lengths.append(len(counter))
lengths.append(len(defaultdict(int, {"a": 1})))
lengths.append(len(ChainMap({"a": 1}, {"a": 2, "b": 3})))
return lengths
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            new object?[]
            {
                new System.Numerics.BigInteger(2),
                new System.Numerics.BigInteger(3),
                new System.Numerics.BigInteger(4),
                new System.Numerics.BigInteger(3),
                new System.Numerics.BigInteger(4),
                System.Numerics.BigInteger.Zero,
                System.Numerics.BigInteger.One,
                new System.Numerics.BigInteger(2)
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
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
vals.append(str(len(chain)))
vals.append(str(child["d"]))
vals.append(str(len(child)))
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
        Assert.Equal("[('b', 2), ('a', 1)]|1|0|3|['a', 'b', 'c']|[('a', 0), ('b', 2), ('c', 3)]|3|4|4|3|Sequence|Iterable|Mapping[str, int]|NotImplementedError", host.ReadText("/out.txt"));
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
        Assert.Equal("3|Counter({'c': 3, 'a': 1})|Counter({'d': 1})|Counter({'c': 4, 'a': 2, 'd': 1})|Counter({'c': 2})|Counter({'a': 1, 'c': 1})|Counter({'c': 3, 'd': 2, 'a': 1})|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_EmptyCounterRendersBare()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter
vals = [repr(Counter()), str(Counter()), repr(Counter({"a": 1})), f"{Counter()}", "{}".format(Counter())]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Counter()|Counter()|Counter({'a': 1})|Counter()|Counter()", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Collections_CounterDictEqualityFollowsDictRules()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import Counter, defaultdict
vals = []
vals.append(str(Counter([1, 2]) == {1: 1, 2: 1}))
vals.append(str({1: 1, 2: 1} == Counter([1, 2])))
vals.append(str(Counter({"a": 0}) == {}))
vals.append(str(Counter({"a": 1, "b": 0}) == {"a": 1}))
vals.append(str(Counter({"a": 1}) == defaultdict(int, {"a": 1})))
vals.append(str(defaultdict(int, {"a": 1}) == Counter({"a": 1})))
vals.append(str(Counter([1]) != {1: 1}))
vals.append(str(Counter("aab") == {"a": 2, "b": 1}))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|False|False|True|True|False|True", host.ReadText("/out.txt"));
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
        Assert.Equal("3.75|Counter({'b': 2, 'a': 1.75})|Counter({'a': 2})|Counter({'b': 1})", host.ReadText("/out.txt"));
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
    public void Collections_DequeRotateReducesArbitrarySizeIntegersBeforeNarrowing()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from collections import deque

items = deque([1, 2, 3])
items.rotate(1_000_000_000_000)
empty = deque()
empty.rotate(-1_000_000_000_000_000_000_000)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(list(items)) + "|" + str(list(empty)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[3, 1, 2]|[]", host.ReadText("/out.txt"));
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
from collections import Counter
Counter("aab").keys().isdisjoint(1)
""",
        "TypeError",
        "'int' object is not iterable")]
    [InlineData(
        """
from collections import Counter
Counter("aab").keys().isdisjoint([[]])
""",
        "TypeError",
        "hashable")]
    [InlineData(
        """
from collections import defaultdict
defaultdict(int, {"a": 1}).items().isdisjoint(5)
""",
        "TypeError",
        "'int' object is not iterable")]
    [InlineData(
        """
from collections import ChainMap
ChainMap({"a": 1}).keys().isdisjoint(1)
""",
        "TypeError",
        "'int' object is not iterable")]
    [InlineData(
        """
from collections import ChainMap
ChainMap({"a": 1}).keys().isdisjoint([[]])
""",
        "TypeError",
        "hashable")]
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
            Assert.Equal(exceptionType, result.Failure?.ExceptionType);
            Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DefaultDictCounterKeysQuoteLikePython()
    {
        // defaultdict and Counter string keys render through repr like
        // CPython on the shared non-interpolated path, so quoting and
        // escapes match plain-dict rendering (single-key Counter rows keep
        // the test free of the separate most-common ordering gap).
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter
            results = []
            d = defaultdict(list)
            d["it's"] = [1]
            d["plain"] = 2
            d[7] = 3
            results.append(str(d))
            results.append(repr(d))
            c = Counter()
            c["it's"] = 2
            results.append(str(c))
            results.append(repr(c))
            e = defaultdict(list)
            results.append(str(e))
            results.append(repr(e))
            results.append(f"{d}")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "defaultdict(<class 'list'>, {\"it's\": [1], 'plain': 2, 7: 3})",
            "defaultdict(<class 'list'>, {\"it's\": [1], 'plain': 2, 7: 3})",
            "Counter({\"it's\": 2})",
            "Counter({\"it's\": 2})",
            "defaultdict(<class 'list'>, {})",
            "defaultdict(<class 'list'>, {})",
            "defaultdict(<class 'list'>, {\"it's\": [1], 'plain': 2, 7: 3})",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CounterReprOrdersByMostCommon()
    {
        // Like CPython repr, Counter entries order by most-common count
        // with ties in insertion order, while unorderable values keep
        // insertion order; string values render through repr on the same
        // path (float rows and the most_common error shape stay out).
        var script = new LythonEngine().Compile("""
            from collections import Counter, defaultdict
            results = []
            c = Counter()
            c["a"] = 3
            c["b"] = 1
            c["c"] = 2
            results.append(str(c))
            results.append(repr(c))
            t = Counter()
            t["x"] = 1
            t["y"] = 1
            t["z"] = 1
            results.append(str(t))
            results.append(repr(t))
            z = Counter()
            z["a"] = 0
            z["n"] = -2
            z["p"] = 5
            results.append(str(z))
            results.append(repr(z))
            s = Counter()
            s["a"] = 'x'
            s["b"] = 1
            results.append(str(s))
            results.append(repr(s))
            d = defaultdict(list)
            d["k"] = 'v'
            results.append(str(d))
            results.append(repr(d))
            results.append(str(Counter()))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "Counter({'a': 3, 'c': 2, 'b': 1})",
            "Counter({'a': 3, 'c': 2, 'b': 1})",
            "Counter({'x': 1, 'y': 1, 'z': 1})",
            "Counter({'x': 1, 'y': 1, 'z': 1})",
            "Counter({'p': 5, 'a': 0, 'n': -2})",
            "Counter({'p': 5, 'a': 0, 'n': -2})",
            "Counter({'a': 'x', 'b': 1})",
            "Counter({'a': 'x', 'b': 1})",
            "defaultdict(<class 'list'>, {'k': 'v'})",
            "defaultdict(<class 'list'>, {'k': 'v'})",
            "Counter()",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CounterMostCommonReportsComparisonTypeError()
    {
        // most_common on unorderable values surfaces the comparison
        // TypeError like CPython instead of the BCL comparer wrap; funded
        // paths keep most-common order in both modes.
        var script = new LythonEngine().Compile("""
            from collections import Counter
            results = []
            c = Counter()
            c["a"] = 'x'
            c["b"] = 1
            try:
                c.most_common()
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            results.append(str(Counter({"p": 1, "q": 2}).most_common()))
            results.append(str(Counter('aab').most_common()))
            results.append(str(Counter({"p": 1, "q": 2}).most_common(1)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "TypeError",
            "'<' not supported between instances of 'str' and 'int'",
            "[('q', 2), ('p', 1)]",
            "[('a', 2), ('b', 1)]",
            "[('q', 2)]",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CounterReprOrdersDecimalCounts()
    {
        // Decimal counts order numerically in repr like most_common,
        // mirroring the PyComparison domain order in both modes.
        var script = new LythonEngine().Compile("""
            from decimal import Decimal
            from collections import Counter
            results = []
            d = Counter()
            d['a'] = Decimal('1.5')
            d['b'] = Decimal('2')
            d['c'] = Decimal('0.25')
            results.append(str(d))
            results.append(repr(d))
            results.append(str(d.most_common()))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "Counter({'b': Decimal('2'), 'a': Decimal('1.5'), 'c': Decimal('0.25')})",
            "Counter({'b': Decimal('2'), 'a': Decimal('1.5'), 'c': Decimal('0.25')})",
            "[('b', Decimal('2')), ('a', Decimal('1.5')), ('c', Decimal('0.25'))]",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MappingUnionOperatorsAdvanceLikeCpython()
    {
        // Mapping | builds a fresh merged mapping (defaultdict keeps the
        // left factory, Counter mixes merge plain) and |= merges in place
        // with Counter max-plus-purge semantics, like CPython.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, ChainMap
            results = []
            a = defaultdict(list, {"x": [1]})
            b = {"y": 2}
            results.append(str(a | b))
            results.append(str(b | a))
            results.append(str((a | b).default_factory.__name__))
            results.append(str((b | a).default_factory.__name__))
            l = defaultdict(list, {"a": [1]})
            r = defaultdict(int, {"b": 2})
            results.append(str(l | r))
            results.append(str((l | r).default_factory.__name__))
            results.append(str(r | l))
            results.append(str(Counter("aab") | {"a": 1, "z": 9}))
            results.append(str({"a": 1, "z": 9} | Counter("aab")))
            results.append(str(defaultdict(list, {"a": [9]}) | Counter({"a": 1, "b": 2})))
            results.append(str(Counter({"a": 1}) | defaultdict(list, {"a": [9]})))
            def union_arg(x, y):
                return x | y
            try:
                union_arg(defaultdict(list), [1, 2])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            c = Counter("aab")
            alias = c
            c |= Counter("abb")
            results.append(str(c is alias))
            results.append(str(c))
            c2 = Counter({"a": -1, "b": 2})
            c2 |= Counter()
            results.append(str(c2))
            dd = defaultdict(list, {"a": [1]})
            dalias = dd
            dd |= {"b": 2}
            results.append(str(dd is dalias))
            results.append(str(dd))
            d = {"x": 0}
            dalias2 = d
            d |= Counter({"a": 1})
            results.append(str(d is dalias2))
            results.append(str(d))
            results.append(type(d).__name__)
            cm = ChainMap({"a": 1})
            cmalias = cm
            cm |= {"b": 2}
            results.append(str(cm is cmalias))
            results.append(str(list(cm.items())))
            cm |= [("c", 3)]
            results.append(str(list(cm.items())))
            def iunion_arg(x, y):
                x |= y
                return x
            try:
                iunion_arg(Counter(), [(1, 2)])
            except AttributeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "defaultdict(<class 'list'>, {'x': [1], 'y': 2})",
            "defaultdict(<class 'list'>, {'y': 2, 'x': [1]})",
            "list",
            "list",
            "defaultdict(<class 'list'>, {'a': [1], 'b': 2})",
            "list",
            "defaultdict(<class 'int'>, {'b': 2, 'a': [1]})",
            "{'a': 1, 'b': 1, 'z': 9}",
            "{'a': 2, 'z': 9, 'b': 1}",
            "defaultdict(<class 'list'>, {'a': 1, 'b': 2})",
            "defaultdict(<class 'list'>, {'a': [9]})",
            "TypeError",
            "unsupported operand type(s) for |: 'collections.defaultdict' and 'list'",
            "True",
            "Counter({'a': 2, 'b': 2})",
            "Counter({'b': 2})",
            "True",
            "defaultdict(<class 'list'>, {'a': [1], 'b': 2})",
            "True",
            "{'x': 0, 'a': 1}",
            "dict",
            "True",
            "[('a', 1), ('b', 2)]",
            "[('a', 1), ('b', 2), ('c', 3)]",
            "AttributeError",
            "'list' object has no attribute 'items'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ChainMapUnionOperatorsAdvanceLikeCpython()
    {
        // Unions involving a ChainMap build a fresh ChainMap (copied
        // first map plus shared tails, or one merged map), like CPython,
        // except multi-map key order follows the house first-map-first
        // merge rather than CPython last-map-first.
        var script = new LythonEngine().Compile("""
            from collections import ChainMap, Counter, defaultdict
            results = []
            m = {"c": 3}
            cm = ChainMap({"a": 1}, {"b": 2})
            r = m | cm
            results.append(str(list(r.items())))
            results.append(str(len(r.maps)))
            r2 = cm | {"c": 3}
            results.append(str(list(r2.items())))
            results.append(str(len(r2.maps)))
            cm.maps[1]["b"] = 99
            results.append(str(r2["b"]))
            c1 = ChainMap({"a": 1}, {"b": 2})
            c2 = ChainMap({"c": 3})
            results.append(str(list((c1 | c2).items())))
            results.append(str(Counter({"x": 1}) | ChainMap({"a": 2})))
            results.append(str(defaultdict(list, {"x": [1]}) | ChainMap({"a": 2})))
            results.append(str(({"a": 0, "c": 3} | ChainMap({"a": 1, "b": 2}))["a"]))
            def union_arg(x, y):
                return x | y
            try:
                union_arg(ChainMap({"a": 1}), [(1, 2)])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                union_arg([(1, 2)], ChainMap({"a": 1}))
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "[('c', 3), ('a', 1), ('b', 2)]",
            "1",
            "[('a', 1), ('c', 3), ('b', 2)]",
            "2",
            "99",
            "[('a', 1), ('c', 3), ('b', 2)]",
            "ChainMap({'x': 1, 'a': 2})",
            "ChainMap({'x': [1], 'a': 2})",
            "1",
            "TypeError",
            "unsupported operand type(s) for |: 'ChainMap' and 'list'",
            "TypeError",
            "unsupported operand type(s) for |: 'list' and 'ChainMap'",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ChainMapUpdateWritesFirstMapLikeCpython()
    {
        // ChainMap.update writes mappings, pair sequences and keywords
        // into the first map like CPython.
        var script = new LythonEngine().Compile("""
            from collections import ChainMap
            results = []
            cm = ChainMap({"a": 1})
            results.append(str(cm.update({"b": 2})))
            results.append(str(list(cm.items())))
            cm.update([("c", 3)], d=4)
            results.append(str(sorted(cm.items())))
            cm2 = ChainMap({"a": 1}, {"b": 2})
            cm2.update({"c": 3})
            results.append(str(list(cm2.maps[0].items())))
            results.append(str(list(cm2.maps[1].items())))
            def update_arg(m, a, b):
                return m.update(a, b)
            try:
                update_arg(ChainMap({}), {"a": 1}, {"b": 2})
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            results.append(str(hasattr(ChainMap({}), "update")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "None",
            "[('a', 1), ('b', 2)]",
            "[('a', 1), ('b', 2), ('c', 3), ('d', 4)]",
            "[('a', 1), ('c', 3)]",
            "[('b', 2)]",
            "TypeError",
            "ChainMap.update expected at most 1 positional argument.",
            "True",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CollectionTypesWorkInIsinstanceAndIssubclass()
    {
        // defaultdict, Counter, deque and ChainMap are first-class types
        // for isinstance (with dict-subclass relations), like CPython.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            results = []
            results.append(str(isinstance(defaultdict(list), defaultdict)))
            results.append(str(isinstance(Counter(), Counter)))
            results.append(str(isinstance(deque(), deque)))
            results.append(str(isinstance(ChainMap(), ChainMap)))
            results.append(str(isinstance(defaultdict(list), dict)))
            results.append(str(isinstance(Counter(), dict)))
            results.append(str(isinstance(deque(), dict)))
            results.append(str(isinstance({}, defaultdict)))
            results.append(str(isinstance({}, (list, defaultdict))))
            results.append(str(issubclass(defaultdict, dict)))
            results.append(str(issubclass(Counter, dict)))
            results.append(str(issubclass(dict, defaultdict)))
            results.append(str(issubclass(defaultdict, defaultdict)))
            try:
                issubclass([], dict)
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                isinstance({}, 42)
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "False",
            "False",
            "False",
            "True",
            "True",
            "False",
            "True",
            "TypeError",
            "issubclass() arg 1 must be a class",
            "TypeError",
            "isinstance() arg 2 must be a type, a tuple of types, or a union",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CollectionFactoriesExposeTypeNamesLikeCpython()
    {
        // Collection factories report short __name__/__qualname__ plus
        // the collections __module__, like CPython type objects.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            results = []
            results.append(str(defaultdict.__name__))
            results.append(str(Counter.__name__))
            results.append(str(deque.__name__))
            results.append(str(ChainMap.__name__))
            results.append(str(defaultdict.__module__))
            results.append(str(Counter.__module__))
            results.append(str(defaultdict.__qualname__))
            results.append(str(ChainMap.__qualname__))
            results.append(str(defaultdict.__name__ is defaultdict.__name__))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "defaultdict",
            "Counter",
            "deque",
            "ChainMap",
            "collections",
            "collections",
            "defaultdict",
            "ChainMap",
            "True",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CollectionFactoriesExposeBasesLikeCpython()
    {
        // Collection factories report CPython-style __bases__ (dict for
        // the dict-backed kinds, object otherwise since ABCs are absent)
        // and an __mro__ running through object.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            results = []
            results.append(str(defaultdict.__bases__))
            results.append(str(Counter.__bases__))
            results.append(str(deque.__bases__))
            results.append(str(ChainMap.__bases__))
            results.append(str([b.__name__ for b in defaultdict.__mro__]))
            results.append(str([b.__name__ for b in deque.__mro__]))
            results.append(str([b.__name__ for b in ChainMap.__mro__]))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "(<class 'dict'>,)",
            "(<class 'dict'>,)",
            "(<class 'object'>,)",
            "(<class 'object'>,)",
            "['defaultdict', 'dict', 'object']",
            "['deque', 'object']",
            "['ChainMap', 'object']",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DictDequeOperatorDundersAdvanceLikeCpython()
    {
        // dict __or__/__ror__/__ior__ merge plainly (NotImplemented
        // outside mappings) and deque __add__/__mul__/__rmul__ repeat
        // through the shared cores, like CPython.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque
            def call2(f, a, b):
                return f(a, b)
            results = []
            results.append(str({"a": 1}.__or__(defaultdict(list, {"b": [2]}))))
            results.append(str({"a": 1}.__or__(Counter({"b": 2}))))
            results.append(str({"a": 0}.__ror__(defaultdict(list, {"a": [9]}))))
            results.append(str({"a": 1}.__or__({}.keys())))
            results.append(str({"a": 1}.__ror__({}.keys())))
            d = {"a": 1}
            results.append(str(d.__ior__([(1, 2)])))
            results.append(str(d))
            results.append(str(d.__ior__({}) is d))
            results.append(str(deque([1]).__add__(deque([2]))))
            results.append(str(deque([1], maxlen=2).__add__(deque([2, 3]))))
            results.append(str(deque([1]).__mul__(2)))
            results.append(str(deque([1]).__rmul__(2)))
            results.append(str(deque([1]).__mul__(0)))
            try:
                deque([1]).__add__([2])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                deque([1]).__mul__("x")
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            for (f, a, b) in [({"a": 1}.__or__, {}, {}), ({"a": 1}.__ror__, {}, {}), ({"a": 1}.__ior__, {}, {}), (deque([1]).__add__, deque([2]), deque([3])), (deque([1]).__mul__, 1, 2), (deque([1]).__rmul__, 1, 2)]:
                try:
                    call2(f, a, b)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr({}, "__or__")))
            results.append(str(hasattr({}, "__and__")))
            results.append(str(hasattr(deque([1]), "__add__")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "{'a': 1, 'b': [2]}",
            "{'a': 1, 'b': 2}",
            "{'a': 0}",
            "NotImplemented",
            "NotImplemented",
            "{'a': 1, 1: 2}",
            "{'a': 1, 1: 2}",
            "True",
            "deque([1, 2])",
            "deque([2, 3], maxlen=2)",
            "deque([1, 1])",
            "deque([1, 1])",
            "deque([])",
            "TypeError",
            "can only concatenate deque (not \"list\") to deque",
            "TypeError",
            "can't multiply sequence by non-int of type 'str'",
            "TypeError",
            "Method 'dict.__or__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__ror__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__ior__' received too many positional arguments.",
            "TypeError",
            "Method 'deque.__add__' received too many positional arguments.",
            "TypeError",
            "Method 'deque.__mul__' received too many positional arguments.",
            "TypeError",
            "Method 'deque.__rmul__' received too many positional arguments.",
            "True",
            "False",
            "True",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
