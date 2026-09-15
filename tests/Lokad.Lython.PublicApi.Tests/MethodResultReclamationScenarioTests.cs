using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M02: adopted string-method results register with refund-on-deny semantics,
// so a dropped fresh result never strands its construction charge when the
// pool entry cannot be funded. A bounded loop over fresh results completes
// under a small fixed budget instead of accumulating one strand per call.
public sealed class MethodResultReclamationScenarioTests
{
    [Fact]
    public async Task RepeatedUpperResultsStayBounded()
    {
        var script = new LythonEngine().Compile(
            "b = 'y' * 2048\nfor i in range(200):\n    y = b.upper()\nreturn len(y)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(2048);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16384 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
