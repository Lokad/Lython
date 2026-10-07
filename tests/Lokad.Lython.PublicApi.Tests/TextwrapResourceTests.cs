using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextwrapResourceTests
{
    [Fact]
    public async Task PrivateLineProcessingDoesNotUseTheGuestCollectionLimit()
    {
        await AssertBothModes("""
            import textwrap
            print(textwrap.dedent('  a\n  b\n  c'))
            print(textwrap.indent('a\nb\nc','>',lambda line:True))
            """, new LythonRunOptions { MaxCollectionSize = 1 }, "a\nb\nc\n>a\n>b\n>c\n");
    }

    [Fact]
    public async Task StringLimitStopsBeforeTheNextPredicate()
    {
        await AssertBothModes("""
            import textwrap
            def predicate(line):
                print(line.strip())
                return True
            try:
                textwrap.indent('a\nb\nc\nd','>>',predicate)
            except RuntimeError:
                print('err')
            """, new LythonRunOptions { MaxStringLength = 8 }, "a\nb\nc\nerr\n");
    }

    [Fact]
    public async Task StringLimitCountsUnicodeScalars()
    {
        await AssertBothModes("import textwrap\nprint(textwrap.indent('😀\\né','😀'))",
            new LythonRunOptions { MaxStringLength = 5 }, "😀😀\n😀é\n");
    }

    [Fact]
    public async Task AnUnchangedDedentKeepsAliasesUsable()
    {
        await AssertBothModes("""
            import textwrap
            text='a😀\nb'
            for i in range(100):
                assert textwrap.dedent(text) is text
            print(text)
            """, new LythonRunOptions { MaxExecutionMemoryBytes = 65536 }, "a😀\nb\n");
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
