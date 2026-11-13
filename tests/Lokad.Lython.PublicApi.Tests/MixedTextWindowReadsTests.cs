using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MixedTextWindowReadsTests
{
    [Theory]
    [InlineData(false, "utf-8"), InlineData(true, "utf-8")]
    [InlineData(false, "utf-16-le"), InlineData(true, "utf-16-le")]
    public async Task SizedAndUnboundedReadsKeepRetainedLinesFundedAcrossRefill(bool asynchronous, string encoding)
    {
        const string source = """
            with open('input',encoding=encoding,newline='',buffering=4096) as stream:
                assert stream.read(1)=='!'
                first=stream.readline()
                prefix=stream.readline(3)
                part=stream.read(2)
                tail=stream.readline()
                last=stream.readline()
                assert stream.readline()==''
            return first,prefix,part,tail,last
            """;
        var first = "first-café😀-" + new string('x', 20) + "\r\n";
        var second = "ABCDEFGHIJKLMN-é😀\r\n";
        var last = "last-" + new string('z', 20000) + "\r\n";
        var input = "!" + first + second + last;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var bytes = (encoding == "utf-8" ? Encoding.UTF8 : Encoding.Unicode).GetBytes(input);
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var immediate = new MockLythonHost();
            if (delayed is null) immediate.SeedBytes("/input", bytes);
            else delayed.SeedBytes("/input", bytes);
            ILythonHost host = delayed is null ? immediate : delayed;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["encoding"] = encoding } };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(new object?[] { first, "ABC", "DE", second[5..], last }, values);
                Assert.True(budget.ReservedBytes > baseline);
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
