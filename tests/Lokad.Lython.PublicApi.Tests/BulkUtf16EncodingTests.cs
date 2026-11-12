using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BulkUtf16EncodingTests
{
    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-16-le")]
    [InlineData("utf-16-be")]
    public async Task ScalarBoundariesNewlinesAndRepeatedWritesKeepExactBytesAndCounts(string encoding)
    {
        const string source = """
            encoded = value.encode(codec)
            with open('/output', 'w', encoding=codec, newline=line_ending) as target:
                first = target.write(value)
                second = target.write(value)
                position = target.tell()
            print(first, second, position)
            return encoded
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var codec = encoding == "utf-16-be" ? Encoding.BigEndianUnicode : Encoding.Unicode;
            var preamble = encoding == "utf-16" ? codec.GetPreamble() : [];
            foreach (var newline in new[] { "", "\n", "\r", "\r\n" })
            foreach (var leading in new[] { 1021, 1022, 1023, 1024 })
            {
                var text = new string('A', leading) + "é😀\nΩ\r\nend";
                var count = text.EnumerateRunes().Count();
                var translated = newline.Length == 0 ? text : text.Replace("\n", newline, StringComparison.Ordinal);
                byte[] expectedFile = [.. preamble, .. codec.GetBytes(translated + translated)];
                byte[] expectedEncoded = [.. preamble, .. codec.GetBytes(text)];
                var options = new LythonRunOptions
                {
                    MemoryBudget = budget,
                    Globals = new Dictionary<string, object?>
                    { ["value"] = text, ["codec"] = encoding, ["line_ending"] = newline },
                };
                var firstHost = new MockLythonHost();
                using (var first = script.Run(firstHost, options))
                {
                    Check(first, firstHost);
                    var retained = budget.ReservedBytes;
                    Assert.True(retained > baseline);
                    var secondHost = new MockLythonHost();
                    using (var second = await script.RunAsync(secondHost, options))
                    {
                        Check(second, secondHost);
                        Assert.True(budget.ReservedBytes > retained);
                    }
                    Assert.Equal(retained, budget.ReservedBytes);
                    Assert.Equal(expectedEncoded, Assert.IsType<byte[]>(first.ReturnValue));
                }
                Assert.Equal(baseline, budget.ReservedBytes);

                void Check(LythonExecutionResult result, MockLythonHost host)
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal($"{count} {count} {expectedFile.Length}\n", result.StandardOutput);
                    Assert.Equal(expectedEncoded, Assert.IsType<byte[]>(result.ReturnValue));
                    Assert.Equal(expectedFile, host.ReadBytes("/output"));
                }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
