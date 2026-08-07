using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionParityTests
{
    public static TheoryData<string> PurePrograms => new()
    {
        "return str([x * x for x in range(8) if x % 2 == 0])\n",
        "def f(a, b=2, *, c=3):\n    return a + b + c\nreturn str(f(1, c=4))\n",
        "class Base:\n    def value(self):\n        return 'base'\nclass Child(Base):\n    def value(self):\n        return super().value() + '-child'\nreturn Child().value()\n",
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
