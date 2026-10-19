using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class InstanceOwnerLifetimeTests
{
    public static IEnumerable<object[]> DroppedCases()
    {
        foreach (var kind in new[] { "ordered", "instance", "child", "empty", "init", "foreign", "shallow" })
            foreach (var asynchronous in new[] { false, true })
                yield return [kind, asynchronous];
    }

    public static IEnumerable<object[]> RetainedCases()
    {
        foreach (var kind in new[] { "ordered", "instance", "child", "empty", "init", "shallow" })
            foreach (var asynchronous in new[] { false, true })
                yield return [kind, asynchronous];
    }

    [Theory]
    [MemberData(nameof(DroppedCases))]
    public async Task WorkloadBatchesReturnSharedAccounting(string kind, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        for (var batch = 0; batch < 8; batch++)
        {
            using var result = await Run(Workload(kind, false).Replace("1500", "16"), asynchronous, budget);
            Assert.True(result.Success, result.Failure?.ExceptionType + ":" + result.Failure?.Message);
            Assert.Equal(new BigInteger(16), result.ReturnValue);
            result.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(RetainedCases))]
    public async Task RetainedResultsStillExhaustRealLiveStorage(string kind, bool asynchronous)
    {
        using var result = await Run(Workload(kind, true), asynchronous);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    public static IEnumerable<object[]> CopyAndConstructorGoldens()
    {
        (string Source, object Expected)[] cases =
        {
            ("import copy, json\nseen = []\nclass Bomb:\n def __deepcopy__(self, memo):\n  dst = memo[id(source)]\n  seen.append([hasattr(dst, 'first'), hasattr(dst, 'second'), hasattr(dst, 'later'), getattr(dst, 'first', None)])\n  raise ValueError('stop')\nclass Holder:\n pass\nsource = Holder()\nsource.first = 1\nsource.second = Bomb()\nsource.later = 3\ntry:\n copy.deepcopy(source)\nexcept ValueError:\n pass\nreturn json.dumps(seen)\n", "[[false, false, false, null]]"),
            ("import copy\nclass Holder:\n pass\nsource = Holder()\nsource.loop = source\nsource.items = [source, source]\ndst = copy.deepcopy(source)\nreturn dst is not source and dst.loop is dst and dst.items[0] is dst and dst.items[1] is dst\n", true),
            ("import json\nseen = []\nclass Broken:\n def __init__(self):\n  seen.append(self)\n  self.value = 17\n  raise ValueError('stop')\ntry:\n Broken()\nexcept ValueError:\n pass\nreturn json.dumps([len(seen), seen[0].value])\n", "[1, 17]"),
            ("saved = None\nclass Reuse:\n def __new__(cls):\n  if saved is None:\n   return object.__new__(cls)\n  return saved\n def __init__(self):\n  self.count = getattr(self, 'count', 0) + 1\nsaved = Reuse()\nfor i in range(5):\n last = Reuse()\nreturn last is saved and saved.count == 6\n", true)
        };
        foreach (var (source, expected) in cases)
            foreach (var asynchronous in new[] { false, true })
                yield return [source, expected, asynchronous];
    }

    [Theory]
    [MemberData(nameof(CopyAndConstructorGoldens))]
    public async Task CallbackPartialStateCyclesAndAllocatorAliasesStayCompatible(string source, object expected, bool asynchronous)
    {
        using var result = await Run(source, asynchronous);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.ReturnValue);
    }

    private static string Workload(string kind, bool retain)
    {
        var capture = retain ? "saved.append(value)" : "value";
        string setup;
        string operation;
        switch (kind)
        {
            case "ordered":
                setup = "from collections import OrderedDict\nsource = [(1, 1), (2,)]\n";
                operation = retain ? "saved.append(OrderedDict([(1, 1)]))" : "OrderedDict(source)";
                break;
            case "instance":
            case "child":
                setup = "import copy\nclass Bomb:\n def __deepcopy__(self, memo):\n" +
                    (retain ? kind == "child" ? "  saved.append(memo[id(source.first)])\n" : "  saved.append(memo[id(source)])\n" : "") +
                    "  raise ValueError('stop')\nclass Holder:\n pass\nsource = Holder()\nsource.first = " +
                    (kind == "child" ? "[1] * 32" : "1") + "\nsource.second = Bomb()\n";
                operation = "copy.deepcopy(source)";
                break;
            case "init":
                setup = "class Holder:\n def __init__(self):\n" +
                    (retain ? "  saved.append(self)\n" : "") + "  raise ValueError('stop')\n";
                operation = "Holder()";
                break;
            case "foreign":
                setup = "class Holder:\n def __new__(cls):\n  return None\n";
                operation = "Holder()";
                break;
            default:
                setup = "import copy\nclass Holder:\n pass\nsource = Holder()\n";
                operation = kind == "shallow" ? "copy.copy(source)" : "Holder()";
                operation = "value = " + operation + "\n  " + capture;
                break;
        }
        return "saved = []\n" + setup + "for i in range(1500):\n try:\n  " + operation +
            "\n except (TypeError, ValueError):\n  pass\nreturn i + 1\n";
    }

    private static async Task<LythonExecutionResult> Run(string source, bool asynchronous, ILythonMemoryBudget? budget = null)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("with open('/ready', 'r') as stream:\n stream.read()\n" + source, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/ready", "ready");
            var result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
            return result;
        }
        var synchronous = new MockLythonHost();
        synchronous.SeedFile("/ready", "ready");
        var completed = script.Run(synchronous, options);
        Assert.InRange(completed.PeakExecutionMemoryBytes, 1, 65536);
        return completed;
    }
}
