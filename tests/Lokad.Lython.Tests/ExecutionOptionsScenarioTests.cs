using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionOptionsScenarioTests
{
    [Fact]
    public void CancelledRun_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = new LythonEngine().Run(
            """
value = "hello"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveCancellation_DuringPureInterpreterLoop_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();

        var runTask = Task.Run(() => new LythonEngine().Run(
            """
while True:
    pass
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            }));

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask;

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UsesCancellationTokenAndPreservesResultContract()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await new LythonEngine().RunAsync(
            """
value = "hello"
""",
            new MockLythonHost(),
            cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SucceedsForNormalExecution()
    {
        var result = await new LythonEngine().RunAsync(
            """
value = 40 + 2
return_value = value
""",
            new MockLythonHost(),
            new LythonRunOptions());

        Assert.True(result.Success);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task RunAsync_ActiveCancellation_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();

        var runTask = new LythonEngine().RunAsync(
            """
while True:
    pass
""",
            new MockLythonHost(),
            cancellationToken: cts.Token);

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask;

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HostCancellationToken_CancelsAsyncHostOperation()
    {
        using var cts = new CancellationTokenSource();

        var runTask = new LythonEngine().RunAsync(
            """
value = open("/input.txt").read()
""",
            new BlockingReadHost(),
            cancellationToken: cts.Token);

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecursionLimit_IsEnforced()
    {
        var result = new LythonEngine().Run(
            """
def loop():
    loop()

loop()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxRecursionDepth = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RecursionError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCallLimit_IsEnforced()
    {
        var host = new MockLythonHost();
        host.SeedFile("/a.txt", "A");
        host.SeedFile("/b.txt", "B");

        var result = new LythonEngine().Run(
            """
left = open("/a.txt").read()
right = open("/b.txt").read()
""",
            host,
            new LythonRunOptions
            {
                MaxHostCalls = 1
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum host call count exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroHostCallLimit_DoesNotFallBackToDefaultOrUnlimited()
    {
        var host = new MockLythonHost();
        host.SeedFile("/a.txt", "A");

        var result = new LythonEngine().Run(
            "value = open('/a.txt').read()",
            host,
            new LythonRunOptions
            {
                DisableDefaultLimits = true,
                MaxHostCalls = 0
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum host call count exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroCollectionLimit_AllowsEmptyButRejectsNonEmptyCollections()
    {
        var emptyResult = new LythonEngine().Run(
            "value = []",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 0 });
        var nonEmptyResult = new LythonEngine().Run(
            "value = [1]",
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 0 });

        Assert.True(emptyResult.Success, emptyResult.Failure?.Message);
        Assert.False(nonEmptyResult.Success);
        Assert.NotNull(nonEmptyResult.Failure);
        Assert.Contains("maximum collection size exceeded", nonEmptyResult.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mapping = {}\nmapping['key'] = 1")]
    [InlineData("from collections import Counter\ncounts = Counter()\ncounts['key'] = 1")]
    [InlineData("import operator\nmapping = {}\noperator.setitem(mapping, 'key', 1)")]
    public void ZeroCollectionLimit_RejectsSubscriptInsertion(string source)
    {
        var result = new LythonEngine().Run(
            source,
            new MockLythonHost(),
            new LythonRunOptions { MaxCollectionSize = 0 });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Contains("maximum collection size exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("steps")]
    [InlineData("recursion")]
    [InlineData("host-calls")]
    [InlineData("collection")]
    [InlineData("string")]
    [InlineData("host-read")]
    [InlineData("stdout")]
    [InlineData("stderr")]
    [InlineData("execution-memory")]
    [InlineData("projection-memory")]
    public void NegativeLimits_AreRejectedExplicitly(string limit)
    {
        var options = limit switch
        {
            "steps" => new LythonRunOptions { MaxExecutionSteps = -1 },
            "recursion" => new LythonRunOptions { MaxRecursionDepth = -1 },
            "host-calls" => new LythonRunOptions { MaxHostCalls = -1 },
            "collection" => new LythonRunOptions { MaxCollectionSize = -1 },
            "string" => new LythonRunOptions { MaxStringLength = -1 },
            "host-read" => new LythonRunOptions { MaxHostReadBytes = -1 },
            "stdout" => new LythonRunOptions { MaxStandardOutputBytes = -1 },
            "stderr" => new LythonRunOptions { MaxStandardErrorBytes = -1 },
            "execution-memory" => new LythonRunOptions { MaxExecutionMemoryBytes = -1 },
            "projection-memory" => new LythonRunOptions { MaxProjectionMemoryBytes = -1 },
            _ => throw new InvalidOperationException($"unknown test limit: {limit}")
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine().Run("return 1", new MockLythonHost(), options));
    }

    [Fact]
    public void ExecutionStepLimit_IsEnforced()
    {
        var result = new LythonEngine().Run(
            """
while True:
    pass
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionSteps = 50
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringLimit_IsEnforced()
    {
        var result = new LythonEngine().Run(
            """
value = "abcdef"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxStringLength = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum string length exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringLimit_IsEnforcedForConcatenation()
    {
        var result = new LythonEngine().Run(
            """
value = "abc" + "def"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxStringLength = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum string length exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringLimit_IsEnforcedForRepetition()
    {
        var result = new LythonEngine().Run(
            """
value = "ab" * 4
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxStringLength = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum string length exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForStringLiteralMaterialization()
    {
        var result = new LythonEngine().Run(
            """
value = "abcdefghijklmnopqrstuvwxyz0123456789"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 48
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionLimit_IsEnforced()
    {
        var result = new LythonEngine().Run(
            """
values = list(range(10))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxCollectionSize = 3
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforced()
    {
        var result = new LythonEngine().Run(
            """
values = []
while True:
    values.append("abcdef")
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 512
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsSharedAcrossNestedFunctionFrames()
    {
        var result = new LythonEngine().Run(
            """
def push(values, remaining):
    if remaining == 0:
        return
    values.append("abcdef")
    push(values, remaining - 1)

items = []
push(items, 100)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 512
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForAccumulatedStandardOutput()
    {
        var result = new LythonEngine().Run(
            """
while True:
    print("abcdefghij")
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 256
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_DoesNotAccumulateRepeatedConstantObservations()
    {
        var result = new LythonEngine().Run(
            """
i = 0
while i < 1000:
    value = "constant"
    i += 1
return value
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("constant", result.ReturnValue);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForTupleLiteralMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = (1, 2, 3, 4, 5, 6, 7, 8)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForBytesLiteralMaterialization()
    {
        var result = new LythonEngine().Run(
            """
value = b"abcdefghijklmnopqrstuvwxyz0123456789"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 48
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForListLiteralMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = [1, 2, 3, 4, 5, 6, 7, 8]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForDictionaryLiteralMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = {1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7}
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForBytesBuiltinMaterialization()
    {
        var result = new LythonEngine().Run(
            """
value = bytes(range(64))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForHostTextIngress()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", new string('a', 512));

        var result = new LythonEngine().Run(
            """
text = open("/input.txt").read()
""",
            host,
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForHostGlobalsNormalization()
    {
        var result = new LythonEngine().Run(
            """
return_value = payload
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128,
                Globals = new Dictionary<string, object?>
                {
                    ["payload"] = new Dictionary<string, object?>
                    {
                        ["items"] = Enumerable.Range(0, 16).Select(i => (object?)i).ToList()
                    }
                }
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForRegexFindAllMaterialization()
    {
        var result = new LythonEngine().Run(
            """
import re
matches = re.findall("a", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForJsonArrayProjection()
    {
        var result = new LythonEngine().Run(
            """
import json
value = json.loads("[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]")
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForVariadicKeywordPacking()
    {
        var result = new LythonEngine().Run(
            """
def collect(**kwargs):
    return kwargs

value = collect(a=1, b=2, c=3, d=4, e=5, f=6, g=7, h=8)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForListComprehensionMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = [n for n in range(32)]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForPathGlobMaterialization()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a0.md", "x");
        host.SeedFile("/repo/docs/a1.md", "x");
        host.SeedFile("/repo/docs/a2.md", "x");
        host.SeedFile("/repo/docs/a3.md", "x");
        host.SeedFile("/repo/docs/a4.md", "x");
        host.SeedFile("/repo/docs/a5.md", "x");
        host.SeedFile("/repo/docs/a6.md", "x");
        host.SeedFile("/repo/docs/a7.md", "x");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
items = Path("/repo/docs").glob("*.md")
return_value = list(items)
""",
            host,
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForStringSplitMaterialization()
    {
        var result = new LythonEngine().Run(
            """
value = ("alpha " * 24).split()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForDataclassFieldMetadataMaterialization()
    {
        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, field, fields

@dataclass
class Box:
    a: int = field(metadata={"k0": "v0", "k1": "v1", "k2": "v2", "k3": "v3"})
    b: int = field(metadata={"k4": "v4", "k5": "v5", "k6": "v6", "k7": "v7"})
    c: int = field(metadata={"k8": "v8", "k9": "v9", "k10": "v10", "k11": "v11"})

value = fields(Box)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 160
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForTupleConcatenation()
    {
        var result = new LythonEngine().Run(
            """
value = (1, 2, 3, 4, 5, 6) + (7, 8, 9, 10, 11, 12)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForStringConcatenation()
    {
        var result = new LythonEngine().Run(
            """
value = "abcdefghij" + "klmnopqrst" + "uvwxyz0123"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForBigIntegerPowerGrowth()
    {
        var result = new LythonEngine().Run(
            """
value = 2 ** 4096
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 256
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostReadLimit_IsEnforcedBeforeLargeTextIngress()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", new string('a', 512));

        var result = new LythonEngine().Run(
            """
text = open("/input.txt").read()
""",
            host,
            new LythonRunOptions
            {
                MaxHostReadBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("host text read exceeded maximum bytes", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StandardOutputLimit_IsEnforcedAsStructuredRuntimeFailure()
    {
        var result = new LythonEngine().Run(
            """
print("abcdefghij")
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxStandardOutputBytes = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("standard output exceeded maximum captured output bytes", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AwaitsAsynchronousHostReadsWithoutThreadPoolWrapper()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = await new LythonEngine().RunAsync(
            """
return open("/input.txt").read()
""",
            host,
            new LythonRunOptions());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously);
    }

    [Fact]
    public async Task RunAsync_AwaitsAsynchronousHostReadsInsideBoundMethods()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = await new LythonEngine().RunAsync(
            """
class Reader:
    def read(self):
        return open("/input.txt").read()

reader = Reader()
return reader.read()
""",
            host,
            new LythonRunOptions());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously);
    }

    [Fact]
    public void Run_WithAsynchronousHostRead_FailsFastWithRunAsyncGuidance()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = new LythonEngine().Run(
            """
return open("/input.txt").read()
""",
            host,
            new LythonRunOptions());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("use RunAsync", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(host.CompletedAsynchronously);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForBigIntegerLeftShiftGrowth()
    {
        var result = new LythonEngine().Run(
            """
value = 1 << 4096
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 256
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForPathPartsProjection()
    {
        var result = new LythonEngine().Run(
            """
from pathlib import Path
value = Path("/repo/deeply/nested/folder/with/many/segments/file.txt").parts
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 96
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForPartialKeywordProjection()
    {
        var result = new LythonEngine().Run(
            """
import functools

def f(**kwargs):
    return kwargs

p = functools.partial(f, a=1, b=2, c=3, d=4, e=5, f=6, g=7, h=8)
value = p.keywords
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_RangeSliceDoesNotMaterializeAList()
    {
        var result = new LythonEngine().Run(
            """
values = range(32)
tail = values[1:]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForDictItemsViewProjection()
    {
        var result = new LythonEngine().Run(
            """
values = {0: 0, 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7}
items = list(values.items())
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForItertoolsProductTupleMaterialization()
    {
        var result = new LythonEngine().Run(
            """
import itertools
value = list(itertools.product(range(6), range(6), range(6)))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 384
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForListCopyMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = list(range(24))
copy = values.copy()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 192
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForDictCopyMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = {0: 0, 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7}
copy = values.copy()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 192
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForSetCopyMaterialization()
    {
        var result = new LythonEngine().Run(
            """
values = {0, 1, 2, 3, 4, 5, 6, 7}
copy = values.copy()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 192
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForCounterCopyMaterialization()
    {
        var result = new LythonEngine().Run(
            """
from collections import Counter
value = Counter(range(32)).copy()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 192
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForSetUnionMaterialization()
    {
        var result = new LythonEngine().Run(
            """
left = {0, 1, 2, 3, 4, 5}
right = {6, 7, 8, 9, 10, 11}
value = left | right
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 192
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForTypeMroProjection()
    {
        var result = new LythonEngine().Run(
            """
class A:
    pass

class B(A):
    pass

class C(B):
    pass

value = C.__mro__
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 64
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionMemoryBudget_IsEnforcedForItertoolsZipLongestTupleMaterialization()
    {
        var result = new LythonEngine().Run(
            """
import itertools
value = list(itertools.zip_longest(range(12), range(12), fillvalue=None))
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 320
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionBudget_FailsDistinctlyFromScriptRuntimeBudget()
    {
        var result = new LythonEngine().Run(
            """
return ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccccccccccc"]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionMemoryBytes = 4096,
                MaxProjectionMemoryBytes = 128
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ProjectionError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("projection memory budget exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeeplyNestedGlobalRendering_IsStackSafe()
    {
        var nested = CreateDeeplyNestedList(700);

        var result = new LythonEngine().Run(
            """
text = str(data)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["data"] = nested
                }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
    }

    private static List<object?> CreateDeeplyNestedList(int depth)
    {
        object? current = "leaf";
        for (var i = 0; i < depth; i++)
        {
            current = new List<object?> { current };
        }

        return (List<object?>)current;
    }

    private sealed class BlockingReadHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ILythonTextInput? StandardInput => _inner.StandardInput;

        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

        public ILythonTextOutput? StandardError => _inner.StandardError;

        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }

    private sealed class DelayedReadHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public bool CompletedAsynchronously { get; private set; }

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ILythonTextInput? StandardInput => _inner.StandardInput;

        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

        public ILythonTextOutput? StandardError => _inner.StandardError;

        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            await Task.Delay(25, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CompletedAsynchronously = true;
            return await _inner.ReadTextUtf8Async(path, cancellationToken);
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }
}
