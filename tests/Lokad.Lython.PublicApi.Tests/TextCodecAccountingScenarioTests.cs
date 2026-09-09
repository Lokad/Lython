using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG07: codec paths must preserve decode values and error contracts while
/// their scratch stays bounded.
/// </summary>
public sealed class TextCodecAccountingScenarioTests
{
    [Fact]
    public async Task CodecContractsStayExact()
    {
        // MG07: bounding decode scratch must not change decoded values,
        // newline translation, or the strict/handler error contracts.
        var script = new LythonEngine().Compile(
            """
            results = []
            results.append(bytes([72, 105]).decode("utf-8"))
            results.append(bytes([255, 65]).decode("utf-8", errors="replace"))
            results.append(bytes([255]).decode("utf-8", errors="backslashreplace"))
            results.append(bytes([65, 13, 10, 66]).decode("utf-8"))
            try:
                bytes([255]).decode("utf-8")
            except UnicodeDecodeError:
                results.append("strict")
            try:
                bytes([65]).decode("utf-8", errors="bogus")
            except ValueError:
                results.append("badhandler")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "Hi", "\uFFFDA", "\\xff", "A\r\nB", "strict", "badhandler" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
