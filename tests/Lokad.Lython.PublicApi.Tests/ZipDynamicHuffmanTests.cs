using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ZipDynamicHuffmanTests
{
    // Synthetic archive independently read by CPython 3.13 and zlib. HCLEN=15
    // declares all 19 code-length entries, including the final symbol 15.
    private static byte[] Archive() => Convert.FromHexString(
        "504b0304140000000800000000008def02d28d000000010000000a0000006d65" +
        "6d6265722e62696e05e00190244992244922b1a87964f5ec3d00000000000000" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "00000000000000000000000000000000003cfaff03504b010214001400000008" +
        "00000000008def02d28d000000010000000a0000000000000000000000000000" +
        "0000006d656d6265722e62696e504b0506000000000100010038000000b50000" +
        "000000");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FullCodeLengthAlphabetReadsOrReportsCorruption(bool asynchronous, bool corrupt)
    {
        var payload = Archive();
        if (corrupt) payload[40] |= 6; // Reserved block type, not a CLR bounds failure.
        const string source = """
            import zipfile
            with zipfile.ZipFile('/fixture.zip') as archive:
                content=archive.read('member.bin')
                assert archive.testzip() is None
                return content
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var mock = new MockLythonHost(); mock.SeedBytes("/fixture.zip", payload);
            var delayed = new DelayedLythonHost(); delayed.SeedBytes("/fixture.zip", payload);
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
            {
                Assert.Equal(!corrupt, result.Success);
                if (corrupt) Assert.Equal("BadZipFile", result.Failure?.ExceptionType);
                else
                {
                    Assert.Equal(new byte[] { 0 }, Assert.IsType<byte[]>(result.ReturnValue));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            if (asynchronous) Assert.True(delayed.ClosedFileCount > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
