using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Zip;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R36: staged entries are counted, copied under reservation, and released together
/// with their references only once publication succeeds; failed publishes keep both
/// for a balanced retry. STORED reads reserve before copying, and extraction plans
/// are counted like any other growing collection.
/// </summary>
public sealed class ZipStagedAccountingTests
{
    [Fact]
    public async Task ManyEmptyMembersEnforceCollectionLimit()
    {
        const string script = "import zipfile\nz = zipfile.ZipFile(\"/t.zip\", \"w\")\nfor i in range(200):\n    z.writestr(\"f\" + str(i), b\"\")\nz.close()\nreturn 1\n";
        var options = new LythonRunOptions { MaxCollectionSize = 10 };
        var sync = new LythonEngine().Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await new LythonEngine().RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClosedArchivesReleaseStagedCharges()
    {
        // Successful publication releases staged charges and drops the references,
        // so retaining closed handles cannot accumulate uncharged storage.
        const string script = "import zipfile\npayload = bytes(32768)\nhandles = []\nfor i in range(20):\n    z = zipfile.ZipFile(\"/a\" + str(i) + \".zip\", \"w\")\n    z.writestr(\"x\", payload)\n    z.close()\n    handles.append(z)\nwith zipfile.ZipFile(\"/a0.zip\") as archive:\n    return [len(handles), archive.read(\"x\") == payload]\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = new LythonEngine().Run(script, new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(20), true }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await new LythonEngine().RunAsync(script, new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(20), true }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public void FailedPublishKeepsStagedChargesForRetry()
    {
        const string script = "import zipfile\nz = zipfile.ZipFile(\"/out.zip\", \"w\")\nz.writestr(\"a\", bytes(60000))\ntry:\n    z.close()\nexcept RuntimeError:\n    seen = True\nz.writestr(\"b\", bytes(60000))\nz.close()\nreturn seen\n";
        var host = new MockLythonHost();
        host.FailNextWriteBytes("/out.zip", "disk is full");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 100000 };
        var result = new LythonEngine().Run(script, host, options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoredReadRespectsMemoryBudget()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"x\", bytes(32768), zipfile.ZIP_STORED)\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return len(archive.read(\"x\"))\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2048 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        var sync = new LythonEngine().Run(script, syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ExtractAllInfosBranchEnforcesCollectionLimit()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    for i in range(20):\n        archive.writestr(\"f\" + str(i), b\"\")\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive.extractall(\"/out\")\nreturn 1\n";
        var options = new LythonRunOptions { MaxCollectionSize = 10 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        var sync = new LythonEngine().Run(script, syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeflatedReadRespectsMemoryBudget()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"x\", bytes(32768), zipfile.ZIP_DEFLATED)\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return len(archive.read(\"x\"))\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2048 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        var sync = new LythonEngine().Run(script, syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    private static byte[] ManyMemberArchive(int members)
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine().Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    for i in range(" + members + "):\n        archive.writestr(\"f\" + str(i), b\"\")\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        return seed.ReadBytes("/t.zip");
    }

    private static int FindEndRecord(byte[] archive)
    {
        for (var i = archive.Length - 22; i >= 0; i--)
        {
            if (archive[i] == 0x50 && archive[i + 1] == 0x4B && archive[i + 2] == 0x05 && archive[i + 3] == 0x06)
            {
                return i;
            }
        }

        throw new InvalidOperationException("End record not found.");
    }

    private static LythonRuntime.ExecutionContext NewContext(MockLythonHost host)
    {
        return new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
    }

    [Fact]
    public void FailedDirectoryParseReleasesMetadataCharges()
    {
        var archive = ManyMemberArchive(200);
        var end = FindEndRecord(archive);
        archive[end + 12]++;
        var host = new MockLythonHost();
        var context = NewContext(host);
        var beforeCommitted = context.MemoryGovernor.CurrentCommittedBytes;
        var beforeReserved = context.MemoryGovernor.CurrentReservedBytes;
        Assert.Throws<InvalidDataException>(() => ZipDirectoryReader.Read(archive, false, context, new LythonSourceSpan(0, 0, 0, 0)));
        Assert.Equal(beforeCommitted, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(beforeReserved, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ParsedDirectoryRetainsExactlyItsCharge()
    {
        var archive = ManyMemberArchive(200);
        var host = new MockLythonHost();
        var context = NewContext(host);
        var beforeCommitted = context.MemoryGovernor.CurrentCommittedBytes;
        var directory = ZipDirectoryReader.Read(archive, false, context, new LythonSourceSpan(0, 0, 0, 0));
        Assert.Equal(200, directory.Entries.Count);
        Assert.True(directory.MetadataCharge > 0, "expected a positive metadata charge");
        Assert.Equal(beforeCommitted + directory.MetadataCharge, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.Release(directory.MetadataCharge);
        Assert.Equal(beforeCommitted, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task RepeatedRecognitionStaysCharged()
    {
        const string script = "import zipfile\nreturn [zipfile.is_zipfile(\"/t.zip\") for _ in range(30)]\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", ManyMemberArchive(200));
        var sync = new LythonEngine().Run(script, syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(30, Assert.IsType<List<object?>>(sync.ReturnValue).Count);

        var asyncHost = new DelayedLythonHost("/");
        asyncHost.SeedBytes("/t.zip", ManyMemberArchive(200));
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(30, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }

    [Fact]
    public async Task FailedOpenThenRecognize()
    {
        var archive = ManyMemberArchive(200);
        var end = FindEndRecord(archive);
        archive[end + 12]++;
        const string script = "import zipfile\ndef probe():\n    try:\n        archive = zipfile.ZipFile(\"/t.zip\")\n        archive.close()\n    except zipfile.BadZipFile:\n        return True\n    return False\nbad = 0\nfor _ in range(10):\n    if probe():\n        bad = bad + 1\nreturn [bad, zipfile.is_zipfile(\"/t.zip\")]\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 153600 };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        var sync = new LythonEngine().Run(script, syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(10), false }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncHost = new DelayedLythonHost("/");
        asyncHost.SeedBytes("/t.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(10), false }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedClosedWritersStayCharged()
    {
        // R36 public repro: 20 closed DEFLATED writers holding 32KiB each
        // cannot hide 640KiB of staged storage inside a 512KiB budget.
        const string script = "import zipfile\npayload = bytes(32768)\nhandles = []\nwith zipfile.ZipFile(\"/out.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    for i in range(20):\n        handle = archive.open(\"x\" + str(i), \"w\")\n        handle.write(payload)\n        handle.close()\n        handles.append(handle)\nreturn len(handles)\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = new LythonEngine().Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await new LythonEngine().RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }


    [Fact]
    public async Task ManyTinyMembersEnforceMemoryBudget()
    {
        // R36: thousands of individually tiny members still accumulate
        // staged storage against the memory budget.
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    for i in range(2000):\n        archive.writestr(\"f\" + str(i), b\"\")\nreturn 1\n";
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = new LythonEngine().Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await new LythonEngine().RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }


    [Fact]
    public async Task CancelledStagedWriteFailsExplicitly()
    {
        // R36: a cancelled staged write honors cancellation instead of
        // running to completion or failing with an unrelated error.
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/out.zip\", \"w\") as archive:\n    archive.writestr(\"a\", bytes(1000))\nreturn 1\n";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };
        var sync = new LythonEngine().Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("execution canceled", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await new LythonEngine().RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

}
