using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Bounded stress and deterministic fuzz inputs for the ZIP surface: every
/// case must finish quickly with an explicit outcome. No test may hang,
/// exhaust the run budget opaquely, or leak a CLR exception.
/// </summary>
public sealed class ZipStressTests
{
    private static string FindCasesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "zipfile", "cases");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("ZIP fixture cases not found.");
    }

    [Fact]
    public void ManyTinyEntriesStayOrdered()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    for i in range(2000):
        archive.writestr("f" + str(i), b"x")
with zipfile.ZipFile("/out.zip") as archive:
    names = archive.namelist()
    return [len(names), names[0], names[1999], archive.read("f1999")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(2000), values[0]);
        Assert.Equal("f0", values[1]);
        Assert.Equal("f1999", values[2]);
        Assert.Equal(new byte[] { (byte)'x' }, Assert.IsType<byte[]>(values[3]));
    }

    [Fact]
    public void ManyEmptyDuplicatesCollapseToLast()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    for i in range(500):
        archive.writestr("dup", b"")
with zipfile.ZipFile("/out.zip") as archive:
    return [len(archive.namelist()), archive.read("dup"), archive.testzip() is None]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(500), values[0]);
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(true, values[2]);
    }

    [Fact]
    public void BigBoundedMemberRoundTrips()
    {
        var host = new MockLythonHost();
        host.SeedWorkbook("/big.bin", new byte[2_000_000]);
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", strict_timestamps=False) as archive:
    archive.write("/big.bin", "big.bin")
with zipfile.ZipFile("/out.zip") as archive:
    return len(archive.read("big.bin"))
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(2_000_000), result.ReturnValue);
    }

    [Fact]
    public void DeclaredHugeSizesSerializeActualLengths()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    info = zipfile.ZipInfo("tiny.txt")
    info.file_size = 1099511627776
    archive.writestr(info, b"tiny")
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.read("tiny.txt"), archive.getinfo("tiny.txt").file_size]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("tiny"), Assert.IsType<byte[]>(values[0]));
        Assert.Equal(new BigInteger(4), values[1]);
    }

    [Fact]
    public void TightMemoryBudgetFailsStagingExplicitly()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("big.txt", "Z" * 5000)
return 1
""",
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 1024L });
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("budget", result.Failure?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedArchivesFailBounded()
    {
        var full = File.ReadAllBytes(Path.Combine(FindCasesRoot(), "zip-deflated", "input.zip"));
        var step = Math.Max(1, full.Length / 40);
        for (var cut = 0; cut <= full.Length; cut += step)
        {
            var host = new MockLythonHost();
            host.SeedWorkbook("/t.zip", full[..cut]);
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive.testzip()\n",
                host);
            if (result.Success)
            {
                continue;
            }

            Assert.True(
                result.Failure?.ExceptionType == "BadZipFile",
                $"cut {cut}: {result.Failure?.ExceptionType}: {result.Failure?.Message}");
        }
    }

    [Fact]
    public void MutatedArchivesNeverEscapeExplicitCategories()
    {
        var full = File.ReadAllBytes(Path.Combine(FindCasesRoot(), "zip-deflated", "input.zip"));
        for (var sample = 0; sample < 24; sample++)
        {
            var mutated = (byte[])full.Clone();
            mutated[(sample * 97 + 7) % mutated.Length] ^= 0xFF;
            var host = new MockLythonHost();
            host.SeedWorkbook("/t.zip", mutated);
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive.testzip()\n",
                host);
            if (result.Success)
            {
                continue;
            }

            Assert.True(
                result.Failure?.ExceptionType == "BadZipFile",
                $"sample {sample}: {result.Failure?.ExceptionType}: {result.Failure?.Message}");
        }
    }

    [Fact]
    public void RepeatedOpenCloseCyclesStayStable()
    {
        var host = new MockLythonHost();
        for (var i = 0; i < 200; i++)
        {
            var result = new LythonEngine().Run(
                """
import zipfile
with zipfile.ZipFile("/c.zip", "w") as archive:
    archive.writestr("a", b"x")
return 1
""",
                host);
            Assert.True(result.Success, $"cycle {i}: {result.Failure?.Message}");
        }

        var reread = new LythonEngine().Run(
            "import zipfile\nwith zipfile.ZipFile(\"/c.zip\") as archive:\n    return archive.read(\"a\")\n",
            host);
        Assert.True(reread.Success, reread.Failure?.Message);
        Assert.Equal(new byte[] { (byte)'x' }, Assert.IsType<byte[]>(reread.ReturnValue));
    }

    [Fact]
    public void DeepNestedExtractionStaysContained()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    archive.writestr("d/" * 100 + "f.txt", b"deep")
with zipfile.ZipFile("/t.zip") as archive:
    return archive.extract("d/" * 100 + "f.txt", "/out")
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var deep = "/out/" + string.Concat(Enumerable.Repeat("d/", 100)) + "f.txt";
        Assert.Equal("/out/" + string.Concat(Enumerable.Repeat("d/", 100)) + "f.txt", result.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("deep"), host.ReadBytes(deep));
        Assert.False(host.Exists("/f.txt"));
    }

    [Fact]
    public async Task CancelledScanFailsExplicitly()
    {
        var host = new DelayedLythonHost("/");
        host.SeedBytes("/t.zip", File.ReadAllBytes(Path.Combine(FindCasesRoot(), "zip-deflated", "input.zip")));
        using var cancellation = new CancellationTokenSource();
        var task = new LythonEngine().RunAsync(
            "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return archive.testzip()\n",
            host,
            cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var result = await task;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }
}
