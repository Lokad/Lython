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

    [Fact]
    public async Task DictViewSetAlgebra_MatchesPythonShapes()
    {
        const string source = """
d = {"a": 1, "b": 2}
vals = []
vals.append(str(sorted(d.keys() & {"a"})))
vals.append(str(sorted(d.keys() | {"c"})))
vals.append(str(sorted(d.keys() - {"a"})))
vals.append(str(sorted(d.keys() ^ {"a", "c"})))
vals.append(str(sorted({"a"} & d.keys())))
vals.append(str(sorted({"c"} - d.keys())))
vals.append(str(sorted(d.keys() & ["a"])))
vals.append(str(sorted(d.keys() | (x for x in ["c"]))))
vals.append(str(sorted(d.keys() - {"a": 1})))
vals.append(str(sorted(d.items() & {("a", 1)})))
vals.append(str(sorted(d.items() | {("c", 3)})))
vals.append(str(sorted(d.items() - {("a", 1)})))
vals.append(str(d.keys() & d.items() == set()))
vals.append(str(d.items() | d.keys() == {("a", 1), ("b", 2), "a", "b"}))
vals.append(str({}.keys() | set() == set()))
vals.append(str(d.keys() == {"a", "b"}))
vals.append(str(d.items() == {("a", 1), ("b", 2)}))
vals.append(str(d.keys() == d.keys()))
vals.append(str(d.values() == d.values()))
vals.append(str(d.keys() == ["a", "b"]))
vals.append(str(d.keys() != {"a"}))
try:
    d.values() & {1}
except TypeError as e:
    vals.append(str(e))
try:
    d.keys() & 1
except TypeError as e:
    vals.append(str(e))
return "|".join(vals)
""";

        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        const string expected = "['a']|['a', 'b', 'c']|['b']|['b', 'c']|['a']|['c']|['a']|['a', 'b', 'c']|['b']|[('a', 1)]|[('a', 1), ('b', 2), ('c', 3)]|[('b', 2)]|True|True|True|True|True|True|False|False|True|unsupported operand type(s) for &: 'dict_values' and 'set'|'int' object is not iterable";
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DictViewIsDisjoint_MatchesPythonShapes()
    {
        const string source = """
d = {"a": 1, "b": 2}
vals = []
vals.append(str(d.keys().isdisjoint(["c"])))
vals.append(str(d.keys().isdisjoint(["a"])))
vals.append(str(d.keys().isdisjoint({"b": 9})))
vals.append(str(d.keys().isdisjoint((x for x in ["c"]))))
vals.append(str(d.items().isdisjoint([("c", 3)])))
vals.append(str(d.items().isdisjoint([("a", 1)])))
vals.append(str(d.items().isdisjoint([("a", 9)])))
vals.append(str(d.items().isdisjoint([["a", 1]])))
vals.append(str({}.keys().isdisjoint([])))
vals.append(str({}.items().isdisjoint([])))
vals.append(str(d.keys().isdisjoint(d.keys())))
vals.append(str(d.items().isdisjoint(d.items())))
return "|".join(vals)
""";

        const string expected = "True|False|False|True|True|False|True|True|True|True|False|False";

        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData("return {1}.union([[2]])\n", "hashable")]
    [InlineData("return {1}.intersection(2)\n", "iterable")]
    [InlineData("return {1}.symmetric_difference()\n", "expects")]
    [InlineData("return {1}.symmetric_difference([2], [3])\n", "expects")]
    [InlineData("return {1}.isdisjoint()\n", "expects")]
    [InlineData("return {1: 2}.keys().isdisjoint(1)\n", "iterable")]
    [InlineData("return {1: 2}.keys().isdisjoint([[]])\n", "hashable")]
    [InlineData("return {1: 2}.keys().isdisjoint()\n", "missing argument")]
    [InlineData("return {1: 2}.keys().isdisjoint([1], [2])\n", "too many positional")]
    [InlineData("return {1: 2}.keys().isdisjoint(other=[1])\n", "unexpected keyword")]
    [InlineData("return {(1, 2): 3}.items().isdisjoint(5)\n", "iterable")]
    [InlineData("return {(1, 2): 3}.items().isdisjoint([([1], 2)])\n", "hashable")]
    [InlineData("return {1: 2}.values().isdisjoint([1])\n", "has no attribute")]
    [InlineData("return {1}.union(other={2})\n", "positional")]
    [InlineData("return set([[]])\n", "unhashable type: 'list'")]
    [InlineData("def f(p):\n return set(p)\nf([{}])\n", "unhashable type: 'dict'")]
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

    private const string OperatorDundersSource =
        """
def two_args(f, a, b):
    return f(a, b)
vals = []
vals.append(str(sorted({1, 2}.__or__({2, 3}))))
vals.append(str(sorted({1, 2}.__and__({2, 3}))))
vals.append(str(sorted({1, 2}.__sub__({2, 3}))))
vals.append(str(sorted({1, 2}.__xor__({2, 3}))))
vals.append(str(sorted({1}.__ror__({1, 2}))))
vals.append(str(sorted({1}.__rand__({1, 2}))))
vals.append(str(sorted({1}.__rsub__({1, 2}))))
vals.append(str(sorted({1}.__rxor__({1, 2}))))
s = {1, 2}
r = s.__ior__({2, 3})
vals.append(str(sorted(r)))
vals.append(str(sorted(s)))
vals.append(str(r is s))
t = {1, 2}
r = t.__iand__({2, 3})
vals.append(str(sorted(r)))
vals.append(str(sorted(t)))
vals.append(str(r is t))
u = {1, 2}
r = u.__isub__({2})
vals.append(str(sorted(r)))
vals.append(str(sorted(u)))
vals.append(str(r is u))
v = {1, 2}
r = v.__ixor__({2, 3})
vals.append(str(sorted(r)))
vals.append(str(sorted(v)))
vals.append(str(r is v))
vals.append(str({1}.__or__(1)))
vals.append(str({1}.__and__([1])))
vals.append(str({1}.__sub__({}.keys())))
vals.append(str({1}.__xor__(None)))
for f in [{1}.__or__, {1}.__ror__, {1}.__and__, {1}.__rand__, {1}.__sub__, {1}.__rsub__, {1}.__xor__, {1}.__rxor__, {1}.__ior__, {1}.__iand__, {1}.__isub__, {1}.__ixor__]:
    try:
        two_args(f, 1, 2)
    except TypeError as e:
        vals.append(type(e).__name__)
        vals.append(str(e))
vals.append(str(hasattr({1}, "__xor__")))
vals.append(str(callable({1}.__and__)))
return "|".join(vals)
""";

    [Fact]
    public void SetOperatorDundersAdvanceLikeCpython()
    {
        var result = new LythonEngine().Run(OperatorDundersSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[1, 2, 3]|[2]|[1]|[1, 3]|[1, 2]|[1]|[2]|[2]|" +
            "[1, 2, 3]|[1, 2, 3]|True|" +
            "[2]|[2]|True|" +
            "[1]|[1]|True|" +
            "[1, 3]|[1, 3]|True|" +
            "NotImplemented|NotImplemented|NotImplemented|NotImplemented|" +
            "TypeError|" +
            "Method 'set.__or__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ror__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__and__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rand__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__sub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rsub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__xor__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rxor__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ior__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__iand__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__isub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ixor__' received too many positional arguments.|" +
            "True|True",
            result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_SetOperatorDundersUseTheSameSurface()
    {
        var result = await new LythonEngine().RunAsync(OperatorDundersSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[1, 2, 3]|[2]|[1]|[1, 3]|[1, 2]|[1]|[2]|[2]|" +
            "[1, 2, 3]|[1, 2, 3]|True|" +
            "[2]|[2]|True|" +
            "[1]|[1]|True|" +
            "[1, 3]|[1, 3]|True|" +
            "NotImplemented|NotImplemented|NotImplemented|NotImplemented|" +
            "TypeError|" +
            "Method 'set.__or__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ror__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__and__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rand__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__sub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rsub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__xor__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__rxor__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ior__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__iand__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__isub__' received too many positional arguments.|" +
            "TypeError|" +
            "Method 'set.__ixor__' received too many positional arguments.|" +
            "True|True",
            result.ReturnValue);
    }
    private const string DictViewOperatorDundersSource =
        """
def two_args(f, a, b):
    return f(a, b)
vals = []
d = {1: "x", 2: "y"}
k = d.keys()
vals.append(str(sorted(k.__or__({2, 3}))))
vals.append(str(sorted(k.__and__({2, 3}))))
vals.append(str(sorted(k.__sub__({2}))))
vals.append(str(sorted(k.__xor__({2, 3}))))
vals.append(str(sorted(k.__ror__([2, 3]))))
vals.append(str(sorted(k.__rand__([1, 3]))))
vals.append(str(sorted(k.__rsub__([0, 1, 3]))))
vals.append(str(sorted(k.__rxor__([2, 3]))))
vals.append(str(type(k.__or__({3})).__name__))
vals.append(str(type(k.__and__({3})).__name__))
vals.append(str(type(k.__sub__({3})).__name__))
vals.append(str(type(k.__xor__({3})).__name__))
vals.append(str(k.__or__({"a"}) == {1, 2, "a"}))
vals.append(str(k.__or__(d.items()) == {1, 2, (1, "x"), (2, "y")}))
vals.append(str(d.items().__sub__(k) == {(1, "x"), (2, "y")}))
it = {1: 10, 2: 20}.items()
vals.append(str(sorted(it.__and__({(1, 10), (3, 30)}))))
vals.append(str(sorted(it.__sub__({(1, 10), (3, 30)}))))
vals.append(str(sorted(it.__or__({(3, 30)}))))
vals.append(str(sorted(it.__xor__({(1, 10), (3, 30)}))))
vals.append(str(sorted(it.__rsub__([(1, 10), (3, 30)]))))
vals.append(str(type(it.__or__({(3, 30)})).__name__))
vals.append(str(sorted([1, 2] | k)))
vals.append(str(sorted([1, 3] & k)))
vals.append(str(sorted([0, 1, 3] - k)))
vals.append(str(sorted([2, 3] ^ k)))
try:
    k.__or__(5)
except TypeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
try:
    k.__and__(5)
except TypeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
try:
    it.__or__(5)
except TypeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
try:
    k.__or__([[1]])
except TypeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
try:
    {"a": [1]}.items() | {("a", 1)}
except TypeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
try:
    {1: "x"}.values().__or__({1})
except AttributeError as e:
    vals.append(type(e).__name__)
    vals.append(str(e))
for f in [k.__or__, k.__ror__, k.__and__, k.__rand__, k.__sub__, k.__rsub__, k.__xor__, k.__rxor__, it.__or__, it.__ror__, it.__and__, it.__rand__, it.__sub__, it.__rsub__, it.__xor__, it.__rxor__]:
    try:
        two_args(f, 1, 2)
    except TypeError as e:
        vals.append(type(e).__name__)
        vals.append(str(e))
vals.append(str(hasattr(k, "__or__")))
vals.append(str(hasattr(it, "__xor__")))
vals.append(str(hasattr({1: "x"}.values(), "__or__")))
vals.append(str(hasattr(k, "__ior__")))
vals.append(str(callable(k.__and__)))
return "|".join(vals)
""";

    [Fact]
    public void DictViewOperatorDundersAdvanceLikeCpython()
    {
        var result = new LythonEngine().Run(DictViewOperatorDundersSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[1, 2, 3]|[2]|[1]|[1, 3]|[1, 2, 3]|[1]|" +
            "[0, 3]|[1, 3]|set|set|set|set|" +
            "True|True|True|[(1, 10)]|[(2, 20)]|[(1, 10), (2, 20), (3, 30)]|" +
            "[(2, 20), (3, 30)]|[(3, 30)]|set|[1, 2]|[1]|[0, 3]|" +
            "[1, 3]|TypeError|'int' object is not iterable|TypeError|'int' object is not iterable|TypeError|" +
            "'int' object is not iterable|TypeError|unhashable type: 'list'|TypeError|unhashable type: 'list'|AttributeError|" +
            "'dict_values' object has no attribute '__or__'|TypeError|Method 'dict_keys.__or__' received too many positional arguments.|TypeError|Method 'dict_keys.__ror__' received too many positional arguments.|TypeError|" +
            "Method 'dict_keys.__and__' received too many positional arguments.|TypeError|Method 'dict_keys.__rand__' received too many positional arguments.|TypeError|Method 'dict_keys.__sub__' received too many positional arguments.|TypeError|" +
            "Method 'dict_keys.__rsub__' received too many positional arguments.|TypeError|Method 'dict_keys.__xor__' received too many positional arguments.|TypeError|Method 'dict_keys.__rxor__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__or__' received too many positional arguments.|TypeError|Method 'dict_items.__ror__' received too many positional arguments.|TypeError|Method 'dict_items.__and__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__rand__' received too many positional arguments.|TypeError|Method 'dict_items.__sub__' received too many positional arguments.|TypeError|Method 'dict_items.__rsub__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__xor__' received too many positional arguments.|TypeError|Method 'dict_items.__rxor__' received too many positional arguments.|True|True|False|" +
            "False|True",
            result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_DictViewOperatorDundersUseTheSameSurface()
    {
        var result = await new LythonEngine().RunAsync(DictViewOperatorDundersSource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "[1, 2, 3]|[2]|[1]|[1, 3]|[1, 2, 3]|[1]|" +
            "[0, 3]|[1, 3]|set|set|set|set|" +
            "True|True|True|[(1, 10)]|[(2, 20)]|[(1, 10), (2, 20), (3, 30)]|" +
            "[(2, 20), (3, 30)]|[(3, 30)]|set|[1, 2]|[1]|[0, 3]|" +
            "[1, 3]|TypeError|'int' object is not iterable|TypeError|'int' object is not iterable|TypeError|" +
            "'int' object is not iterable|TypeError|unhashable type: 'list'|TypeError|unhashable type: 'list'|AttributeError|" +
            "'dict_values' object has no attribute '__or__'|TypeError|Method 'dict_keys.__or__' received too many positional arguments.|TypeError|Method 'dict_keys.__ror__' received too many positional arguments.|TypeError|" +
            "Method 'dict_keys.__and__' received too many positional arguments.|TypeError|Method 'dict_keys.__rand__' received too many positional arguments.|TypeError|Method 'dict_keys.__sub__' received too many positional arguments.|TypeError|" +
            "Method 'dict_keys.__rsub__' received too many positional arguments.|TypeError|Method 'dict_keys.__xor__' received too many positional arguments.|TypeError|Method 'dict_keys.__rxor__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__or__' received too many positional arguments.|TypeError|Method 'dict_items.__ror__' received too many positional arguments.|TypeError|Method 'dict_items.__and__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__rand__' received too many positional arguments.|TypeError|Method 'dict_items.__sub__' received too many positional arguments.|TypeError|Method 'dict_items.__rsub__' received too many positional arguments.|TypeError|" +
            "Method 'dict_items.__xor__' received too many positional arguments.|TypeError|Method 'dict_items.__rxor__' received too many positional arguments.|True|True|False|" +
            "False|True",
            result.ReturnValue);
    }
    [Fact]
    public async Task DictSetComparisonDundersAdvanceLikeCpython()
    {
        // dict __eq__/__ne__ take mappings only while ordering always declines, set dunders take sets only with subset semantics, and view __eq__/__ne__ take sets or views only, all through the shared cores exactly like CPython.
        var script = new LythonEngine().Compile("""
from collections import defaultdict, Counter
def call2(f, a, b):
    return f(a, b)
results = []
results.append(str({1: 2}.__eq__({1: 2})))
results.append(str({1: 2}.__ne__({1: 3})))
results.append(str({}.__lt__({})))
results.append(str({}.__le__({})))
results.append(str({}.__eq__([])))
results.append(str({}.__ne__([])))
results.append(str({}.__eq__(defaultdict(list))))
results.append(str({"a": 1}.__eq__(Counter({"a": 1}))))
results.append(str({}.__lt__(defaultdict(list))))
results.append(str({}.__ge__(Counter())))
results.append(str({1: 2}.__eq__((1, 2))))
results.append(str({1, 2}.__eq__({1, 2})))
results.append(str({1, 2}.__ne__({1})))
results.append(str({1}.__lt__({1, 2})))
results.append(str({1, 2}.__le__({1, 2})))
results.append(str({1, 2}.__gt__({1})))
results.append(str({1, 2}.__ge__({1, 2})))
results.append(str(set().__lt__(set())))
results.append(str(set().__le__(set())))
results.append(str({1, 2}.__eq__([1, 2])))
results.append(str({1}.__lt__([1, 2])))
d = {1: 2, 3: 4}
results.append(str(d.keys().__eq__({1, 3})))
results.append(str(d.keys().__eq__([1, 3])))
results.append(str(d.keys().__ne__({1})))
results.append(str(d.items().__eq__({(1, 2), (3, 4)})))
results.append(str(d.items().__eq__([(1, 2)])))
results.append(str(d.items().__ne__({(1, 2)})))
results.append(str(d.items().__eq__([(1, 2), (3, 4)])))
results.append(str(d.items().__ne__([(1, 2)])))
results.append(str(d.keys().__ne__([1, 3])))
try:
    {"a": [1]}.items().__eq__({("a", 1)})
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
for (f, a, b) in [({}.__eq__, {}, {}), ({}.__ne__, {}, {}), ({}.__lt__, {}, {}), ({}.__le__, {}, {}), ({}.__gt__, {}, {}), ({}.__ge__, {}, {}), ({1}.__eq__, {1}, {1}), ({1}.__ne__, {1}, {1}), ({1}.__lt__, {1}, {1}), ({1}.__le__, {1}, {1}), ({1}.__gt__, {1}, {1}), ({1}.__ge__, {1}, {1}), (d.keys().__eq__, 1, 2), (d.keys().__ne__, 1, 2), (d.items().__eq__, 1, 2), (d.items().__ne__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr({}, "__eq__")))
results.append(str(hasattr({1}, "__lt__")))
results.append(str(hasattr(d.keys(), "__eq__")))
results.append(str(hasattr(d.items(), "__ne__")))
results.append(str(hasattr({}, "__rlt__")))
results.append(str(hasattr({1}, "__req__")))
results.append(str(callable({1}.__eq__)))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "False",
            "True",
            "NotImplemented",
            "NotImplemented",
            "True",
            "NotImplemented",
            "True",
            "True",
            "NotImplemented",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "TypeError",
            "unhashable type: 'list'",
            "TypeError",
            "Method 'dict.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__ge__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'set.__ge__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_keys.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_keys.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__ne__' received too many positional arguments.",
            "True",
            "True",
            "True",
            "True",
            "False",
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
    [Fact]
    public async Task DictViewSubsetDundersAdvanceLikeCpython()
    {
        // dict key/item views take sets or views only for subset ordering through the shared set core, declining everything else with NotImplemented, exactly like CPython.
        var script = new LythonEngine().Compile("""
d = {1: 2, 3: 4}
k = d.keys()
def call2(f, a, b):
    return f(a, b)
results = []
results.append(str(k.__lt__({1, 3, 5})))
results.append(str(k.__le__({1, 3})))
results.append(str(k.__gt__({1})))
results.append(str(k.__ge__({1, 3})))
results.append(str(k.__lt__({1})))
results.append(str(k.__ge__({1, 5})))
results.append(str(k.__lt__([1, 3, 5])))
results.append(str(k.__lt__({1: 9})))
results.append(str(k.__lt__(k)))
results.append(str(d.items().__lt__({(1, 2)})))
results.append(str(d.items().__le__({(1, 2), (3, 4)})))
results.append(str(d.items().__gt__({(1, 2)})))
for (f, a, b) in [(k.__lt__, 1, 2), (k.__le__, 1, 2), (k.__gt__, 1, 2), (k.__ge__, 1, 2), (d.items().__lt__, 1, 2), (d.items().__le__, 1, 2), (d.items().__gt__, 1, 2), (d.items().__ge__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(k, "__lt__")))
results.append(str(hasattr(d.items(), "__ge__")))
results.append(str(hasattr(k, "__rlt__")))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "True",
            "True",
            "False",
            "False",
            "NotImplemented",
            "NotImplemented",
            "False",
            "False",
            "True",
            "True",
            "TypeError",
            "Method 'dict_keys.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_keys.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_keys.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_keys.__ge__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'dict_items.__ge__' received too many positional arguments.",
            "True",
            "True",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    [Fact]
    public async Task ChainMapViewSetComparisonDundersAdvanceLikeCpython()
    {
        // ChainMap key/item views convert any iterable for set operations
        // (declining non-iterables, unlike C dict views) and take sets or
        // views for equality and subset ordering, all through the shared
        // set core exactly like CPython.
        var script = new LythonEngine().Compile("""
from collections import ChainMap
def call2(f, a, b):
    return f(a, b)
results = []
cm = ChainMap({"a": 1, "b": 2}, {"c": 3})
k = cm.keys()
results.append(str(sorted(k.__or__({"x"}))))
results.append(str(sorted(k.__and__({"a", "z"}))))
results.append(str(sorted(k.__sub__({"a"}))))
results.append(str(sorted(k.__xor__({"a", "x"}))))
results.append(str(k.__ror__([1, 2]) == {1, 2, "a", "b", "c"}))
results.append(str(k.__rand__([1, "a"]) == {"a"}))
results.append(str(k.__rsub__([1, "a", "x"]) == {1, "x"}))
results.append(str(k.__rxor__([1, "a", "x"]) == {1, "x", "b", "c"}))
results.append(str(sorted(k.__or__({"x": 9}.keys()))))
results.append(str(type(k.__or__({1})).__name__))
results.append(str(type(k.__and__({1})).__name__))
results.append(str(type(k.__sub__({1})).__name__))
results.append(str(type(k.__xor__({1})).__name__))
results.append(str(k.__eq__({"a", "b", "c"})))
results.append(str(k.__ne__({"a"})))
results.append(str(k.__eq__({"a": 1, "b": 2, "c": 3}.keys())))
results.append(str(k.__eq__([1, 2])))
results.append(str(k.__lt__({"a", "b", "c", "x"})))
results.append(str(k.__le__({"a": 1, "b": 2, "c": 3}.keys())))
results.append(str(k.__gt__({"a"})))
results.append(str(k.__ge__({"a": 1, "b": 2, "c": 3}.keys())))
results.append(str(k.__lt__(k)))
results.append(str(k.__or__(5)))
results.append(str(k.__ror__(5)))
results.append(str(k.__and__(5)))
results.append(str(k.__rand__(5)))
results.append(str(k.__sub__(5)))
results.append(str(k.__rsub__(5)))
results.append(str(k.__xor__(5)))
results.append(str(k.__rxor__(5)))
try:
    k.__or__([[1]])
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
class It:
    def __iter__(self):
        return iter([1, 2])
results.append(str(k.__or__(It()) == {1, 2, "a", "b", "c"}))
results.append(str(k.__or__(iter([1, 2])) == {1, 2, "a", "b", "c"}))
results.append(str(([1, 2] | k) == {1, 2, "a", "b", "c"}))
results.append(str(sorted([1, "a"] & k)))
it = cm.items()
results.append(str(sorted(it.__and__({("a", 1)}))))
results.append(str(sorted(it.__sub__({("a", 1)}))))
results.append(str(sorted(it.__or__({("z", 9)}))))
results.append(str(sorted(it.__xor__({("a", 1), ("z", 9)}))))
results.append(str(it.__eq__({("a", 1), ("b", 2), ("c", 3)})))
results.append(str(it.__eq__([[("a", 1)]])))
results.append(str(it.__ne__({("a", 1)})))
results.append(str(it.__lt__({("a", 1), ("b", 2), ("c", 3), ("z", 9)})))
results.append(str(it.__le__({("a", 1), ("b", 2), ("c", 3)})))
results.append(str(it.__gt__({("a", 1)})))
results.append(str(it.__or__(5)))
results.append(str(it.__rxor__(5)))
class Pairs:
    def __iter__(self):
        return iter([("a", 1), ("z", 9)])
results.append(str(it.__and__(Pairs()) == {("a", 1)}))
results.append(str(it.__or__(Pairs()) == {("a", 1), ("b", 2), ("c", 3), ("z", 9)}))
try:
    ChainMap({"a": [1]}).items().__eq__({("a", 1)})
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
for (f, a, b) in [(k.__or__, 1, 2), (k.__ror__, 1, 2), (k.__and__, 1, 2), (k.__rand__, 1, 2), (k.__sub__, 1, 2), (k.__rsub__, 1, 2), (k.__xor__, 1, 2), (k.__rxor__, 1, 2), (k.__eq__, 1, 2), (k.__ne__, 1, 2), (k.__lt__, 1, 2), (k.__le__, 1, 2), (k.__gt__, 1, 2), (k.__ge__, 1, 2), (it.__or__, 1, 2), (it.__ror__, 1, 2), (it.__and__, 1, 2), (it.__rand__, 1, 2), (it.__sub__, 1, 2), (it.__rsub__, 1, 2), (it.__xor__, 1, 2), (it.__rxor__, 1, 2), (it.__eq__, 1, 2), (it.__ne__, 1, 2), (it.__lt__, 1, 2), (it.__le__, 1, 2), (it.__gt__, 1, 2), (it.__ge__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(k, "__or__")))
results.append(str(hasattr(it, "__eq__")))
results.append(str(hasattr(k, "__lt__")))
results.append(str(hasattr(cm.values(), "__or__")))
results.append(str(hasattr(k, "__rlt__")))
results.append(str(callable(k.__and__)))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "['a', 'b', 'c', 'x']",
            "['a']",
            "['b', 'c']",
            "['b', 'c', 'x']",
            "True",
            "True",
            "True",
            "True",
            "['a', 'b', 'c', 'x']",
            "set",
            "set",
            "set",
            "set",
            "True",
            "True",
            "True",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
            "False",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "TypeError",
            "unhashable type: 'list'",
            "True",
            "True",
            "True",
            "['a']",
            "[('a', 1)]",
            "[('b', 2), ('c', 3)]",
            "[('a', 1), ('b', 2), ('c', 3), ('z', 9)]",
            "[('b', 2), ('c', 3), ('z', 9)]",
            "True",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "TypeError",
            "unhashable type: 'list'",
            "TypeError",
            "Method 'ChainMap.keys.__or__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__ror__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__and__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__rand__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__sub__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__rsub__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__xor__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__rxor__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.keys.__ge__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__or__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__ror__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__and__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__rand__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__sub__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__rsub__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__xor__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__rxor__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.items.__ge__' received too many positional arguments.",
            "True",
            "True",
            "True",
            "False",
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
