using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ChunkedReadLimitTests
{
    [Theory]
    [InlineData(false, "utf-8", false)]
    [InlineData(true, "utf-8", false)]
    [InlineData(false, "utf-8", true)]
    [InlineData(true, "utf-8", true)]
    [InlineData(false, "utf-16-le", false)]
    [InlineData(true, "utf-16-le", false)]
    [InlineData(false, "utf-16-le", true)]
    [InlineData(true, "utf-16-le", true)]
    public async Task SizedReadlineSpansWindowsAndRetainsItsResults(bool asynchronous, string encoding, bool preserveNewlines)
    {
        var content = string.Concat(Enumerable.Repeat("aé😀", 7000)) + "\r\nTAIL";
        var host = new MockLythonHost();
        if (encoding == "utf-8") host.SeedRawTextUtf8("/file", Encoding.UTF8.GetBytes(content));
        else host.SeedBytes("/file", Encoding.Unicode.GetBytes(content));
        var source = "with open('/file',encoding='" + encoding + "',newline=" + (preserveNewlines ? "''" : "None") + ") as f:\n" + """
    first=f.readline(10000)
    tail=f.readline()
    end=f.read()
print(len(first),first[-1],len(tail),tail.endswith('\n'),end)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(x => x.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(preserveNewlines ? "10000 a 11002 True TAIL\n" : "10000 a 11001 True TAIL\n", result.StandardOutput);
        }
        Assert.True(host.RangedReadCount > 1);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
