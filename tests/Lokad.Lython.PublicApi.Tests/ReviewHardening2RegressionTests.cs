using System.IO.Compression;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening2RegressionTests
{
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    [Fact]
    public void CustomWriteTextMethodCompilesAndRuns()
    {
        const string Source = """
class Writer:
    def write_text(self, value):
        return len(value)
print(Writer().write_text(b"abc"))
""";
        var compiled = new LythonEngine().Compile(Source);
        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "LA3046");
        var result = new LythonEngine().Run(Source, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("3\n", result.StandardOutput);
    }

    [Fact]
    public void KnownPathWriteTextBytesStillDiagnosed()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
Path("/repo/out.txt").write_text(b"abc")
""");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3046");
    }

    private const string IterableRepro = """
class C:
    def __iter__(self):
        return iter([2, 1])
v = C()
result = [list(v), sorted(v), min(v)]
def f(*args):
    return list(args)
result.append(f(*v))
return result
""";

    [Fact]
    public void CustomIterationParityAcrossSyncApis()
    {
        var result = new LythonEngine().Run(IterableRepro, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        var outer = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(4, outer.Count);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, Assert.IsType<List<object?>>(outer[0]));
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(2) }, Assert.IsType<List<object?>>(outer[1]));
        Assert.Equal(new BigInteger(1), outer[2]);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, Assert.IsType<List<object?>>(outer[3]));
    }

    [Fact]
    public async Task CustomIterationParityAcrossAsyncApis()
    {
        var result = await new LythonEngine().RunAsync(IterableRepro, new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        var outer = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(4, outer.Count);
        Assert.Equal(new BigInteger(1), outer[2]);
    }

    [Fact]
    public void CustomIterationEffectsAndErrorsSync()
    {
        var result = new LythonEngine().Run(
            """
calls = []
class C:
    def __iter__(self):
        calls.append("iter")
        return iter([2, 1])
v = C()
first = sorted(v)
second = list(v)
return [calls, first, second]
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        var outer = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(2, Assert.IsType<List<object?>>(outer[0]).Count);

        var bad = new LythonEngine().Run(
            """
class D:
    pass
errors = []
for probe in [lambda: list(D()), lambda: sorted(D()), lambda: min(D())]:
    try:
        probe()
    except TypeError as ex:
        errors.append(ex.type)
return errors
""",
            new MockLythonHost());
        Assert.True(bad.Success, Describe(bad));
        Assert.Equal(3, Assert.IsType<List<object?>>(bad.ReturnValue).Count);
    }

    [Fact]
    public async Task CustomIterationLoopsAndDisplaysAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
class C:
    def __iter__(self):
        return iter([2, 1])
v = C()
total = 0
for x in v:
    total = total + x
comp = [x * 10 for x in v]
unpacked = [*v]
return [total, comp, unpacked, list(v), tuple(v), sorted(v)]
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        var outer = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(3), outer[0]);
    }

    [Fact]
    public void SavedEntriesCarryHostClock()
    {
        var host = new MockLythonHost();
        var stamp = new DateTimeOffset(2024, 5, 6, 7, 8, 10, TimeSpan.Zero);
        host.LocalNow = stamp;
        host.UtcNow = stamp;
        var result = new LythonEngine().Run(
            """
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
ws["A1"] = "sku"
wb.save("/out.xlsx")
loaded = openpyxl.load_workbook("/out.xlsx")
loaded.save("/resaved.xlsx")
return loaded["Sheet"]["A1"].value
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("sku", result.ReturnValue);
        foreach (var path in new[] { "/out.xlsx", "/resaved.xlsx" })
        {
            using var archive = new ZipArchive(new MemoryStream(host.ReadBytes(path)), ZipArchiveMode.Read);
            Assert.NotEmpty(archive.Entries);
            foreach (var entry in archive.Entries)
            {
                Assert.Equal(stamp.DateTime, entry.LastWriteTime.DateTime);
            }
        }
    }

    [Fact]
    public void UnrelatedBodyErrorStillPublishesValidGzipWrites()
    {
        var host = new MockLythonHost("/repo");
        var failed = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "wb") as writer:
    writer.write(b"abc")
    raise ValueError("unrelated")
""",
            host);
        Assert.False(failed.Success);
        Assert.Equal("ValueError", failed.Failure?.ExceptionType);
        var reread = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "rb") as reader:
    return reader.read()
""",
            host);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(new byte[] { (byte)97, (byte)98, (byte)99 }, Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public async Task UnrelatedBodyErrorStillPublishesValidGzipWritesAsync()
    {
        var host = new MockLythonHost("/repo");
        var failed = await new LythonEngine().RunAsync(
            """
import gzip
with gzip.open("/repo/out.gz", "wb") as writer:
    writer.write(b"abc")
    raise ValueError("unrelated")
""",
            host);
        Assert.False(failed.Success);
        Assert.Equal("ValueError", failed.Failure?.ExceptionType);
        var reread = await new LythonEngine().RunAsync(
            """
import gzip
with gzip.open("/repo/out.gz", "rb") as reader:
    return reader.read()
""",
            host);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(new byte[] { (byte)97, (byte)98, (byte)99 }, Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public void ValidationFailureStillDiscardsNewGzipFile()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/new.gz", "wt", encoding="latin-1") as writer:
    writer.write("€")
""",
            host);
        Assert.False(result.Success);
        Assert.Equal("UnicodeEncodeError", result.Failure?.ExceptionType);
        Assert.False(host.Exists("/repo/new.gz"));
    }

    [Fact]
    public void ExplicitClosePublishesStagedWrites()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
writer.close()
return writer.closed
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal(true, result.ReturnValue);
        var reread = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "rb") as reader:
    return reader.read()
""",
            host);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(new byte[] { (byte)97, (byte)98, (byte)99 }, Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public void RepeatedCloseIsHarmless()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
writer.close()
writer.close()
return writer.closed
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal(true, result.ReturnValue);
        var reread = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "rb") as reader:
    return reader.read()
""",
            host);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(new byte[] { (byte)97, (byte)98, (byte)99 }, Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public void FailedCloseLeavesRetryableHandle()
    {
        var host = new MockLythonHost("/repo");
        host.FailNextWriteBytes("/repo/out.gz", "disk is full");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
try:
    writer.close()
except RuntimeError:
    seen = True
else:
    seen = False
writer.write(b"def")
writer.close()
return [seen, writer.closed]
""",
            host);
        // The failed close raised the redacted host error while leaving the
        // handle open with its staged writes; the retry published everything.
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new List<object?> { true, true }, Assert.IsType<List<object?>>(result.ReturnValue));
        var reread = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "rb") as reader:
    return reader.read()
""",
            host);
        Assert.True(reread.Success, Describe(reread));
        Assert.Equal(
            new byte[] { (byte)97, (byte)98, (byte)99, (byte)100, (byte)101, (byte)102 },
            Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public async Task FailedCloseLeavesRetryableHandleAsync()
    {
        var host = new MockLythonHost("/repo");
        host.FailWriteBytes("/repo/out.gz", "disk is full");
        var result = await new LythonEngine().RunAsync(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
writer.close()
return writer.closed
""",
            host);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("Host write_bytes failed.", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.False(host.Exists("/repo/out.gz"));
    }

    [Fact]
    public void BudgetFailureDuringFinalizationLeavesHandleOpen()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
try:
    writer.close()
except RuntimeError:
    outcome = "limited"
writer.write(b"def")
return [outcome, writer.closed]
""",
            host,
            new LythonRunOptions { MaxHostCalls = 0 });
        // Publication is the first host call, so finalization fails before
        // anything is written; the handle stays open with its staged writes.
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new List<object?> { "limited", false }, Assert.IsType<List<object?>>(result.ReturnValue));
        Assert.False(host.Exists("/repo/out.gz"));
    }

    [Fact]
    public void CloseAfterValidationFailureDiscardsStagedOutput()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/new.gz", "wb")
writer.write(b"abc")
try:
    writer.write("boom")
except TypeError:
    pass
writer.close()
return writer.closed
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal(true, result.ReturnValue);
        Assert.False(host.Exists("/repo/new.gz"));
    }

    [Fact]
    public void UseAfterCloseRaisesValueError()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
writer = gzip.open("/repo/out.gz", "wb")
writer.write(b"abc")
writer.close()
try:
    writer.write(b"late")
except ValueError:
    return "closed-ok"
return "still-open"
""",
            host);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("closed-ok", result.ReturnValue);
    }
}

