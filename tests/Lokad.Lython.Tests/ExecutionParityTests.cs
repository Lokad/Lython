using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionParityTests
{
    public static TheoryData<string> PurePrograms => new()
    {
        "return str([x * x for x in range(8) if x % 2 == 0])\n",
        "def f(a, b=2, *, c=3):\n    return a + b + c\nreturn str(f(1, c=4))\n",
        "def f(*args, **kwargs):\n    return [args, kwargs]\nreturn str(f('head', *[1, 2], **{'name': 'value'}))\n",
        "class Base:\n    def value(self):\n        return 'base'\nclass Child(Base):\n    def value(self):\n        return super().value() + '-child'\nreturn Child().value()\n",
        "class Manager:\n    def __enter__(self):\n        return self\n    def __exit__(self, exc_type, exc, traceback):\n        return exc_type is ValueError and exc_type.__name__ == exc.type\nwith Manager():\n    raise ValueError('boom')\nreturn 'suppressed'\n",
        "values = []\nfor value in [0, 1]:\n    try:\n        values.append(str(4 // value))\n    except ZeroDivisionError as ex:\n        values.append(ex.type)\nreturn '|'.join(values)\n",
        "import json\nreturn json.dumps(json.loads('{\"b\":2,\"a\":[1,true,null]}'), sort_keys=True)\n",
        "import re\nreturn str(re.findall(r'(a+)(b?)', 'aaab aab'))\n",
        "from collections import Counter\nreturn str(Counter('abac').most_common())\n",
        "import itertools\nreturn str(list(itertools.chain([1, 2], [3])))\n",
        "from functools import partial\ndef add(a, b):\n    return a + b\nreturn str(partial(add, 2)(5))\n",
        "from dataclasses import dataclass\n@dataclass\nclass Point:\n    x: int\n    y: int = 2\nreturn repr(Point(1))\n",
        "from decimal import Decimal\nreturn str(Decimal('1.25') + Decimal('2.50'))\n",
        "import shlex\nreturn str(shlex.split(\"one 'two three'\"))\n",
        "import hashlib\nh = hashlib.sha256(b'a')\nh.update(b'b')\nreturn h.hexdigest()\n",
        "import gzip\nreturn str(gzip.decompress(gzip.compress(b'payload')) == b'payload')\n",
        "import time\nreturn str(time.gmtime(0)) + '|' + str(time.time_ns())\n",
        "last = -1\na = [(last := x) for x in range(3)]\nb = {(last := x) for x in range(3, 5)}\nc = {x: (last := x * 2) for x in range(2)}\nreturn str([a, b, c, last])\n",
    };

    [Theory]
    [MemberData(nameof(PurePrograms))]
    public async Task SyncAndAsyncRuntimesAgreeForPurePrograms(string source)
    {
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());

        AssertEquivalent(sync, asyncResult);
    }

    [Fact]
    public async Task SyncAndAsyncRuntimesAgreeForHostMediatedEffects()
    {
        const string source = """
import time
with open("/input.txt") as source:
    value = source.read()
with open("/output.txt", "w") as output:
    output.write(value.upper())
print(value, end="!")
time.sleep(0.001)
return str(time.monotonic_ns())
""";
        var syncHost = CreateEffectHost();
        var asyncHost = CreateEffectHost();

        var sync = new LythonEngine().Run(source, syncHost);
        var asyncResult = await new LythonEngine().RunAsync(source, asyncHost);

        AssertEquivalent(sync, asyncResult);
        Assert.Equal(syncHost.ReadText("/output.txt"), asyncHost.ReadText("/output.txt"));
        Assert.Equal(syncHost.CapturedStandardOutput(), asyncHost.CapturedStandardOutput());
    }

    [Fact]
    public async Task SyncAndAsyncRuntimesAwaitTheSameTruthinessProtocol()
    {
        const string source = """
class Flag:
    def __bool__(self):
        with open("/flag.txt") as source:
            return source.read() == "yes"

def evaluate():
    flag = Flag()
    values = []
    if flag:
        values.append("if")
    values.append("and" if flag and True else "bad")
    values.append("conditional" if flag else "bad")
    values.append(str(not flag))
    values.extend(["1" for ignored in [1] if flag])
    assert flag
    return "|".join(values)

return evaluate()
""";
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/flag.txt", "yes");
        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/flag.txt", "yes");

        var sync = new LythonEngine().Run(source, syncHost);
        var asyncResult = await new LythonEngine().RunAsync(source, asyncHost);

        AssertEquivalent(sync, asyncResult);
        Assert.Equal("if|and|conditional|False|1", asyncResult.ReturnValue);
        Assert.True(asyncHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task LoweredExpressionsPreservePythonOperatorProtocolsInBothExecutionModes()
    {
        const string source = """
class Box:
    def __add__(self, other):
        return 42
    def __radd__(self, other):
        return 43
    def __lt__(self, other):
        return True
    def __gt__(self, other):
        return True
    def __eq__(self, other):
        return True
    def __contains__(self, item):
        return item == 1
    def __neg__(self):
        return 44

evaluate = lambda: [Box() + Box(), 1 + Box(), Box() < Box(), 1 < Box(), Box() == Box(), 1 in Box(), -Box()]
return str(evaluate())
""";

        var sync = new LythonEngine().Run(source, new MockLythonHost());
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());

        AssertEquivalent(sync, asyncResult);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("[42, 43, True, True, True, True, 44]", sync.ReturnValue);
    }

    [Fact]
    public async Task AsyncLoweredOperatorsAwaitUserProtocolBodies()
    {
        const string source = """
class Box:
    def __add__(self, other):
        with open("/value.txt") as source:
            return source.read()

return (lambda: Box() + Box())()
""";
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "payload");
        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/value.txt", "payload");

        var sync = new LythonEngine().Run(source, syncHost);
        var asyncResult = await new LythonEngine().RunAsync(source, asyncHost);

        AssertEquivalent(sync, asyncResult);
        Assert.Equal("payload", asyncResult.ReturnValue);
        Assert.True(asyncHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task AsyncSortingAwaitsUserComparisonProtocols()
    {
        const string source = """
class Key:
    def __init__(self, value):
        self.value = value
    def __lt__(self, other):
        with open("/permission.txt") as source:
            allowed = source.read() == "yes"
        return allowed and self.value < other.value

values = [Key(3), Key(1), Key(2)]
return str([item.value for item in sorted(values)])
""";
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/permission.txt", "yes");
        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/permission.txt", "yes");

        var sync = new LythonEngine().Run(source, syncHost);
        var asyncResult = await new LythonEngine().RunAsync(source, asyncHost);

        AssertEquivalent(sync, asyncResult);
        Assert.Equal("[1, 2, 3]", asyncResult.ReturnValue);
        Assert.True(asyncHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task SyncAndAsyncRuntimesAgreeForRuntimeFailures()
    {
        const string source = "def fail():\n    raise ValueError('bad')\nfail()\n";

        var sync = new LythonEngine().Run(source, new MockLythonHost());
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());

        AssertEquivalent(sync, asyncResult);
    }

    private static MockLythonHost CreateEffectHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "hello");
        host.EnableTiming();
        return host;
    }

    private static void AssertEquivalent(LythonExecutionResult expected, LythonExecutionResult actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.ReturnValue, actual.ReturnValue);
        Assert.Equal(expected.StandardOutput, actual.StandardOutput);
        Assert.Equal(expected.StandardError, actual.StandardError);
        Assert.Equal(expected.ExitCode, actual.ExitCode);
        Assert.Equal(expected.Failure?.ExceptionType, actual.Failure?.ExceptionType);
        Assert.Equal(expected.Failure?.Message, actual.Failure?.Message);
        Assert.Equal(
            expected.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Message)),
            actual.Diagnostics.Select(diagnostic => (diagnostic.Code, diagnostic.Message)));
    }
}
