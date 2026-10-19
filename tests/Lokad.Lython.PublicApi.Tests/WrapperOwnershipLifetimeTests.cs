using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WrapperOwnershipLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FailedPopulationReclaimsItsUnpublishedOwner(bool counter, bool asynchronous)
    {
        var call = counter ? "ctor(bad())" : "ctor(None, [(1, None), (2,)])";
        var body = "import collections\ndef bad():\n yield 1\n raise ValueError('stop')\n" +
            "def build(ctor):\n try:\n  " + call +
            "\n except (TypeError, ValueError):\n  return True\n return False\n" +
            "for i in range(16):\n assert build(collections." + (counter ? "Counter" : "defaultdict") + ")\nreturn i + 1\n";
        using var result = await Run(body, asynchronous);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(16), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyAndUnionTurnoverDoesNotStrandIntermediateDictionaries(bool asynchronous)
    {
        using var result = await Run("""
            from collections import defaultdict, ChainMap
            original = defaultdict(None, [(1, None)])
            chain = ChainMap(original)
            for i in range(16):
                copied = original.copy()
                assert len(copied) == 1
                combined = original | {2: None}
                assert len(combined) == 2
                reflected = {2: None} | original
                assert len(reflected) == 2
                chain_copy = chain.copy()
                assert len(chain_copy) == 1
            return len(original)
            """, asynchronous);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(BigInteger.One, result.ReturnValue);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeepcopyFailurePreservesEscapedClonesAndReclaimsDroppedOnes(bool retain, bool asynchronous)
    {
        var save = retain ? "saved.append(memo[id(source)])" : "clone = memo[id(source)]";
        var body = "import copy\nfrom collections import defaultdict\nsaved = []\n" +
            "class Bomb:\n def __deepcopy__(self, memo):\n  " + save +
            "\n  raise ValueError('stop')\n" +
            "source = defaultdict(None, {'payload': [1] * 64, 'bomb': Bomb()})\n" +
            "for i in range(128):\n try:\n  copy.deepcopy(source)\n except ValueError:\n  pass\nreturn len(saved)\n";
        using var result = await Run(retain ? body : body.Replace("range(128)", "range(2)"), asynchronous, batches: retain ? 1 : 8);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(BigInteger.Zero, result.ReturnValue);
        }
    }

    private static async Task<LythonExecutionResult> Run(string body, bool asynchronous, int batches = 8)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("with open('/ready', 'r') as stream:\n stream.read()\n" + body, budget);
        var baseline = budget.ReservedBytes;
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        LythonExecutionResult? last = null;
        for (var batch = 0; batch < batches; batch++)
        {
            LythonExecutionResult result;
            if (asynchronous)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/ready", "ready");
                result = await script.RunAsync(host, options);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            else
            {
                var host = new MockLythonHost();
                host.SeedFile("/ready", "ready");
                result = script.Run(host, options);
            }
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
            if (batch == batches - 1) last = result;
            else
            {
                using (result) Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        return last!;
    }
}
