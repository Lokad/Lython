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
        var result = new LythonEngine().Run(
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
        var result = new LythonEngine().Run(
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
        var engine = new LythonEngine();
        var first = engine.Run(
            """
import copy
import hashlib
copy.dispatch_table["review_marker"] = 123
hashlib.algorithms_available.clear()
return copy.dispatch_table.get("review_marker", "absent")
""",
            new MockLythonHost());
        Assert.True(first.Success, Describe(first));

        var second = engine.Run(
            """
import copy
import hashlib
return [copy.dispatch_table.get("review_marker", "absent"), len(hashlib.algorithms_available) > 0]
""",
            new MockLythonHost());
        Assert.True(second.Success, Describe(second));
        var list = Assert.IsType<List<object?>>(second.ReturnValue);
        Assert.Equal("absent", list[0]);
        Assert.Equal(true, list[1]);
    }

    [Fact]
    public void ByteLimitsRejectNegativeValuesBeforeNarrowing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine().Run(
                "return 42",
                new MockLythonHost(),
                new LythonRunOptions { MaxHostReadBytes = -4294967296 }));
        var ok = new LythonEngine().Run(
            "return 42",
            new MockLythonHost(),
            new LythonRunOptions { MaxHostReadBytes = 0 });
        Assert.True(ok.Success, Describe(ok));
    }

    [Fact]
    public void ContextManagerExitIsInvokedExactlyOnceOnNormalExitFailure()
    {
        var result = new LythonEngine().Run(
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
        var result = await new LythonEngine().RunAsync(
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

