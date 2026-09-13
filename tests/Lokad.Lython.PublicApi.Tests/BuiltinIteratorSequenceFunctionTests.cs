using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinIteratorSequenceFunctionTests
{
    [Fact]
    public void NextRequiresIteratorAndGeneratorExpressionsAreOneShot()
    {
        var result = new LythonEngine().Run(
            """
g = (x for x in [1, 2])
values = [str(next(g)), str(next(g)), str(next(g, "done"))]
try:
    next([1])
except TypeError:
    values.append("caught")
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1|2|done|caught", result.ReturnValue);
    }

    [Fact]
    public void NextOnNonIterators_NamesOperandTypes()
    {
        var result = new LythonEngine().Run(
            """
vals = []
try:
    next(1)
except TypeError as err:
    vals.append(err.message)
try:
    next("ab")
except TypeError as err:
    vals.append(err.message)
try:
    next([1])
except TypeError as err:
    vals.append(err.message)
return "|".join(vals)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("'int' object is not an iterator|'str' object is not an iterator|'list' object is not an iterator", result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_GeneratorExpressionsAdvanceOneItemAtATime()
    {
        var result = await new LythonEngine().RunAsync(
            """
observed = []
def observe(value):
    observed.append(value)
    return value

values = (observe(value) for value in [1, 2, 3])
first = next(values)
return str(first) + "|" + str(observed)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1|[1]", result.ReturnValue);
    }

    [Fact]
    public void RangeEnumerateAndZipUseLazyIteratorSemantics()
    {
        var result = new LythonEngine().Run(
            """
r = range(3)
e = enumerate(["a", "b"])
z = zip([1, 2], [3, 4])
values = [repr(r), repr(r[1:]), str(iter(e) is e), str(next(e)), str(next(e)), str(iter(z) is z), str(next(z)), str(next(z))]
try:
    list(zip([1], [2, 3], strict=True))
except ValueError:
    values.append("strict")
values.append(str(next(iter(range(100000000000000000000)), -1)))
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("range(0, 3)|range(1, 3)|True|(0, 'a')|(1, 'b')|True|(1, 3)|(2, 4)|strict|0", result.ReturnValue);
    }

    [Fact]
    public void ZipStrictMismatch_NamesArgumentsLikeCpython()
    {
        var result = new LythonEngine().Run(
            """
def strict_error(*seqs):
    try:
        list(zip(*seqs, strict=True))
    except ValueError as e:
        return str(e)
    return "no error"
vals = []
vals.append(strict_error([1, 2], [3]))
vals.append(strict_error([1], [2, 3]))
vals.append(strict_error([1], [2], [3, 4]))
vals.append(strict_error([1, 2], [3, 4], [5]))
vals.append(strict_error([1, 2], [3], [4, 5]))
vals.append(strict_error([1], [2, 3], [4]))
vals.append(str(list(zip([1], [2], strict=True))))
return "|".join(vals)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("zip() argument 2 is shorter than argument 1|zip() argument 2 is longer than argument 1|zip() argument 3 is longer than arguments 1-2|zip() argument 3 is shorter than arguments 1-2|zip() argument 2 is shorter than argument 1|zip() argument 2 is longer than argument 1|[(1, 2)]", result.ReturnValue);
    }

    [Fact]
    public void EnumerateStart_CoercesIndexLikeCpython()
    {
        var result = new LythonEngine().Run(
            """
class J:
    def __index__(self):
        return 5

parts = []
parts.append(str(list(enumerate("ab", J()))))
parts.append(str(list(enumerate("ab", True))))
parts.append(str(list(enumerate("ab", 2))))
try:
    list(enumerate("ab", "a"))
    parts.append("no-error")
except TypeError as e:
    parts.append(str(e))
return "|".join(parts)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[(5, 'a'), (6, 'b')]|[(1, 'a'), (2, 'b')]|[(2, 'a'), (3, 'b')]|'str' object cannot be interpreted as an integer", result.ReturnValue);
    }


    [Fact]
    public void SetOperatorsMatchPythonAndInPlaceDifferencePreservesIdentity()
    {
        var result = new LythonEngine().Run(
            """
a = {1, 2}
b = a
a -= {2}
return str({1} < {1, 2}) + "|" + str({1} <= {1}) + "|" + str({1, 2} > {2}) + "|" + str(a is b) + "|" + str(a)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|{1}", result.ReturnValue);

        var invalidAddition = new LythonEngine().Run("return {1} + {2}\n", new MockLythonHost());
        Assert.False(invalidAddition.Success);
    }

    [Fact]
    public async Task ReversedDictKeys_MatchPython()
    {
        const string script = """
            values = []
            values.append(str(list(reversed({"a": 1, "b": 2}))))
            values.append(str(list(reversed({}))))
            from collections import defaultdict, Counter
            values.append(str(list(reversed(defaultdict(int, {"x": 1})))))
            values.append(str(list(reversed(Counter("ab")))))
            d = {"a": 1}
            r = reversed(d)
            d["b"] = 2
            try:
                list(r)
            except RuntimeError as ex:
                values.append(str(ex))
            it1 = reversed({"m": 1, "n": 2})
            it2 = reversed({"m": 1, "n": 2})
            values.append(str(next(it1)))
            values.append(str(list(it2)))
            values.append(str(list(it1)))
            __lython_file = open("/out.txt", "w")
            __lython_file.write("|".join(values))
            __lython_file.close()
            """;
        const string expected = "['b', 'a']|[]|['x']|['b', 'a']|dictionary changed size during iteration|n|['n', 'm']|['m']";

        var host = new MockLythonHost();
        var result = new LythonEngine().Run(script, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, host.ReadText("/out.txt"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncHost.ReadText("/out.txt"));
    }
    [Fact]
    public void IteratorAndSequenceBuiltins_MatchPythonShapedCoreBehavior()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []

it = iter([1, 2])
values.append(str(next(it)))
values.append(str(next(it)))
values.append(str(next(it, 99)))
values.append(str(iter(it) is it))

source = [1, 2, 0, 3]
def take():
    return source.pop(0)

values.append(str(list(iter(take, 0))))

def double(x):
    return x * 2

def add(left, right):
    return left + right

def odd(x):
    return x % 2

values.append(str(list(map(double, [1, 2, 3]))))
values.append(str(list(map(add, [1, 2, 3], [10, 20]))))
values.append(str(list(filter(odd, [1, 2, 3, 4]))))
values.append(str(list(filter(None, [0, 1, False, True]))))

values.append(str(list(reversed([1, 2, 3]))))
values.append("".join(reversed("ab😀")))
values.append(str(list(reversed(bytes([1, 2, 3])))))

class Custom:
    def __reversed__(self):
        return iter([7, 8])

values.append(str(list(reversed(Custom()))))

s = slice(1, 5, 2)
values.append(str(s.start) + ":" + str(s.stop) + ":" + str(s.step))
values.append(str([0, 1, 2, 3, 4, 5][s]))
values.append("abcdef"[slice(2)])
values.append(str(slice(None)))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "1|2|99|True|[1, 2]|[2, 4, 6]|[11, 22]|[1, 3]|[1, True]|[3, 2, 1]|😀ba|[3, 2, 1]|[7, 8]|1:5:2|[1, 3]|ab|slice(None, None, None)",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task BareNextSurfacesExactStopIterationShapes()
    {
        // Bare next() over exhausted engine iterators raises an empty
        // StopIteration like CPython (never the house text), user-defined
        // __next__ failures propagate untouched, and StopIteration.value
        // reads args[0] or None with independent write storage, in both modes.
        var script = new LythonEngine().Compile("""
            import shlex
            results = []
            def shapes(thunk):
                try:
                    thunk()
                except StopIteration as e:
                    results.append(str(e))
                    results.append(repr(e.args))
                    results.append(repr(e))
            shapes(lambda: next(iter([])))
            def dict_exhaust():
                it = iter({'a': 1})
                next(it)
                return next(it)
            shapes(dict_exhaust)
            def str_exhaust():
                it = iter('ab')
                next(it)
                next(it)
                return next(it)
            shapes(str_exhaust)
            def range_exhaust():
                it = iter(range(1))
                next(it)
                return next(it)
            shapes(range_exhaust)
            def map_exhaust():
                it = map(str, [1])
                next(it)
                return next(it)
            shapes(map_exhaust)
            class MyIter:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    if self.n > 0:
                        raise StopIteration('custom')
                    self.n += 1
                    return 1
            def user_custom():
                it = MyIter()
                next(it)
                return next(it)
            shapes(user_custom)
            def user_default():
                it = MyIter()
                next(it)
                return next(it, 'dflt')
            results.append(str(user_default()))
            def shlex_exhaust():
                lx = shlex.shlex('a b')
                lx.read_token()
                lx.read_token()
                return next(lx)
            shapes(shlex_exhaust)
            def bare_value():
                try:
                    next(iter([]))
                except StopIteration as e:
                    return e.value
            results.append(str(bare_value()))
            def custom_value():
                try:
                    raise StopIteration(5)
                except StopIteration as e:
                    return e.value
            results.append(str(custom_value()))
            def write_value():
                try:
                    raise StopIteration(5)
                except StopIteration as e:
                    e.value = 7
                    return (e.value, e.args)
            results.append(str(write_value()))
            def write_bare():
                try:
                    next(iter([]))
                except StopIteration as e:
                    e.value = 9
                    return (e.value, e.args)
            results.append(str(write_bare()))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "",
            "()",
            "StopIteration()",
            "",
            "()",
            "StopIteration()",
            "",
            "()",
            "StopIteration()",
            "",
            "()",
            "StopIteration()",
            "",
            "()",
            "StopIteration()",
            "custom",
            "('custom',)",
            "StopIteration('custom')",
            "dflt",
            "",
            "()",
            "StopIteration()",
            "None",
            "5",
            "(7, (5,))",
            "(9, ())",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_MaterializersAndLazyBuiltinIterators_AwaitAsyncSourcesAndCallbacks()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/root.txt", "r");
        host.SeedFile("/repo/docs/a/alpha.txt", "a");
        host.SeedFile("/repo/docs/b/beta.txt", "b");
        host.SeedFile("/repo/marker.txt", "m");

        var result = await new LythonEngine().RunAsync(
            """
import os

def root_with_read(row):
    return row[0] + ":" + open("/repo/marker.txt").read()

def is_a(row):
    open("/repo/marker.txt").read()
    return row[0] == "/repo/docs/a"

def under_repo(row):
    return row[0].startswith("/repo")

vals = []
vals.append(",".join([row[0] for row in os.walk("/repo/docs")]))
vals.append(",".join(list(map(root_with_read, os.walk("/repo/docs")))))
vals.append(",".join([row[0] for row in filter(is_a, os.walk("/repo/docs"))]))
first = next(os.walk("/repo/docs"))
vals.append(first[0] + ":" + str(first[1]) + ":" + str(first[2]))
vals.append(str(len(list(os.walk("/repo/docs")))))
vals.append(str(len(tuple(os.walk("/repo/docs")))))
vals.append(str(any(map(is_a, os.walk("/repo/docs")))))
vals.append(str(all(map(under_repo, os.walk("/repo/docs")))))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(
            "/repo/docs,/repo/docs/a,/repo/docs/b|/repo/docs:m,/repo/docs/a:m,/repo/docs/b:m|/repo/docs/a|/repo/docs:['a', 'b']:['root.txt']|3|3|True|True",
            host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("iter(1)\n", "TypeError", "'int' object is not iterable")]
    [InlineData("iter(None)\n", "TypeError", "'NoneType' object is not iterable")]
    [InlineData("def f(a):\n return list(a)\nf(1)\n", "TypeError", "'int' object is not iterable")]
    [InlineData("def f(a):\n return sorted(a)\nf(None)\n", "TypeError", "'NoneType' object is not iterable")]
    [InlineData("iter(1, 0)\n", "TypeError", "callable")]
    [InlineData("reversed(1)\n", "TypeError", "reversible")]
    [InlineData("map(1, [1])\n", "TypeError", "callable")]
    [InlineData("map(lambda x: x)\n", "TypeError", "at least one iterable")]
    [InlineData("filter(1, [1])\n", "TypeError", "is not callable")]
    [InlineData("slice()\n", "TypeError", "slice expected at least 1 argument, got 0")]
    [InlineData("slice(1, 2, 3, 4)\n", "TypeError", "slice expected at most 3 arguments, got 4")]
    [InlineData("[1, 2][slice(0, 2, 0)]\n", "ValueError", "slice step cannot be zero")]
    public void IteratorAndSequenceBuiltins_ReportExplicitFailures(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void RangeAcceptsBooleansAsIntegers()
    {
        var result = new LythonEngine().Run(
            """
results = [str(list(range(True))), str(list(range(False))), str(list(range(0, 5, True))), str(list(range(True, 3)))]
try:
    list(range(2.0))
except TypeError:
    results.append("float-rejected")
return "|".join(results)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0]|[]|[0, 1, 2, 3, 4]|[1, 2]|float-rejected", result.ReturnValue);
    }

    [Fact]
    public async Task IteratorDundersAdvanceLikeCpython()
    {
        // Engine iterators expose __iter__ (identity) and __next__ (advance
        // or empty StopIteration) like CPython, uniformly across iterator
        // kinds, in both modes.
        var script = new LythonEngine().Compile("""
            results = []
            it = iter([1, 2])
            results.append(str(it.__iter__() is it))
            results.append(str(it.__next__()))
            results.append(str(it.__next__()))
            try:
                it.__next__()
            except StopIteration as e:
                results.append(type(e).__name__)
                results.append(str(e.args))
            results.append(str(iter((3,)).__next__()))
            def dictk():
                return iter({"a": 1}.keys()).__next__()
            results.append(str(dictk()))
            def dictv():
                return iter({"a": 1}.values()).__next__()
            results.append(str(dictv()))
            def dicti():
                return iter({"a": 1}.items()).__next__()
            results.append(str(dicti()))
            results.append(str(iter('ab').__next__()))
            results.append(str(iter(range(2)).__next__()))
            results.append(str(iter({1}).__next__()))
            results.append(str(enumerate('ab').__next__()))
            results.append(str(zip([1], [2]).__next__()))
            results.append(str(map(str, [1]).__next__()))
            results.append(str(filter(None, [1]).__next__()))
            results.append(str(reversed([1, 2]).__next__()))
            results.append(str((x for x in [1]).__next__()))
            f = iter([1, 2]).__next__
            results.append(str((f(), f())))
            def next_arg():
                return iter([1]).__next__(1)
            try:
                next_arg()
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            def iter_arg():
                return iter([1]).__iter__(1)
            try:
                iter_arg()
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            results.append(str(hasattr(iter([1]), '__next__')))
            results.append(str(hasattr(iter([1]), '__iter__')))
            results.append(str(callable(iter([1]).__next__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "1",
            "2",
            "StopIteration",
            "()",
            "3",
            "a",
            "1",
            "('a', 1)",
            "a",
            "0",
            "1",
            "(0, 'a')",
            "(1, 2)",
            "1",
            "1",
            "2",
            "1",
            "(1, 2)",
            "TypeError",
            "iterator.__next__() expects no arguments.",
            "TypeError",
            "iterator.__iter__() expects no arguments.",
            "True",
            "True",
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
    public async Task ContainerDundersAdvanceLikeCpython()
    {
        // Builtin containers expose __iter__ returning a fresh iterator
        // with the same values as iter(), like CPython.
        var script = new LythonEngine().Compile("""
            def iter_arg(x):
                return x.__iter__(1)
            results = []
            results.append(str(list([1, 2].__iter__())))
            results.append(str(list((7,).__iter__())))
            results.append(str(list({"a": 1, "b": 2}.__iter__())))
            results.append(str(list({9}.__iter__())))
            results.append(str(list("ab".__iter__())))
            results.append(str(list(b"ab".__iter__())))
            results.append(str(list(range(3).__iter__())))
            results.append(str([1, 2].__iter__() is [1, 2]))
            a = [1, 2]
            results.append(str(a.__iter__() is a.__iter__()))
            it = [1].__iter__()
            results.append(str(it.__next__()))
            try:
                it.__next__()
            except StopIteration as e:
                results.append(type(e).__name__)
                results.append(str(e.args))
            for v in [[1], (1,), {"a": 1}, {1}, "ab", b"ab", range(2)]:
                try:
                    iter_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__iter__")))
            results.append(str(callable([1].__iter__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "[1, 2]",
            "[7]",
            "['a', 'b']",
            "[9]",
            "['a', 'b']",
            "[97, 98]",
            "[0, 1, 2]",
            "False",
            "False",
            "1",
            "StopIteration",
            "()",
            "TypeError",
            "list.__iter__() expects no arguments.",
            "TypeError",
            "tuple.__iter__() expects no arguments.",
            "TypeError",
            "dict.__iter__() expects no arguments.",
            "TypeError",
            "set.__iter__() expects no arguments.",
            "TypeError",
            "str.__iter__() expects no arguments.",
            "TypeError",
            "bytes.__iter__() expects no arguments.",
            "TypeError",
            "range.__iter__() expects no arguments.",
            "True",
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
    public async Task CollectionViewDundersAdvanceLikeCpython()
    {
        // Dict views and collection types expose __iter__ with the same
        // values as iter(), like CPython.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque
            def iter_arg(x):
                return x.__iter__(1)
            results = []
            d = {"a": 1, "b": 2}
            results.append(str(list(d.keys().__iter__())))
            results.append(str(list(d.values().__iter__())))
            results.append(str(list(d.items().__iter__())))
            results.append(str(list(defaultdict(list, {"a": [1]}).__iter__())))
            results.append(str(list(Counter("aab").__iter__())))
            results.append(str(list(deque([1, 2]).__iter__())))
            q = deque([1])
            results.append(str(q.__iter__() is q))
            it = q.__iter__()
            results.append(str(it.__next__()))
            try:
                it.__next__()
            except StopIteration as e:
                results.append(type(e).__name__)
                results.append(str(e.args))
            for v in [defaultdict(list), Counter(), deque(), {"a": 1}.keys(), {"a": 1}.values(), {"a": 1}.items()]:
                try:
                    iter_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr({}.keys(), "__iter__")))
            results.append(str(callable({}.values().__iter__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "['a', 'b']",
            "[1, 2]",
            "[('a', 1), ('b', 2)]",
            "['a']",
            "['a', 'b']",
            "[1, 2]",
            "False",
            "1",
            "StopIteration",
            "()",
            "TypeError",
            "defaultdict.__iter__() expects no arguments.",
            "TypeError",
            "Counter.__iter__() expects no arguments.",
            "TypeError",
            "deque.__iter__() expects no arguments.",
            "TypeError",
            "dict_keys.__iter__() expects no arguments.",
            "TypeError",
            "dict_values.__iter__() expects no arguments.",
            "TypeError",
            "dict_items.__iter__() expects no arguments.",
            "True",
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
    public async Task ChainMapViewDundersAdvanceLikeCpython()
    {
        // ChainMap views expose __iter__ with the same values as iter(),
        // like CPython (single-map rows; multi-map order is separate).
        var script = new LythonEngine().Compile("""
            from collections import ChainMap
            def iter_arg(x):
                return x.__iter__(1)
            results = []
            cm = ChainMap({"a": 1})
            results.append(str(list(cm.keys().__iter__())))
            results.append(str(list(cm.values().__iter__())))
            results.append(str(list(cm.items().__iter__())))
            results.append(str(cm.keys().__iter__() is cm.keys()))
            it = cm.items().__iter__()
            results.append(str(it.__next__()))
            try:
                it.__next__()
            except StopIteration as e:
                results.append(type(e).__name__)
                results.append(str(e.args))
            for v in [cm.keys(), cm.values(), cm.items()]:
                try:
                    iter_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr(ChainMap({}).keys(), "__iter__")))
            results.append(str(callable(ChainMap({}).values().__iter__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "['a']",
            "[1]",
            "[('a', 1)]",
            "False",
            "('a', 1)",
            "StopIteration",
            "()",
            "TypeError",
            "ChainMap.keys.__iter__() expects no arguments.",
            "TypeError",
            "ChainMap.values.__iter__() expects no arguments.",
            "TypeError",
            "ChainMap.items.__iter__() expects no arguments.",
            "True",
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
    public async Task ContainerLenDundersAdvanceLikeCpython()
    {
        // Sized builtins expose __len__ with exactly the len() value,
        // like CPython.
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            def len_arg(x):
                return x.__len__(1)
            results = []
            results.append(str([1, 2].__len__()))
            results.append(str((1,).__len__()))
            results.append(str({"a": 1}.__len__()))
            results.append(str({1}.__len__()))
            results.append(str("ab".__len__()))
            results.append(str(b"ab".__len__()))
            results.append(str(range(5).__len__()))
            results.append(str(defaultdict(list, {"a": 1}).__len__()))
            results.append(str(Counter("aab").__len__()))
            results.append(str(deque([1, 2]).__len__()))
            results.append(str(ChainMap({"a": 1}, {"b": 2}).__len__()))
            results.append(str([1, 2].__len__() + 1))
            results.append(str(len("ab") == "ab".__len__()))
            vals = [[1], (1,), {"a": 1}, {1}, "ab", b"ab", range(2), defaultdict(list), Counter(), deque([1]), ChainMap({"a": 1})]
            for v in vals:
                try:
                    len_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__len__")))
            results.append(str(callable("ab".__len__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "2",
            "1",
            "1",
            "1",
            "2",
            "2",
            "5",
            "1",
            "2",
            "2",
            "2",
            "3",
            "True",
            "TypeError",
            "list.__len__() expects no arguments.",
            "TypeError",
            "tuple.__len__() expects no arguments.",
            "TypeError",
            "dict.__len__() expects no arguments.",
            "TypeError",
            "set.__len__() expects no arguments.",
            "TypeError",
            "str.__len__() expects no arguments.",
            "TypeError",
            "bytes.__len__() expects no arguments.",
            "TypeError",
            "range.__len__() expects no arguments.",
            "TypeError",
            "defaultdict.__len__() expects no arguments.",
            "TypeError",
            "Counter.__len__() expects no arguments.",
            "TypeError",
            "deque.__len__() expects no arguments.",
            "TypeError",
            "ChainMap.__len__() expects no arguments.",
            "True",
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
    public async Task ViewLenDundersAdvanceLikeCpython()
    {
        // Dict and ChainMap views expose __len__ with exactly the len()
        // value, like CPython.
        var script = new LythonEngine().Compile("""
            from collections import ChainMap
            def len_arg(x):
                return x.__len__(1)
            results = []
            d = {"a": 1, "b": 2}
            results.append(str(d.keys().__len__()))
            results.append(str(d.values().__len__()))
            results.append(str(d.items().__len__()))
            results.append(str(len(d.keys()) == d.keys().__len__()))
            cm = ChainMap({"a": 1})
            results.append(str(cm.keys().__len__()))
            results.append(str(cm.values().__len__()))
            results.append(str(cm.items().__len__()))
            vals = [d.keys(), d.values(), d.items(), cm.keys(), cm.values(), cm.items()]
            for v in vals:
                try:
                    len_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr({}.keys(), "__len__")))
            results.append(str(callable({}.values().__len__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "2",
            "2",
            "2",
            "True",
            "1",
            "1",
            "1",
            "TypeError",
            "dict_keys.__len__() expects no arguments.",
            "TypeError",
            "dict_values.__len__() expects no arguments.",
            "TypeError",
            "dict_items.__len__() expects no arguments.",
            "TypeError",
            "ChainMap.keys.__len__() expects no arguments.",
            "TypeError",
            "ChainMap.values.__len__() expects no arguments.",
            "TypeError",
            "ChainMap.items.__len__() expects no arguments.",
            "True",
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
    public async Task ContainerContainsDundersAdvanceLikeCpython()
    {
        // Sized containers expose __contains__ with exactly the in-operator
        // value, like CPython (dict_values has none, like CPython).
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            def contains_arg(x):
                return x.__contains__()
            results = []
            results.append(str([1, 2].__contains__(2)))
            results.append(str([1, 2].__contains__(9)))
            results.append(str("abc".__contains__("b")))
            results.append(str("abc".__contains__("z")))
            results.append(str((1,).__contains__(1)))
            results.append(str({"a": 1}.__contains__("a")))
            results.append(str({"a": 1}.__contains__("z")))
            results.append(str({1}.__contains__(1)))
            results.append(str(b"ab".__contains__(98)))
            results.append(str(b"ab".__contains__(b"a")))
            results.append(str(range(5).__contains__(3)))
            results.append(str(range(5).__contains__(9)))
            dd = defaultdict(list, {"a": [1]})
            results.append(str(dd.__contains__("a")))
            results.append(str(dd.__contains__("z")))
            results.append(str(Counter("aab").__contains__("b")))
            results.append(str(Counter("aab").__contains__("z")))
            results.append(str(deque([1, 2]).__contains__(2)))
            results.append(str(deque([1, 2]).__contains__(9)))
            cm = ChainMap({"a": 1}, {"b": 2})
            results.append(str(cm.__contains__("b")))
            results.append(str(cm.__contains__("z")))
            d = {"a": 1, "b": 2}
            results.append(str(d.keys().__contains__("a")))
            results.append(str(d.keys().__contains__("z")))
            results.append(str(2 in d.values()))
            results.append(str(d.items().__contains__(("b", 2))))
            scm = ChainMap({"a": 1})
            results.append(str(scm.keys().__contains__("a")))
            results.append(str(scm.values().__contains__(1)))
            results.append(str(scm.items().__contains__(("a", 1))))
            try:
                d.values().__contains__(1)
            except AttributeError as e:
                results.append(type(e).__name__)
            try:
                {1}.__contains__([])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                {"a": 1}.__contains__([])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            for v in [[1], (1,), {"a": 1}, {1}, "ab", b"ab", range(2), defaultdict(list), Counter(), deque([1]), ChainMap({"a": 1}), {"a": 1}.keys(), {"a": 1}.items(), ChainMap({"a": 1}).keys(), ChainMap({"a": 1}).values(), ChainMap({"a": 1}).items()]:
                try:
                    contains_arg(v)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__contains__")))
            results.append(str(callable("ab".__contains__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "False",
            "True",
            "False",
            "True",
            "True",
            "False",
            "True",
            "True",
            "True",
            "True",
            "False",
            "True",
            "False",
            "True",
            "False",
            "True",
            "False",
            "True",
            "False",
            "True",
            "False",
            "True",
            "True",
            "True",
            "True",
            "True",
            "AttributeError",
            "TypeError",
            "unhashable type: 'list'",
            "TypeError",
            "unhashable type: 'list'",
            "TypeError",
            "Method 'list.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'tuple.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'dict.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'set.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'str.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'bytes.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'range.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'defaultdict.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'Counter.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'deque.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'ChainMap.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'dict_keys.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'dict_items.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'ChainMap.keys.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'ChainMap.values.__contains__' is missing argument 'item'.",
            "TypeError",
            "Method 'ChainMap.items.__contains__' is missing argument 'item'.",
            "True",
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
    public async Task ContainerGetItemDundersAdvanceLikeCpython()
    {
        // Subscriptable builtins expose __getitem__ with exactly the []
        // value, like CPython (sets and views have none, like CPython).
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            def getitem_arg(x, i, j):
                return x.__getitem__(i, j)
            results = []
            results.append(str([10, 20].__getitem__(0)))
            results.append(str([10, 20].__getitem__(-1)))
            results.append(str("abc".__getitem__(1)))
            results.append(str((7, 8).__getitem__(1)))
            results.append(str({"a": 1}.__getitem__("a")))
            results.append(str(b"ab".__getitem__(0)))
            results.append(str(range(5).__getitem__(2)))
            dd = defaultdict(list, {"a": [1]})
            results.append(str(dd.__getitem__("a")))
            results.append(str(dd.__getitem__("missing")))
            results.append(str("missing" in dd))
            results.append(str(Counter("aab").__getitem__("a")))
            results.append(str(Counter("aab").__getitem__("z")))
            results.append(str(deque([7, 8]).__getitem__(1)))
            results.append(str(ChainMap({"a": 1}, {"b": 2}).__getitem__("b")))
            results.append(str([10, 20, 30].__getitem__(slice(0, 2))))
            results.append(str("abcdef".__getitem__(slice(1, 5, 2))))
            try:
                [1].__getitem__(5)
            except IndexError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                "ab".__getitem__("x")
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                {"a": 1}.__getitem__([])
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                {"a": 1}.__getitem__("z")
            except KeyError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            vals = [([1], 0, 1), ((1,), 0, 1), ({"a": 1}, "a", 1), ("ab", 0, 1), (b"ab", 0, 1), (range(2), 0, 1), (defaultdict(list), "a", 1), (Counter(), "a", 1), (deque([1]), 0, 1), (ChainMap({"a": 1}), "a", 1)]
            for (v, i, j) in vals:
                try:
                    getitem_arg(v, i, j)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__getitem__")))
            results.append(str(hasattr({1}, "__getitem__")))
            results.append(str(hasattr({}.keys(), "__getitem__")))
            results.append(str(callable("ab".__getitem__)))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "10",
            "20",
            "b",
            "8",
            "1",
            "97",
            "2",
            "[1]",
            "[]",
            "True",
            "2",
            "0",
            "8",
            "2",
            "[10, 20]",
            "bd",
            "IndexError",
            "list index out of range",
            "TypeError",
            "string indices must be integers, not 'str'",
            "TypeError",
            "unhashable type: 'list'",
            "KeyError",
            "'z'",
            "TypeError",
            "Method 'list.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'tuple.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'str.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'range.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'defaultdict.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'Counter.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'deque.__getitem__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.__getitem__' received too many positional arguments.",
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
    public async Task ContainerSetItemDundersAdvanceLikeCpython()
    {
        // Mutable containers expose __setitem__ with exactly the []=
        // effect, like CPython (immutables have none, like CPython).
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            def setitem_arg(x, i):
                return x.__setitem__(i)
            results = []
            l = [1, 2]
            results.append(str(l.__setitem__(0, 9)))
            results.append(str(l))
            d = {"a": 1}
            results.append(str(d.__setitem__("b", 2)))
            results.append(str(len(d)))
            results.append(str(d["b"]))
            dd = defaultdict(list)
            results.append(str(dd.__setitem__("k", [1])))
            results.append(str(dd["k"]))
            c = Counter("aab")
            results.append(str(c.__setitem__("a", 5)))
            results.append(str(c["a"]))
            q = deque([1, 2])
            results.append(str(q.__setitem__(0, 9)))
            results.append(str(list(q)))
            cm = ChainMap({"a": 1})
            results.append(str(cm.__setitem__("b", 2)))
            results.append(str(cm["b"]))
            s = [1, 2, 3]
            results.append(str(s.__setitem__(slice(0, 2), [7, 8])))
            results.append(str(s))
            try:
                (1,).__setitem__(0, 1)
            except AttributeError as e:
                results.append(type(e).__name__)
            try:
                [1].__setitem__(5, 9)
            except IndexError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            for (v, i) in [([1], 0), ({"a": 1}, "a"), (defaultdict(list), "a"), (Counter(), "a"), (deque([1]), 0), (ChainMap({"a": 1}), "a")]:
                try:
                    setitem_arg(v, i)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__setitem__")))
            results.append(str(hasattr((1,), "__setitem__")))
            results.append(str(hasattr("ab", "__setitem__")))
            results.append(str(hasattr({1}, "__setitem__")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "None",
            "[9, 2]",
            "None",
            "2",
            "2",
            "None",
            "[1]",
            "None",
            "5",
            "None",
            "[9, 2]",
            "None",
            "2",
            "None",
            "[7, 8, 3]",
            "AttributeError",
            "IndexError",
            "list assignment index out of range",
            "TypeError",
            "Method 'list.__setitem__' is missing argument 'value'.",
            "TypeError",
            "Method 'dict.__setitem__' is missing argument 'value'.",
            "TypeError",
            "Method 'defaultdict.__setitem__' is missing argument 'value'.",
            "TypeError",
            "Method 'Counter.__setitem__' is missing argument 'value'.",
            "TypeError",
            "Method 'deque.__setitem__' is missing argument 'value'.",
            "TypeError",
            "Method 'ChainMap.__setitem__' is missing argument 'value'.",
            "True",
            "False",
            "False",
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
    public async Task ContainerDelItemDundersAdvanceLikeCpython()
    {
        // Mutable containers expose __delitem__ with exactly the del []
        // effect, like CPython (immutables have none, like CPython).
        var script = new LythonEngine().Compile("""
            from collections import defaultdict, Counter, deque, ChainMap
            def delitem_arg(x, i, j):
                return x.__delitem__(i, j)
            results = []
            l = [1, 2]
            results.append(str(l.__delitem__(0)))
            results.append(str(l))
            d = {"a": 1, "b": 2}
            results.append(str(d.__delitem__("a")))
            results.append(str(d == {"b": 2}))
            dd = defaultdict(list, {"a": [1]})
            results.append(str(dd.__delitem__("a")))
            results.append(str(len(dd)))
            c = Counter("aab")
            results.append(str(c.__delitem__("z")))
            results.append(str(len(c)))
            results.append(str(c.__delitem__("a")))
            results.append(str(len(c)))
            q = deque([1, 2])
            results.append(str(q.__delitem__(0)))
            results.append(str(list(q)))
            cm = ChainMap({"a": 1}, {"b": 2})
            results.append(str(cm.__delitem__("a")))
            results.append(str(len(cm)))
            s = [1, 2, 3]
            results.append(str(s.__delitem__(slice(0, 2))))
            results.append(str(s))
            try:
                (1,).__delitem__(0)
            except AttributeError as e:
                results.append(type(e).__name__)
            try:
                {"a": 1}.__delitem__("z")
            except KeyError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            for (v, i, j) in [([1], 0, 1), ({"a": 1}, "a", 1), (defaultdict(list), "a", 1), (Counter(), "a", 1), (deque([1]), 0, 1), (ChainMap({"a": 1}), "a", 1)]:
                try:
                    delitem_arg(v, i, j)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__delitem__")))
            results.append(str(hasattr((1,), "__delitem__")))
            results.append(str(hasattr("ab", "__delitem__")))
            results.append(str(hasattr({1}, "__delitem__")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "None",
            "[2]",
            "None",
            "True",
            "None",
            "0",
            "None",
            "2",
            "None",
            "1",
            "None",
            "[2]",
            "None",
            "1",
            "None",
            "[3]",
            "AttributeError",
            "KeyError",
            "'z'",
            "TypeError",
            "Method 'list.__delitem__' received too many positional arguments.",
            "TypeError",
            "Method 'dict.__delitem__' received too many positional arguments.",
            "TypeError",
            "Method 'defaultdict.__delitem__' received too many positional arguments.",
            "TypeError",
            "Method 'Counter.__delitem__' received too many positional arguments.",
            "TypeError",
            "Method 'deque.__delitem__' received too many positional arguments.",
            "TypeError",
            "Method 'ChainMap.__delitem__' received too many positional arguments.",
            "True",
            "False",
            "False",
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
    public async Task SequenceConcatRepeatDundersAdvanceLikeCpython()
    {
        // Sequence types expose __add__/__mul__/__rmul__ with exactly the
        // operator values (no __radd__, like CPython).
        var script = new LythonEngine().Compile("""
            def two_args(x, a, b):
                return x(a, b)
            results = []
            results.append(str([1, 2].__add__([3])))
            results.append(str("ab".__add__("cd")))
            results.append(str((7,).__add__((8,))))
            results.append(str(b"ab".__add__(b"cd")))
            results.append(str([1].__mul__(3)))
            results.append(str([1].__mul__(0)))
            results.append(str("ab".__mul__(2)))
            results.append(str("ab".__mul__(-1)))
            results.append(str((1,).__mul__(2)))
            results.append(str(b"ab".__mul__(2)))
            results.append(str([1].__rmul__(2)))
            results.append(str("ab".__rmul__(2)))
            try:
                [1].__add__("x")
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                "ab".__mul__("x")
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            try:
                (1,).__rmul__("x")
            except TypeError as e:
                results.append(type(e).__name__)
                results.append(str(e))
            for f in [[1].__add__, (1,).__add__, "ab".__add__, b"ab".__add__, [1].__mul__, (1,).__mul__, "ab".__mul__, b"ab".__mul__, [1].__rmul__, (1,).__rmul__, "ab".__rmul__, b"ab".__rmul__]:
                try:
                    two_args(f, 1, 2)
                except TypeError as e:
                    results.append(type(e).__name__)
                    results.append(str(e))
            results.append(str(hasattr([1], "__rmul__")))
            results.append(str(hasattr((1,), "__radd__")))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "[1, 2, 3]",
            "abcd",
            "(7, 8)",
            "b'abcd'",
            "[1, 1, 1]",
            "[]",
            "abab",
            "",
            "(1, 1)",
            "b'abab'",
            "[1, 1]",
            "abab",
            "TypeError",
            "can only concatenate list (not \"str\") to list",
            "TypeError",
            "can't multiply sequence by non-int of type 'str'",
            "TypeError",
            "can't multiply sequence by non-int of type 'tuple'",
            "TypeError",
            "Method 'list.__add__' received too many positional arguments.",
            "TypeError",
            "Method 'tuple.__add__' received too many positional arguments.",
            "TypeError",
            "Method 'str.__add__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__add__' received too many positional arguments.",
            "TypeError",
            "Method 'list.__mul__' received too many positional arguments.",
            "TypeError",
            "Method 'tuple.__mul__' received too many positional arguments.",
            "TypeError",
            "Method 'str.__mul__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__mul__' received too many positional arguments.",
            "TypeError",
            "Method 'list.__rmul__' received too many positional arguments.",
            "TypeError",
            "Method 'tuple.__rmul__' received too many positional arguments.",
            "TypeError",
            "Method 'str.__rmul__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__rmul__' received too many positional arguments.",
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
}
