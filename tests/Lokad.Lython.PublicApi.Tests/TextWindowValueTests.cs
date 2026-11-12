using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextWindowValueTests
{
    [Theory]
    [InlineData(false, "utf-8")]
    [InlineData(true, "utf-8")]
    [InlineData(false, "utf-16-le")]
    [InlineData(true, "utf-16-le")]
    public async Task RetainedLinesKeepStringSemanticsAfterRefillAndClose(bool asynchronous, string encoding)
    {
        const string source = """
            import json,re
            with open('input', encoding=encoding, newline='', buffering=4096) as stream:
                assert stream.read(1) == '!'
                first = stream.readline()
                for line in stream:
                    last = line
            assert last == 'end\n'
            assert type(first) is str
            assert first == expected and len(first) == len(expected)
            assert hash(first) == hash(expected)
            assert first[1:-1] == expected[1:-1]
            assert first[::-1] == expected[::-1]
            assert first.encode() == expected.encode()
            assert first.replace('x', 'y') == expected.replace('x', 'y')
            assert json.loads(json.dumps(first)) == first
            match = re.search('😀', first)
            assert match.span() == (4,5) and match.string is first
            return first
            """;
        var expected = "café😀-" + new string('x', 31) + "\r\n";
        var input = "!" + expected + new string('z', 10000) + "\r\nend\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var bytes = (encoding == "utf-8" ? Encoding.UTF8 : Encoding.Unicode).GetBytes(input);
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var immediate = new MockLythonHost();
        if (delayed is not null) delayed.SeedBytes("/input", bytes);
        else immediate.SeedBytes("/input", bytes);
        ILythonHost host = delayed is null ? immediate : delayed;
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { Globals = new Dictionary<string, object?> { ["encoding"] = encoding, ["expected"] = expected } })
            : script.Run(host, new LythonRunOptions { Globals = new Dictionary<string, object?> { ["encoding"] = encoding, ["expected"] = expected } }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
