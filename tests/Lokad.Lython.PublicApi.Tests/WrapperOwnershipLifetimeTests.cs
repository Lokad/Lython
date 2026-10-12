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
            "\n except ValueError:\n  return True\n return False\n" +
            "for i in range(2000):\n assert build(collections." + (counter ? "Counter" : "defaultdict") + ")\nreturn i + 1\n";
        var result = await Run(body, asynchronous);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(2000), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyAndUnionTurnoverDoesNotStrandIntermediateDictionaries(bool asynchronous)
    {
        var result = await Run("""
            from collections import defaultdict, ChainMap
            original = defaultdict(None, [(1, None)])
            chain = ChainMap(original)
            for i in range(500):
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
        var result = await Run(body, asynchronous);
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

    private static async Task<LythonExecutionResult> Run(string body, bool asynchronous)
    {
        var script = new LythonEngine().Compile("with open('/ready', 'r') as stream:\n stream.read()\n" + body);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
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
