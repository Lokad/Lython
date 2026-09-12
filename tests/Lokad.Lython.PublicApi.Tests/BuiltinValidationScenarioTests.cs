using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinValidationScenarioTests
{
    [Theory]
    [InlineData("min([])\n", "ValueError", "min() iterable argument is empty")]
    [InlineData("max([])\n", "ValueError", "max() iterable argument is empty")]
    [InlineData("min([1], key=1)\n", "TypeError", "'int' object is not callable")]
    [InlineData("max([1], key=1)\n", "TypeError", "'int' object is not callable")]
    [InlineData("min(1, 2, key=1)\n", "TypeError", "'int' object is not callable")]
    [InlineData("sorted([3, 1], key=1)\n", "TypeError", "'int' object is not callable")]
    [InlineData("x = [3, 1]\nx.sort(key=1)\n", "TypeError", "'int' object is not callable")]
    [InlineData("sorted([1], key='a')\n", "TypeError", "'str' object is not callable")]
    [InlineData("str(1, 2, 3)\n", "TypeError", "str() argument 'encoding' must be str, not int")]
    [InlineData("str(1, \"utf-8\")\n", "TypeError", "decoding to str: need a bytes-like object, int found")]
    [InlineData("str(\"a\", \"utf-8\")\n", "TypeError", "decoding str is not supported")]
    [InlineData("bytes(1, 2)\n", "TypeError", "bytes() argument 'encoding' must be str, not int")]
    [InlineData("bytes([1], \"utf-8\")\n", "TypeError", "encoding without a string argument")]
    [InlineData("bytes(\"a\", \"utf-8\", 1)\n", "TypeError", "bytes() argument 'errors' must be str, not int")]
    [InlineData("str(b\"a\", \"utf-8\", 1)\n", "TypeError", "str() argument 'errors' must be str, not int")]
    [InlineData("bytes(\"a\", None)\n", "TypeError", "bytes() argument 'encoding' must be str, not None")]
    [InlineData("str(b\"a\", None)\n", "TypeError", "str() argument 'encoding' must be str, not NoneType")]
    [InlineData("{**1}\n", "TypeError", "'int' object is not a mapping")]
    [InlineData("{**None}\n", "TypeError", "'NoneType' object is not a mapping")]
    [InlineData("{**'a'}\n", "TypeError", "'str' object is not a mapping")]
    [InlineData("def f(a):\n return a\nf(*1)\n", "TypeError", "__main__.f() argument after * must be an iterable, not int")]
    [InlineData("def f(a):\n return a\nf(**1)\n", "TypeError", "__main__.f() argument after ** must be a mapping, not int")]
    [InlineData("len(*1)\n", "TypeError", "len() argument after * must be an iterable, not int")]
    [InlineData("[].append(*1)\n", "TypeError", "list.append() argument after * must be an iterable, not int")]
    [InlineData("str(*1)\n", "TypeError", "str() argument after * must be an iterable, not int")]
    [InlineData("min(*1)\n", "TypeError", "min() argument after * must be an iterable, not int")]
    [InlineData("len(*None)\n", "TypeError", "len() argument after * must be an iterable, not NoneType")]
    [InlineData("import functools\ndef f(p):\n return p(*1)\nf(functools.partial(int))\n", "TypeError", "functools.partial(<class 'int'>) argument after * must be an iterable, not int")]
    [InlineData("class C:\n def __call__(self, a):\n  return a\nC()(*1)\n", "TypeError", "<C object> argument after * must be an iterable, not int")]
    [InlineData("class C:\n pass\ndef f(**k):\n return 0\nf(**C())\n", "TypeError", "__main__.f() argument after ** must be a mapping, not C")]
    [InlineData("def f(s):\n return s.find(1)\nf(\"abc\")\n", "TypeError", "find() argument 1 must be str, not int")]
    [InlineData("def f(s):\n return s.rfind(1)\nf(\"abc\")\n", "TypeError", "rfind() argument 1 must be str, not int")]
    [InlineData("def f(s):\n return s.index(1)\nf(\"abc\")\n", "TypeError", "index() argument 1 must be str, not int")]
    [InlineData("def f(s):\n return s.rindex(1)\nf(\"abc\")\n", "TypeError", "rindex() argument 1 must be str, not int")]
    [InlineData("def f(s):\n return s.count(1)\nf(\"abc\")\n", "TypeError", "count() argument 1 must be str, not int")]
    [InlineData("def f(s):\n return s.find(None)\nf(\"abc\")\n", "TypeError", "find() argument 1 must be str, not None")]
    [InlineData("def f(s):\n return s.startswith(1)\nf(\"abc\")\n", "TypeError", "startswith first arg must be str or a tuple of str, not int")]
    [InlineData("def f(s):\n return s.endswith(1)\nf(\"abc\")\n", "TypeError", "endswith first arg must be str or a tuple of str, not int")]
    [InlineData("def f(s):\n return s.startswith(None)\nf(\"abc\")\n", "TypeError", "startswith first arg must be str or a tuple of str, not NoneType")]
    [InlineData("def f(s):\n return s.startswith((\"x\", 1))\nf(\"abc\")\n", "TypeError", "tuple for startswith must only contain str, not int")]
    [InlineData("def f(s):\n return s.endswith((None,))\nf(\"abc\")\n", "TypeError", "tuple for endswith must only contain str, not NoneType")]
    [InlineData("def f(s):\n return s.startswith((\"x\", 1.5))\nf(\"abc\")\n", "TypeError", "tuple for startswith must only contain str, not float")]
    [InlineData("def f(s):\n return s.startswith(b\"a\")\nf(\"abc\")\n", "TypeError", "startswith first arg must be str or a tuple of str, not bytes")]
    [InlineData("def f(s):\n return s.strip(1)\nf(\"abc\")\n", "TypeError", "strip arg must be None or str")]
    [InlineData("def f(s):\n return s.lstrip(1)\nf(\"abc\")\n", "TypeError", "lstrip arg must be None or str")]
    [InlineData("def f(s):\n return s.rstrip(1)\nf(\"abc\")\n", "TypeError", "rstrip arg must be None or str")]
    [InlineData("def f(s):\n return s.strip(b\"a\")\nf(\"abc\")\n", "TypeError", "strip arg must be None or str")]
    [InlineData("range(1, 2, 0)\n", "ValueError", "must not be zero")]
    [InlineData("int(\"bad\")\n", "ValueError", "invalid literal")]
    [InlineData("float(\"bad\")\n", "ValueError", "could not convert string to float: 'bad'")]
    [InlineData("sum([\"a\"])\n", "TypeError", "unsupported operand type(s) for +: 'int' and 'str'")]
    [InlineData("sum([b\"a\"])\n", "TypeError", "unsupported operand type(s) for +: 'int' and 'bytes'")]
    [InlineData("sum([1], 'a')\n", "TypeError", "sum() can't sum strings [use ''.join(seq) instead]")]
    [InlineData("sum([], 'a')\n", "TypeError", "sum() can't sum strings [use ''.join(seq) instead]")]
    [InlineData("sum([1], b'a')\n", "TypeError", "sum() can't sum bytes [use b''.join(seq) instead]")]
    [InlineData("raise \"bad\"\n", "TypeError", "exceptions must derive from BaseException")]
    [InlineData("raise int\n", "TypeError", "exceptions must derive from BaseException")]
    [InlineData("raise ValueError(\"x\") from 1\n", "TypeError", "exception causes must derive from BaseException")]
    [InlineData("def f():\n    raise ValueError(\"x\") from 1\nf()\n", "TypeError", "exception causes must derive from BaseException")]
    [InlineData("isinstance(1, 1)\n", "TypeError", "isinstance() arg 2 must be a type, a tuple of types, or a union")]
    [InlineData("isinstance(\"x\", (1, str))\n", "TypeError", "isinstance() arg 2 must be a type, a tuple of types, or a union")]
    [InlineData("issubclass(1, int)\n", "TypeError", "issubclass() arg 1 must be a class")]
    [InlineData("issubclass(int, 1)\n", "TypeError", "issubclass() arg 2 must be a class, a tuple of classes, or a union")]
    [InlineData("issubclass(int, (str, 1))\n", "TypeError", "issubclass() arg 2 must be a class, a tuple of classes, or a union")]
    [InlineData("list(map(1, [2]))\n", "TypeError", "'int' object is not callable")]
    [InlineData("list(filter(1, [2]))\n", "TypeError", "'int' object is not callable")]
    [InlineData("list(map(None, [1]))\n", "TypeError", "'NoneType' object is not callable")]
    [InlineData("iter(1, 2)\n", "TypeError", "iter(v, w): v must be callable")]
    [InlineData("iter(None, 1)\n", "TypeError", "iter(v, w): v must be callable")]
    [InlineData("import functools\ndef f(g):\n    return g(1)\nf(functools.partial)\n", "TypeError", "the first argument must be callable")]
    [InlineData("import functools\ndef f(g, x):\n    return g(x)\nf(functools.partialmethod, 1)\n", "TypeError", "is not callable or a descriptor")]
    [InlineData("import functools\ndef f(g, x):\n    return g(x)\nf(functools.partialmethod, 'x')\n", "TypeError", "is not callable or a descriptor")]
    [InlineData("reversed(1)\n", "TypeError", "'int' object is not reversible")]
    [InlineData("reversed(None)\n", "TypeError", "'NoneType' object is not reversible")]
    [InlineData("from dataclasses import field\ndefault_factory = list\nfield(default = 1, default_factory = default_factory)\n", "compile", "cannot specify both default and default_factory")]
    [InlineData("from dataclasses import field\nfield(metadata = 1)\n", "TypeError", "metadata=...) expects a dict or None")]
    [InlineData("from dataclasses import dataclass\n@dataclass(order=True, eq=False)\nclass Bad:\n    x: int\n", "TypeError", "requires eq=True")]
    [InlineData("from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Bad:\n    x: int\n    def __hash__(self):\n        return 1\n", "TypeError", "cannot be combined with an explicit __hash__")]
    [InlineData("from dataclasses import dataclass, field, replace\n@dataclass\nclass Box:\n    x: int\n    y: int = field(init=False, default=1)\nreplace(Box(1), y=2)\n", "ValueError", "cannot override init=False field")]
    [InlineData("from dataclasses import InitVar, dataclass, replace\n@dataclass\nclass Box:\n    x: int\n    y: InitVar[int]\nreplace(Box(1, 2), x=3)\n", "ValueError", "InitVar 'y' must be specified")]
    [InlineData("from dataclasses import dataclass\n@dataclass\nclass Box:\n    x: int\nd = {Box(1): 1}\n", "TypeError", "hashable")]
    [InlineData("import math\nmath.sqrt(-1)\n", "ValueError", "math domain error")]
    [InlineData("import math\nmath.log(0)\n", "ValueError", "math domain error")]
    [InlineData("import math\nmath.isclose(1, 2, -1)\n", "ValueError", "non-negative")]
    [InlineData("import math\nmath.prod([1, \"x\"])\n", "TypeError", "iterable of real numbers")]
    [InlineData("import math\nmath.floor(\"x\")\n", "compile", "expects a real number")]
    [InlineData("import datetime\na = datetime.datetime(2024, 1, 1)\nb = datetime.datetime(2024, 1, 1, tzinfo=datetime.timezone.utc)\na < b\n", "TypeError", "naive and timezone-aware datetimes")]
    [InlineData("import datetime\ndatetime.timezone(1)\n", "TypeError", "expects a timedelta offset")]
    [InlineData("import datetime\ndatetime.date.fromisoformat(\"bad\")\n", "ValueError", "Invalid isoformat string")]
    [InlineData("import datetime\ndatetime.datetime.now(1)\n", "TypeError", "expects tz to be a timezone or None")]
    [InlineData("hasattr(1, 2)\n", "TypeError", "attribute name must be string, not 'int'")]
    [InlineData("getattr(1, 2)\n", "TypeError", "attribute name must be string, not 'int'")]
    [InlineData("setattr(1, 2, 3)\n", "TypeError", "attribute name must be string, not 'int'")]
    [InlineData("delattr(1, 2)\n", "TypeError", "attribute name must be string, not 'int'")]
    [InlineData("hasattr(1, None)\n", "TypeError", "attribute name must be string, not 'NoneType'")]
    [InlineData("vars(1)\n", "TypeError", "vars() argument must have __dict__ attribute")]
    [InlineData("vars(\"x\")\n", "TypeError", "vars() argument must have __dict__ attribute")]
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
            Assert.Equal(exceptionType, result.Failure?.ExceptionType);
            Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SumWithInstances_ResolvesAddProtocols()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class C:
    def __init__(self, v):
        self.v = v
    def __add__(self, o):
        return self.v + o
    def __radd__(self, o):
        return self.v + o

parts = []
parts.append(str(sum([C(1), C(2)])))
parts.append(str(sum([C(1), C(2)], C(10))))
parts.append(str(sum([])))
parts.append(str(sum([1, 2, 3])))
parts.append(str(sum([1.5, 2])))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3|13|0|6|3.5", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task CallDoubleStarMapping_AcceptsMappings()
    {
        const string source = "from collections import Counter, defaultdict, ChainMap\ndef f(**k):\n    return sorted(k)\nreturn str(f(**Counter(x=1))) + \"|\" + str(f(**defaultdict(int, y=2))) + \"|\" + str(f(**ChainMap({\"z\": 3}))) + \"|\" + str(f(**{\"w\": 4}))\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("['x']|['y']|['z']|['w']", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("['x']|['y']|['z']|['w']", asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData("dict([([], 1)])\n", "TypeError", "hashable")]
    public void DictConstructorFailure_ReportsExpectedException(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
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
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("repr()\n", "is missing argument 'value'")]
    [InlineData("sum()\n", "is missing argument 'iterable'")]
    [InlineData("sorted()\n", "is missing argument 'iterable'")]
    [InlineData("any()\n", "any() takes exactly one argument (0 given)")]
    [InlineData("all()\n", "all() takes exactly one argument (0 given)")]
    [InlineData("enumerate()\n", "is missing argument 'iterable'")]
    [InlineData("list(1, 2)\n", "received too many positional arguments")]
    [InlineData("tuple(1, 2)\n", "received too many positional arguments")]
    [InlineData("dict(1, 2)\n", "expected at most 1 positional argument")]
    [InlineData("set(1, 2)\n", "received too many positional arguments")]
    [InlineData("range(\"a\")\n", "'str' object cannot be interpreted as an integer")]
    [InlineData("type(\"Name\", (), {})\n", "supports exactly one argument in Lython")]
    public void BuiltinArityFailure_ReportsTypeError(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("key = 1\nsorted([1], key = key)\n", "TypeError", "'int' object is not callable")]
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
            Assert.Equal(expectedFailureKind, result.Failure?.ExceptionType);
            Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CallBindingMatchesPositionalOnlyAndTruthTestSemantics()
    {
        var result = new LythonEngine().Run(
            """
values = ["a\n".splitlines(1)[0], str(sorted([1, 2], reverse=1))]
items = [1, 2]
items.sort(reverse="yes")
values.append(str(items))
try:
    int(value="12")
except TypeError:
    values.append("caught")
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a\n|[2, 1]|[2, 1]|caught", result.ReturnValue);
    }

    [Fact]
    public void LenAllAnyArityFailures_NameCountLikeCpython()
    {
        var result = new LythonEngine().Run(
            """
for f in [len, all, any]:
    try:
        print(f())
    except TypeError as e:
        print(str(e))
    try:
        print(f([1], [2]))
    except TypeError as e:
        print(str(e))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("len() takes exactly one argument (0 given)\nlen() takes exactly one argument (2 given)\nall() takes exactly one argument (0 given)\nall() takes exactly one argument (2 given)\nany() takes exactly one argument (0 given)\nany() takes exactly one argument (2 given)\n", result.StandardOutput);
    }

    [Fact]
    public void FloatUnderscorePlacement_RejectsLikeCpython()
    {
        var result = new LythonEngine().Run(
            """
for src in ["1__0", "_1", "1_", "+_1", "1_.5", "1._5", "1e_5", "0.5_"]:
    try:
        print(float(src))
    except ValueError as e:
        print(str(e))
print(float("1_0"))
print(float("1_0e1_0"))
print(float("1.5_0"))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("could not convert string to float: '1__0'\ncould not convert string to float: '_1'\ncould not convert string to float: '1_'\ncould not convert string to float: '+_1'\ncould not convert string to float: '1_.5'\ncould not convert string to float: '1._5'\ncould not convert string to float: '1e_5'\ncould not convert string to float: '0.5_'\n10.0\n100000000000.0\n1.5\n", result.StandardOutput);
    }

    [Fact]
    public void FloatBytesInputs_ParseLikeCpython()
    {
        var result = new LythonEngine().Run(
            """
for src in [b"1", b" 1.5 ", b"1_0", b"a", b"  a  ", bytes([97, 39, 98]), bytes([255]), b""]:
    try:
        print(float(src))
    except ValueError as e:
        print(str(e))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1.0\n1.5\n10.0\ncould not convert string to float: b'a'\ncould not convert string to float: b'  a  '\ncould not convert string to float: b\"a'b\"\ncould not convert string to float: b'\\xff'\ncould not convert string to float: b''\n", result.StandardOutput);
    }

    [Fact]
    public void CharBaseFailures_MatchCpythonShapes()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class J:
    def __index__(self):
        return 65

parts = []
parts.append(str(chr(J())))
parts.append(str(hex(J())))
parts.append(str(oct(J())))
parts.append(str(bin(J())))
for bad in ["", "ab", None, 12, 1.5, b"ab"]:
    try:
        parts.append(str(ord(bad)))
    except TypeError as e:
        parts.append(str(e))
parts.append(str(ord(b"x")))
parts.append(str(ord("A")))
try:
    parts.append(str(chr("a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(hex("a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(chr(1114112)))
except ValueError as e:
    parts.append(str(e))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("A|0x41|0o101|0b1000001|ord() expected a character, but string of length 0 found|ord() expected a character, but string of length 2 found|ord() expected string of length 1, but NoneType found|ord() expected string of length 1, but int found|ord() expected string of length 1, but float found|ord() expected a character, but string of length 2 found|120|65|'str' object cannot be interpreted as an integer|'str' object cannot be interpreted as an integer|chr() arg not in range(0x110000)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AbsFailures_MatchCpythonShapes()
    {
        var result = new LythonEngine().Run(
            """
class C: pass

for bad in ["a", None, [1], C()]:
    try:
        print(abs(bad))
    except TypeError as e:
        print(str(e))
print(abs(-5))
print(abs(3.5))
print(abs(True))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("bad operand type for abs(): 'str'\nbad operand type for abs(): 'NoneType'\nbad operand type for abs(): 'list'\nbad operand type for abs(): 'C'\n5\n3.5\n1\n", result.StandardOutput);
    }

    [Fact]
    public void PowProtocol_MatchesCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class C:
    def __pow__(self, exp):
        return 42
class R:
    def __rpow__(self, other):
        return 43
class T:
    def __pow__(self, exp, mod):
        return (exp, mod)
class A:
    def __pow__(self, other):
        return NotImplemented
class B:
    def __rpow__(self, other):
        return (7, 8)
class D:
    __pow__ = 5

parts = []
parts.append(str(pow(C(), 2)))
parts.append(str(pow(2, R())))
parts.append(str(pow(A(), B())))
parts.append(str(pow(T(), 2, 3)))
parts.append(str(pow(C(), 2, None)))
try:
    parts.append(str(pow(2, 3, "a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(pow(2.0, 2, 3)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(pow("a", 2, 3)))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(pow(D(), 2, 3)))
except TypeError as e:
    parts.append(str(e))
parts.append(str(pow(2, 3, 5)))
parts.append(str(pow(2, 3)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("42|43|(7, 8)|(2, 3)|42|unsupported operand type(s) for ** or pow(): 'int', 'int', 'str'|pow() 3rd argument not allowed unless all arguments are integers|unsupported operand type(s) for ** or pow(): 'str', 'int', 'int'|'int' object is not callable|3|8", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DivModProtocol_MatchesCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class C:
    def __divmod__(self, other):
        return 42
class R:
    def __rdivmod__(self, other):
        return (9, 9)
class A:
    def __divmod__(self, other):
        return NotImplemented
class B:
    def __rdivmod__(self, other):
        return (7, 8)

parts = []
parts.append(str(divmod(C(), 3)))
parts.append(str(divmod(1, R())))
parts.append(str(divmod(A(), B())))
parts.append(str(divmod(7, 2)))
parts.append(str(divmod(-7, 2)))
parts.append(str(divmod(7.0, 2)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("42|(9, 9)|(7, 8)|(3, 1)|(-4, 1)|(3.0, 1.0)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RangeBounds_CoerceIndexLikeCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class J:
    def __index__(self):
        return 3
class C: pass

parts = []
parts.append(str(list(range(J()))))
parts.append(str(list(range(1, J()))))
parts.append(str(list(range(0, 10, J()))))
parts.append(str(list(range(True, False))))
try:
    parts.append(str(range("a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(range(C())))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(range()))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(range(1, 2, 3, 4)))
except TypeError as e:
    parts.append(str(e))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 1, 2]|[1, 2]|[0, 3, 6, 9]|[]|'str' object cannot be interpreted as an integer|'C' object cannot be interpreted as an integer|range expected at least 1 argument, got 0|range expected at most 3 arguments, got 4", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RoundProtocol_MatchesCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
class C:
    def __round__(self):
        return 7
class N:
    def __round__(self, n):
        return n + 10
class B:
    def __round__(self):
        return "x"
class P: pass
class D:
    __round__ = 5

parts = []
parts.append(str(round(C())))
parts.append(str(round(N(), 1)))
parts.append(str(round(B())))
try:
    parts.append(str(round("a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(round(P())))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(round(1.5, "a")))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(round(D())))
except TypeError as e:
    parts.append(str(e))
try:
    parts.append(str(round(C(), "a")))
except TypeError as e:
    parts.append(str(e))
parts.append(str(round(2.5)))
parts.append(str(round(1.5, None)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7|11|x|type str doesn't define __round__ method|type P doesn't define __round__ method|'str' object cannot be interpreted as an integer|'int' object is not callable|Function '__round__' received too many positional arguments.|2|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void IntFailures_MatchCpythonShapes()
    {
        var result = new LythonEngine().Run(
            """
class C: pass
class I:
    def __int__(self):
        return "x"

for bad in [[], None, object(), C]:
    try:
        print(int(bad))
    except TypeError as e:
        print(str(e))
try:
    print(int(I()))
except TypeError as e:
    print(str(e))
print(int("12"))
print(int(3.9))
print(int(True))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("int() argument must be a string, a bytes-like object or a real number, not 'list'\nint() argument must be a string, a bytes-like object or a real number, not 'NoneType'\nint() argument must be a string, a bytes-like object or a real number, not 'object'\nint() argument must be a string, a bytes-like object or a real number, not 'type'\n__int__ returned non-int (type str)\n12\n3\n1\n", result.StandardOutput);
    }

    [Fact]
    public void FloatFailures_MatchCpythonShapes()
    {
        var result = new LythonEngine().Run(
            """
class F:
    def __float__(self):
        return "x"

for src in ["a", "  a  ", "a" + chr(39) + "b"]:
    try:
        print(float(src))
    except ValueError as e:
        print(str(e))
for bad in [object(), [1], None, F()]:
    try:
        print(float(bad))
    except TypeError as e:
        print(str(e))
print(float("inf"))
print(float(" 1.5 "))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("could not convert string to float: 'a'\ncould not convert string to float: '  a  '\ncould not convert string to float: \"a'b\"\nfloat() argument must be a string or a real number, not 'object'\nfloat() argument must be a string or a real number, not 'list'\nfloat() argument must be a string or a real number, not 'NoneType'\nF.__float__ returned non-float (type str)\ninf\n1.5\n", result.StandardOutput);
    }



    [Fact]
    public void DictionaryConstructionUpdateAndPopMatchPythonForms()
    {
        var result = new LythonEngine().Run(
            """
d = dict([("a", 1)], b=2)
d.update({"c": 3}, d=4)
d.update([("e", 5)])
return str(d) + "|" + str(d.pop("missing", None)) + "|" + str(d.pop("other", 9))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{'a': 1, 'b': 2, 'c': 3, 'd': 4, 'e': 5}|None|9", result.ReturnValue);
    }

    [Fact]
    public void DictionaryUpdateAcceptsSetsOfPairs()
    {
        var result = new LythonEngine().Run(
            """
d = {}
d.update({("f", 6), ("g", 7)})
return str(sorted(d.items()))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("[('f', 6), ('g', 7)]", result.ReturnValue);
    }

    [Theory]
    [InlineData("def get(k):\n    return {}[k]\nget(\"x\")\n", "'x'")]
    [InlineData("def pop(k):\n    return {}.pop(k)\npop(\"x\")\n", "'x'")]
    [InlineData("def delete(k):\n    d = {}\n    del d[k]\ndelete(\"x\")\n", "'x'")]
    [InlineData("def get(k):\n    return {1: 2}[k]\nget(3)\n", "3")]
    [InlineData("def get(k):\n    return {(1, \"a\"): 2}[k]\nget((1, \"b\"))\n", "(1, 'b')")]
    [InlineData("from collections import ChainMap\ndef get(k):\n    return ChainMap({})[k]\nget(\"x\")\n", "'x'")]
    [InlineData("print({}.popitem())\n", "'popitem(): dictionary is empty'")]
    [InlineData("print({1}.remove(2))\n", "2")]
    public void UncaughtKeyErrors_ProjectQuotedKeys(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("KeyError", result.Failure?.ExceptionType);
        Assert.Equal(message, result.Failure?.Message);
    }

    [Fact]
    public void PythonKeywordSpellings_AreAcceptedForSupportedBuiltinsAndMethods()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha");

        var result = new LythonEngine().Run(
            """
import os

items = list("ab")
items.append("c")
items.extend(["d"])
d = dict([("a", 1)])
value = int("12")
text = open("/input.txt").read()
with open(file = "/output.txt", mode = "w") as handle:
    handle.write(text.replace("a", "A"))
    handle.writelines(["\n", str(value), "\n", str(d.get("a"))])

__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + os.path.basename("/output.txt") + "|" + open("/output.txt").read())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("['a', 'b', 'c', 'd']|output.txt|AlphA\n12\n1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void HighValueBuiltinKeywordShapes_AcceptPythonSpellings()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha");

        var result = new LythonEngine().Run(
            """
values = ["a", "b"]
pairs = []
for index, value in enumerate(values, start = 1):
    pairs.append(str(index) + ":" + value)

with open(file = "/input.txt", mode = "r", encoding = "utf-8") as reader:
    text = reader.read()

with open(file = "/printed.txt", mode = "w") as handle:
    print("text", text, sep = "=", end = "", file = handle, flush = True)

print("done", flush = True)

with open(file = "/out.txt", mode = "w") as handle:
    handle.write(",".join(pairs))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("1:a,2:b", host.ReadText("/out.txt"));
        Assert.Equal("text=alpha", host.ReadText("/printed.txt"));
        Assert.Equal("done\n", host.CapturedStandardOutput());
    }

    [Theory]
    [InlineData("range(start = 1)\n", "Builtin 'range' does not accept keyword arguments.")]
    [InlineData("range(stop = 3)\n", "Builtin 'range' does not accept keyword arguments.")]
    [InlineData("enumerate([1], iterable = [2])\n", "Builtin 'enumerate' got an unexpected keyword argument 'iterable'.")]
    [InlineData("enumerate(start = 1)\n", "Builtin 'enumerate' is missing argument 'iterable'.")]
    [InlineData("enumerate(iterable = [1], bad = 2)\n", "Builtin 'enumerate' got an unexpected keyword argument 'iterable'.")]
    [InlineData("open(\"/input.txt\", file = \"/other.txt\")\n", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) got multiple values for argument 'file/path'.")]
    [InlineData("open(mode = \"r\")\n", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) expects a file/path argument.")]
    [InlineData("open(target = \"/input.txt\")\n", "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener]) got an unexpected keyword argument 'target'.")]
    [InlineData("print(\"x\", destination = None)\n", "Builtin 'print' got an unexpected keyword argument 'destination'.")]
    [InlineData("text = \"abc\"\ntext.upper(value = 1)\n", "str.upper() expects no arguments.")]
    [InlineData("int(number = 1)\n", "Builtin 'int' got an unexpected keyword argument 'number'.")]
    [InlineData("int(1, value = 2)\n", "Builtin 'int' got an unexpected keyword argument 'value'.")]
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

    [Theory]
    [InlineData("append_text")]
    [InlineData("basename")]
    [InlineData("copy")]
    [InlineData("cwd")]
    [InlineData("dirname")]
    [InlineData("exists")]
    [InlineData("join_path")]
    [InlineData("listdir")]
    [InlineData("mkdir")]
    [InlineData("move")]
    [InlineData("read_text")]
    [InlineData("remove")]
    [InlineData("stat")]
    [InlineData("write_text")]
    public void NonPythonHostHelpers_AreNotDefaultScriptGlobals(string name)
    {
        var result = new LythonEngine().Run(name + "\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("NameError", result.Failure?.ExceptionType);
        Assert.Contains(name, result.Failure?.Message, StringComparison.Ordinal);
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
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("keywords must be strings", result.Failure?.Message, StringComparison.Ordinal);
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(copy["a"]) + "|" + str(source["a"]))
__lython_file.close()
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(same) + "|" + str(pair) + "|" + str(missing) + "|" + str(popped) + "|" + str(list(d.items())))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("one|pair|one|pair|[(1, 'one')]", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("items = []\nitems.append()\n", "list.append(value) expects one argument.")]
    [InlineData("items = []\nitems.extend()\n", "list.extend(iterable) expects one argument.")]
    [InlineData("items = []\nitems.pop(0, 1)\n", "list.pop([index]) expects zero or one argument.")]
    [InlineData("d = {}\nd.get()\n", "dict.get(key[, default]) expects one key and an optional default.")]
    [InlineData("d = {}\nd.get(\"a\", 1, 2)\n", "dict.get(key[, default]) expects one key and an optional default.")]
    [InlineData("d = {}\nd.keys(1)\n", "dict.keys() expects no arguments.")]
    [InlineData("d = {}\nd.values(1)\n", "dict.values() expects no arguments.")]
    [InlineData("d = {}\nd.items(1)\n", "dict.items() expects no arguments.")]
    [InlineData("d = {}\nd.update({}, {})\n", "dict.update([mapping]) expects zero or one mapping argument.")]
    [InlineData("d = {}\nd.update(1)\n", "dict.update(mapping) expects one dictionary argument.")]
    [InlineData("d = {}\nd.pop()\n", "dict.pop(key[, default]) expects one key and an optional default.")]
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
