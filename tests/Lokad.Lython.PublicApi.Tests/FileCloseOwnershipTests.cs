using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FileCloseOwnershipTests
{
    [Theory]
    [InlineData(false, "binary", true)]
    [InlineData(true, "binary", true)]
    [InlineData(false, "utf-8", true)]
    [InlineData(true, "utf-8", true)]
    [InlineData(false, "utf-16", true)]
    [InlineData(true, "utf-16", true)]
    [InlineData(false, "binary", false)]
    [InlineData(true, "binary", false)]
    [InlineData(false, "utf-8", false)]
    [InlineData(true, "utf-8", false)]
    public async Task FailedCloseRetiresWriterWithoutReplayingPublication(bool asynchronous, string encoding, bool explicitClose)
    {
        var binary = encoding == "binary";
        var source = "f=open('target','wb')\nf.write(b'later')\n";
        if (!binary) source = "f=open('target','w',encoding='" + encoding + "')\nf.write('later')\n";
        source += "print('ready\\n',end='')\n";
        if (explicitClose) source += """
            try:f.close()
            except Exception:print(f.closed)
            f.close()
            try:f.flush()
            except ValueError:print('closed')
            """;
        source += "\nreturn 3";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost(); host.SeedFile("/target", "before");
            var armed = false;
            host.OnStandardOutputWrite = () =>
            {
                if (armed) return;
                armed = true;
                if (encoding == "utf-8") host.FailNextWriteText("/target", "blocked");
                else host.FailNextWriteBytes("/target", "blocked");
            };
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.Equal(explicitClose, result.Success);
                Assert.Equal(explicitClose ? "ready\nTrue\nclosed\n" : "ready\n", result.StandardOutput);
                if (!explicitClose) Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
                Assert.True(armed);
                Assert.Empty(host.ReadBytes("/target"));
                if (explicitClose) Assert.Equal("3", result.ReturnValue?.ToString());
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
