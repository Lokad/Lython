using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TeeReentrancyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecursiveSourcePullReportsTeeRuntimeError(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import tee
            def source():
                try:next(b)
                except RuntimeError as e:
                    print(str(e))
                yield 7
            a,b=tee(source())
            print(next(a),next(b))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("cannot re-enter the tee iterator\n7 7\n", result.StandardOutput);
    }
}
