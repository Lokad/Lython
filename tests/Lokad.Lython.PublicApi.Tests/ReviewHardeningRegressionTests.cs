using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardeningRegressionTests
{
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    [Fact]
    public void GzipHugeReadSizesReturnRemainderWithoutEscaping()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import gzip
with gzip.open("/repo/review.gz", "wb") as writer:
    writer.write(b"abc\n")
with gzip.open("/repo/review.gz", "rb") as reader:
    first = reader.read(1)
    rest = reader.read(2147483647)
    eof = reader.read(2147483647)
    return [first, rest, eof]
""",
            host);

        Assert.True(result.Success, Describe(result));
        var list = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new byte[] { (byte)97 }, Assert.IsType<byte[]>(list[0]));
        Assert.Equal(new byte[] { (byte)98, (byte)99, (byte)10 }, Assert.IsType<byte[]>(list[1]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(list[2]));
    }

    [Fact]
    public void GzipClosedHandleRejectsWithReentry()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import gzip
with gzip.open("/repo/data.gz", "wb") as writer:
    writer.write(b"abc")
reader = gzip.open("/repo/data.gz", "rb")
reader.close()
results = []
try:
    with reader:
        pass
except ValueError:
    results.append("with")
try:
    reader.__enter__()
except ValueError:
    results.append("enter")
return "|".join(results)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("with|enter", result.ReturnValue);
    }

    [Fact]
    public void CopyAndHashlibStateIsIsolatedAcrossRuns()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        using var first = engine.Run(
            """
import copy
import hashlib
copy.__spec__.origin = "review_marker"
hashlib.algorithms_available.clear()
return copy.__spec__.origin
""",
            new MockLythonHost());
        Assert.True(first.Success, Describe(first));

        using var second = engine.Run(
            """
import copy
import hashlib
return [copy.__spec__.origin, len(hashlib.algorithms_available) > 0]
""",
            new MockLythonHost());
        Assert.True(second.Success, Describe(second));
        var list = Assert.IsType<List<object?>>(second.ReturnValue);
        Assert.Equal("built-in", list[0]);
        Assert.Equal(true, list[1]);
    }

    [Fact]
    public void ContextManagerExitIsInvokedExactlyOnceOnNormalExitFailure()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
calls = []
class Manager:
    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc, tb):
        calls.append("exit")
        raise ValueError("exit boom")
try:
    with Manager():
        pass
except ValueError:
    pass
return len(calls)
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new BigInteger(1), result.ReturnValue);
    }

    [Fact]
    public async Task ContextManagerExitIsInvokedExactlyOnceAsync()
    {
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
calls = []
class Manager:
    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc, tb):
        calls.append("exit")
        raise ValueError("exit boom")
try:
    with Manager():
        pass
except ValueError:
    pass
return len(calls)
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new BigInteger(1), result.ReturnValue);
    }
}

