using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N16: host-operation failures route by type (HostOperationException), never by
// matching English message text. These tests pin the routing and the public
// exception shape without asserting any failure wording: the inner host message
// is arbitrary ("boom"), and the args assertion is self-consistency (args carries
// the message exactly once), so neither test depends on the "Host ..." text.
public sealed class OsWalkErrorRoutingTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static MockLythonHost FailingDirHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/d/f0", "x");
        host.FailListDir("/d", "boom");
        return host;
    }

    [Fact]
    public async Task WalkRoutesHostListDirFailureToOnerror()
    {
        var script = Compile(
            """
            import os
            seen = []
            def hook(error):
                seen.append(1)
            walked = [x for x in os.walk("/d", onerror=hook)]
            return [len(walked), len(seen)]
            """);
        var expected = new List<object?> { new BigInteger(0), new BigInteger(1) };
        var sync = script.Run(FailingDirHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(FailingDirHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ScandirFailureKeepsExceptionShape()
    {
        var script = Compile(
            """
            import os
            try:
                list(os.scandir("/d"))
                return "no-error"
            except RuntimeError as error:
                return [type(error).__name__, error.args == (str(error),)]
            """);
        var expected = new List<object?> { "RuntimeError", true };
        var sync = script.Run(FailingDirHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(FailingDirHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
