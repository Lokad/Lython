using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonSortingBeforeSkipTests
{
    [Fact]
    public async Task SortingPrecedesSkippingInBothEncoderPaths()
    {
        var script = new LythonEngine().Compile("""
            import json
            encoder = json.JSONEncoder(skipkeys=True, sort_keys=True)
            for obj in [{(1,): 1, 'x': 2}, {(1,): 1}, {(2,): 2, (1,): 1}]:
                for incremental in [False, True]:
                    try:
                        print(''.join(encoder.iterencode(obj)) if incremental else encoder.encode(obj))
                    except TypeError:
                        print('comparison failed')
            encoder.sort_keys = False
            print(encoder.encode({(1,): 1, 'x': 2}))
            print(''.join(encoder.iterencode({(1,): 1, 'x': 2})))
            """);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("comparison failed\ncomparison failed\n{}\n{}\n{}\n{}\n{\"x\": 2}\n{\"x\": 2}\n", result.StandardOutput);
        }
    }
}
