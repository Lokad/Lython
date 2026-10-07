using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesIOResourceTests
{
    [Fact]
    public async Task ByteStorageUsesMemoryRatherThanTextCollectionOrHostReadLimits()
    {
        var script = Compile("import io\ns=io.BytesIO(b'abc')\nassert s.read()==b'abc'\nassert s.getvalue()==b'abc'");
        var options = new LythonRunOptions
        {
            MaxCollectionSize = 1,
            MaxStringLength = 1,
            MaxHostReadBytes = 1,
            MaxStandardOutputBytes = 1,
            MaxExecutionMemoryBytes = 65536,
        };
        await AssertBothModes(script, options, "");
    }

    [Fact]
    public async Task LargeCheapSeeksAndDeniedWritesKeepThePrefix()
    {
        var script = Compile("""
            import io
            s=io.BytesIO(b'kept')
            s.seek(10**12)
            print(s.write(b''),s.tell())
            try:
                s.write(b'x')
            except MemoryError:
                print(s.getvalue(),s.tell())
            s.seek(0)
            s.write(b'yes')
            print(s.getvalue())
            """);
        await AssertBothModes(script, new LythonRunOptions { MaxExecutionMemoryBytes = 65536 },
            "0 1000000000000\nb'kept' 1000000000000\nb'yest'\n");
    }

    [Fact]
    public async Task DiscardedSnapshotsReclaimUnderASmallBudget()
    {
        var script = Compile("""
            import io
            s=io.BytesIO(b'abc')
            for _ in range(3000):
                s.seek(0)
                s.read()
                s.getvalue()
            print(s.getvalue())
            """);
        await AssertBothModes(script, new LythonRunOptions { MaxExecutionMemoryBytes = 65536 }, "b'abc'\n");
    }

    [Theory]
    [InlineData("getbuffer")]
    [InlineData("readinto")]
    public void UnsupportedBufferExportsFailAtTheKnownSurface(string name)
    {
        var script = new LythonEngine().Compile("import io\ns=io.BytesIO()\ns." + name + "()");
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Message.Contains(name, StringComparison.Ordinal));
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }

    private static async Task AssertBothModes(LythonCompiledScript script, LythonRunOptions options, string expected)
    {
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
