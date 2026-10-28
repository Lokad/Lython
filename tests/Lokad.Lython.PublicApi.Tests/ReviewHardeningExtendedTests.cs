using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardeningExtendedTests
{
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    [Fact]
    public void GzipReadSupportsBoolIndexNegativeZeroAndEof()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import gzip
with gzip.open("/repo/review.gz", "wb") as writer:
    writer.write(b"abcdef")
class Two:
    def __index__(self):
        return 2
with gzip.open("/repo/review.gz", "rb") as reader:
    a = reader.read(True)
    b = reader.read(Two())
    c = reader.read(0)
    try: reader.read(-5)
    except ValueError: pass
    d = reader.read(-1)
    e = reader.read(10)
    f = reader.read(1)
    return [a, b, c, d, e, f]
""",
            host);
        Assert.True(result.Success, Describe(result));
        var list = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new byte[] { (byte)97 }, Assert.IsType<byte[]>(list[0]));
        Assert.Equal(new byte[] { (byte)98, (byte)99 }, Assert.IsType<byte[]>(list[1]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(list[2]));
        Assert.Equal(new byte[] { (byte)100, (byte)101, (byte)102 }, Assert.IsType<byte[]>(list[3]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(list[4]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(list[5]));
    }

    [Fact]
    public void GzipTextHugeSizeReturnsRemainder()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import gzip
with gzip.open("/repo/text.gz", "wt", encoding="utf-8") as writer:
    writer.write("abc\n")
with gzip.open("/repo/text.gz", "rt", encoding="utf-8") as reader:
    reader.read(1)
    rest = reader.read(2147483647)
    return rest
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("bc\n", result.ReturnValue);
    }

    [Fact]
    public async Task ModuleIsolationHoldsConcurrentlyAndForCompiledScript()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        using var script = engine.Compile("import copy\ncopy.__spec__.origin = \"marker\"\nreturn copy.__spec__.origin");
        Assert.True(script.IsValid);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            using var r = script.Run(new MockLythonHost());
            Assert.True(r.Success, Describe(r));
            Assert.Equal("marker", r.ReturnValue);
            using var fresh = engine.Run("import copy\nreturn copy.__spec__.origin", new MockLythonHost());
            Assert.True(fresh.Success, Describe(fresh));
            Assert.Equal("built-in", fresh.ReturnValue);
        })).ToArray();
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void ContextManagerRunsOnceForReturnBreakContinueAndNested()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
calls = []
class Manager:
    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc, tb):
        calls.append("exit")
        return False
def with_return():
    with Manager():
        return "done"
def with_break():
    for i in range(3):
        with Manager():
            break
def with_nested():
    with Manager():
        with Manager():
            pass
with_return()
with_break()
with_nested()
return len(calls)
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new BigInteger(4), result.ReturnValue);
    }
}

