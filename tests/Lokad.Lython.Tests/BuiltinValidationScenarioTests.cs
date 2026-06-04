using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BuiltinValidationScenarioTests
{
    [Theory]
    [InlineData("min([])\n", "ValueError", "empty sequence")]
    [InlineData("max([])\n", "ValueError", "empty sequence")]
    [InlineData("range(1, 2, 0)\n", "ValueError", "must not be zero")]
    [InlineData("int(\"bad\")\n", "ValueError", "could not be parsed")]
    [InlineData("float(\"bad\")\n", "ValueError", "input string")]
    [InlineData("sum([\"a\"])\n", "TypeError", "string or bytes operands")]
    [InlineData("sum([b\"a\"])\n", "TypeError", "string or bytes operands")]
    [InlineData("read_text(1)\n", "compile", "expects one string argument")]
    [InlineData("write_text(\"/x\", 1)\n", "compile", "expects two string arguments")]
    [InlineData("append_text(\"/x\", 1)\n", "compile", "expects two string arguments")]
    [InlineData("join_path()\n", "TypeError", "one or more string arguments")]
    [InlineData("dirname(1)\n", "compile", "expects one string argument")]
    [InlineData("basename(1)\n", "compile", "expects one string argument")]
    [InlineData("raise \"bad\"\n", "TypeError", "raise expects an exception instance")]
    [InlineData("from dataclasses import field\ndefault_factory = list\nfield(default = 1, default_factory = default_factory)\n", "compile", "cannot specify both default and default_factory")]
    [InlineData("from dataclasses import field\nfield(metadata = 1)\n", "TypeError", "metadata=...) expects a dict or None")]
    [InlineData("from dataclasses import dataclass\n@dataclass(order=True, eq=False)\nclass Bad:\n    x: int\n", "TypeError", "requires eq=True")]
    [InlineData("from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Bad:\n    x: int\n    def __hash__(self):\n        return 1\n", "TypeError", "cannot be combined with an explicit __hash__")]
    [InlineData("from dataclasses import dataclass, field, replace\n@dataclass\nclass Box:\n    x: int\n    y: int = field(init=False, default=1)\nreplace(Box(1), y=2)\n", "compile", "cannot override init=False field")]
    [InlineData("from dataclasses import InitVar, dataclass, replace\n@dataclass\nclass Box:\n    x: int\n    y: InitVar[int]\nreplace(Box(1, 2), x=3)\n", "compile", "InitVar 'y' must be specified")]
    [InlineData("from dataclasses import dataclass\n@dataclass\nclass Box:\n    x: int\nd = {Box(1): 1}\n", "TypeError", "hashable")]
    [InlineData("import math\nmath.sqrt(-1)\n", "ValueError", "math domain error")]
    [InlineData("import math\nmath.log(0)\n", "ValueError", "math domain error")]
    [InlineData("import math\nmath.isclose(1, 2, -1)\n", "ValueError", "non-negative")]
    [InlineData("import math\nmath.prod([1, \"x\"])\n", "TypeError", "iterable of real numbers")]
    [InlineData("import math\nmath.floor(\"x\")\n", "TypeError", "expects a real number")]
    [InlineData("import datetime\na = datetime.datetime(2024, 1, 1)\nb = datetime.datetime(2024, 1, 1, tzinfo=datetime.timezone.utc)\na < b\n", "TypeError", "naive and timezone-aware datetimes")]
    [InlineData("import datetime\ndatetime.timezone(1)\n", "TypeError", "expects a timedelta offset")]
    [InlineData("import datetime\ndatetime.date.fromisoformat(\"bad\")\n", "ValueError", "not recognized")]
    [InlineData("import datetime\ndatetime.datetime.now(1)\n", "TypeError", "expects tz to be a timezone or None")]
    public void BuiltinContractFailure_ReportsExpectedException(string source, string exceptionType, string messageFragment)
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

    [Theory]
    [InlineData("dict([([], 1)])\n", "TypeError", "hashable")]
    public void DictConstructorFailure_ReportsExpectedException(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("d = {[]: 1}\n", "dictionary keys must be hashable")]
    [InlineData("d = {1: 2}\nd[[1]] = 3\n", "dictionary keys must be hashable")]
    [InlineData("d = {(1, []): 2}\n", "dictionary keys must be hashable")]
    public void DictionaryKeyHashabilityFailures_ReportTypeError(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("str()\n", "is missing argument 'value'")]
    [InlineData("repr()\n", "is missing argument 'value'")]
    [InlineData("bool()\n", "is missing argument 'value'")]
    [InlineData("sum()\n", "is missing argument 'iterable'")]
    [InlineData("sorted()\n", "is missing argument 'iterable'")]
    [InlineData("any()\n", "expects one argument")]
    [InlineData("all()\n", "expects one argument")]
    [InlineData("enumerate()\n", "expects one or two arguments")]
    [InlineData("int()\n", "is missing argument 'value'")]
    [InlineData("float()\n", "is missing argument 'value'")]
    [InlineData("list(1, 2)\n", "received too many positional arguments")]
    [InlineData("tuple(1, 2)\n", "received too many positional arguments")]
    [InlineData("dict(1, 2)\n", "received too many positional arguments")]
    [InlineData("set(1, 2)\n", "received too many positional arguments")]
    [InlineData("range(\"a\")\n", "expects integer arguments")]
    [InlineData("exists()\n", "is missing argument 'path'")]
    [InlineData("listdir()\n", "is missing argument 'path'")]
    [InlineData("mkdir()\n", "is missing argument 'path'")]
    [InlineData("remove()\n", "is missing argument 'path'")]
    [InlineData("copy(\"/a\")\n", "is missing argument 'destination'")]
    [InlineData("move(\"/a\")\n", "is missing argument 'destination'")]
    [InlineData("cwd(1)\n", "expects no arguments")]
    [InlineData("stat()\n", "is missing argument 'path'")]
    [InlineData("type(\"Name\", (), {})\n", "supports exactly one argument in Lython")]
    public void BuiltinArityFailure_ReportsTypeError(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("key = 1\nsorted([1], key = key)\n", "compile", "callable or None")]
    [InlineData("reverse = 1\nsorted([1], reverse = reverse)\n", "compile", "expects a bool")]
    public void SortedKeywordFailures_ReportExpectedFailure(string source, string expectedFailureKind, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (expectedFailureKind == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(expectedFailureKind, result.Failure!.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void KeywordArguments_AreAcceptedForSupportedBuiltinsAndMethods()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha");

        var result = new LythonEngine().Run(
            """
items = list(iterable = "ab")
items.append(value = "c")
items.extend(iterable = ["d"])
d = dict(iterable = [("a", 1)])
value = int(value = "12")
text = read_text(path = "/input.txt")
with open(path = "/output.txt", mode = "w") as handle:
    handle.write(text = text.replace(old = "a", new = "A"))
    handle.writelines(lines = ["\n", str(value), "\n", str(d.get(key = "a"))])

write_text(path = "/out.txt", text = str(items) + "|" + dirname(path = "/output.txt") + "|" + read_text(path = "/output.txt"))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("[a, b, c, d]|.|AlphA\n12\n1", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("range(start = 1)\n", "Builtin 'range' does not accept keyword arguments.")]
    [InlineData("join_path(left = \"/a\", right = \"b\")\n", "Builtin 'join_path' does not accept keyword arguments.")]
    [InlineData("text = \"abc\"\ntext.upper(value = 1)\n", "str.upper() expects no arguments.")]
    [InlineData("int(number = 1)\n", "Builtin 'int' got an unexpected keyword argument 'number'.")]
    [InlineData("int(1, value = 2)\n", "Builtin 'int' got multiple values for argument 'value'.")]
    [InlineData("copy(source = \"/a\")\n", "Builtin 'copy' is missing argument 'destination'.")]
    [InlineData("text = \"abc\"\ntext.find(needle = \"a\")\n", "str.find(sub[, start[, end]]) expects one to three arguments.")]
    [InlineData("items = []\nitems.append(1, value = 2)\n", "list.append(value) expects one argument.")]
    [InlineData("d = {}\nd.get(default = 1)\n", "dict.get(key[, default]) expects one key and an optional default.")]
    public void KeywordArgumentContractFailures_ReportExpectedMessage(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CallDoubleStarUnpacking_RejectsNonStringKeys()
    {
        var result = new LythonEngine().Run(
            """
def f(**kwargs):
    return kwargs

f(**{1: 2})
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("string keys", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DictConstructor_CopiesDictionaryInput()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
source = {"a": 1}
copy = dict(source)
source.update({"a": 2})
write_text("/out.txt", str(copy["a"]) + "|" + str(source["a"]))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("1|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DictConstructorAndMethods_SupportHashableNonStringKeys()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
d = dict([(1, "one"), ((2, 3), "pair")])
same = d.get(1)
pair = d.get((2, 3))
missing = d.setdefault(True, "bool")
popped = d.pop((2, 3))
write_text("/out.txt", str(same) + "|" + str(pair) + "|" + str(missing) + "|" + str(popped) + "|" + str(list(d.items())))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("one|pair|one|pair|[(1, one)]", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("items = []\nitems.append()\n", "list.append(value) expects one argument.")]
    [InlineData("items = []\nitems.extend()\n", "list.extend(iterable) expects one argument.")]
    [InlineData("items = []\nitems.pop(0)\n", "list.pop() expects no arguments.")]
    [InlineData("d = {}\nd.get()\n", "dict.get(key[, default]) expects one key and an optional default.")]
    [InlineData("d = {}\nd.get(\"a\", 1, 2)\n", "dict.get(key[, default]) expects one key and an optional default.")]
    [InlineData("d = {}\nd.keys(1)\n", "dict.keys() expects no arguments.")]
    [InlineData("d = {}\nd.values(1)\n", "dict.values() expects no arguments.")]
    [InlineData("d = {}\nd.items(1)\n", "dict.items() expects no arguments.")]
    [InlineData("d = {}\nd.update()\n", "dict.update(mapping) expects one dictionary argument.")]
    [InlineData("d = {}\nd.update([])\n", "dict.update(mapping) expects one dictionary argument.")]
    [InlineData("d = {}\nd.pop()\n", "dict.pop(key) expects one key.")]
    [InlineData("items = []\nitems.copy(1)\n", "list.copy() expects no arguments.")]
    [InlineData("items = []\nitems.clear(1)\n", "list.clear() expects no arguments.")]
    [InlineData("d = {}\nd.copy(1)\n", "dict.copy() expects no arguments.")]
    [InlineData("d = {}\nd.clear(1)\n", "dict.clear() expects no arguments.")]
    [InlineData("d = {}\nd.setdefault()\n", "dict.setdefault(key[, default]) expects one key and an optional default.")]
    [InlineData("items = set([1])\nitems.add()\n", "Method 'set.add' is missing argument 'value'.")]
    [InlineData("items = set([1])\nitems.discard()\n", "Method 'set.discard' is missing argument 'value'.")]
    [InlineData("items = set([1])\nitems.remove()\n", "Method 'set.remove' is missing argument 'value'.")]
    [InlineData("items = set([1])\nitems.copy(1)\n", "set.copy() expects no arguments.")]
    [InlineData("items = set([1])\nitems.clear(1)\n", "set.clear() expects no arguments.")]
    public void CollectionMethodContractFailure_ReportsTypeError(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
        }
    }
}
