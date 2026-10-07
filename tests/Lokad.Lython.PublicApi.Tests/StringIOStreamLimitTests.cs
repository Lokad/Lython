using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringIOStreamLimitTests
{
    [Fact]
    public async Task PrivateStreamBackingDoesNotUseTheGuestCollectionItemLimit()
    {
        await AssertBothModes("import io\ns=io.StringIO('abc')\nprint(s.read())",
            new LythonRunOptions { MaxCollectionSize = 1 }, "abc\n");
    }

    [Fact]
    public async Task OversizedStringResultsFailBeforeMovingTheCursor()
    {
        await AssertBothModes("""
            import io
            s=io.StringIO()
            s.write('abc')
            s.write('def')
            s.seek(0)
            for call in [s.getvalue, lambda:s.read(4), s.readline]:
                try:
                    call()
                except RuntimeError:
                    print('err')
            print(s.tell(),s.read(3))
            """, new LythonRunOptions { MaxStringLength = 3 }, "err\nerr\nerr\n0 abc\n");
    }

    private static async Task AssertBothModes(string source, LythonRunOptions options, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
