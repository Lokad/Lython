using System;
using System.IO;
using System.Reflection;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Zip;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R36: staged entries are counted, copied under reservation, and released together
/// with their references only once publication succeeds; failed-close storage stays
/// funded while retained by the closed handle. STORED reads reserve before copying, and extraction plans
/// are counted like any other growing collection.
/// </summary>
public sealed class ZipStagedAccountingTests
{

    [Fact]
    public async Task ClosedArchivesKeepPublicationFunded()
    {
        // Closed handles preserve behavior while conservative publication
        // storage remains funded by the bounded execution owner.
        const string script = "import zipfile\npayload = bytes(32768)\nhandles = []\nfor i in range(2):\n    z = zipfile.ZipFile(\"/a\" + str(i) + \".zip\", \"w\")\n    z.writestr(\"x\", payload)\n    z.close()\n    handles.append(z)\nwith zipfile.ZipFile(\"/a0.zip\") as archive:\n    return [len(handles), archive.read(\"x\") == payload]\n";
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(524288) };
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), true }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), true }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public void StagedPayloadAdmissionFailsBeforePublication()
    {
        const string source = "import zipfile\nz=zipfile.ZipFile('/out.zip','w')\nz.writestr('a',bytes(60000))\nz.close()";
        var host = new MockLythonHost();
        var writes = 0;
        host.OnFileWrite = () => writes++;
        var budget = new LythonMemoryBudget(100000);
        using var script = new LythonEngine().Compile(source);
        using (var result = script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(0, writes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task StoredReadRespectsMemoryBudget()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine(Utf8RegexProvider.Instance).Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"x\", bytes(32768), zipfile.ZIP_STORED)\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return len(archive.read(\"x\"))\n";
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(2048) };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        using var compiledAdmission1 = new LythonEngine(Utf8RegexProvider.Instance).Compile(script);
using         var sync = compiledAdmission1.Run(syncHost,options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.zip", archive);
        using var compiledAdmission2 = new LythonEngine(Utf8RegexProvider.Instance).Compile(script);
using         var asyncResult = await compiledAdmission2.RunAsync(asyncHost,options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DeflatedReadRespectsMemoryBudget()
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine(Utf8RegexProvider.Instance).Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"x\", bytes(32768), zipfile.ZIP_DEFLATED)\nreturn 1\n", seed);
        Assert.True(built.Success, built.Failure?.Message);
        var archive = seed.ReadBytes("/t.zip");

        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return len(archive.read(\"x\"))\n";
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(2048) };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        using var compiledAdmission3 = new LythonEngine(Utf8RegexProvider.Instance).Compile(script);
using         var sync = compiledAdmission3.Run(syncHost,options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/t.zip", archive);
        using var compiledAdmission4 = new LythonEngine(Utf8RegexProvider.Instance).Compile(script);
using         var asyncResult = await compiledAdmission4.RunAsync(asyncHost,options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    private static byte[] ManyMemberArchive(int members)
    {
        var seed = new MockLythonHost();
        var built = new LythonEngine(Utf8RegexProvider.Instance).Run("import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    for i in range(" + members + "):\n        archive.writestr(\"f\" + str(i), b\"\")\nreturn 1\n", seed);
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
        return new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
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
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", ManyMemberArchive(200));
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(30, Assert.IsType<List<object?>>(sync.ReturnValue).Count);

        var asyncHost = new DelayedLythonHost("/");
        asyncHost.SeedBytes("/t.zip", ManyMemberArchive(200));
        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, asyncHost, options);
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
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(153600) };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/t.zip", archive);
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(10), false }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncHost = new DelayedLythonHost("/");
        asyncHost.SeedBytes("/t.zip", archive);
        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, asyncHost, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(10), false }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedClosedWritersStayCharged()
    {
        // R36 public repro: 20 closed DEFLATED writers holding 32KiB each
        // cannot hide 640KiB of staged storage inside a 512KiB budget.
        const string script = "import zipfile\npayload = bytes(32768)\nhandles = []\nwith zipfile.ZipFile(\"/out.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    for i in range(20):\n        handle = archive.open(\"x\" + str(i), \"w\")\n        handle.write(payload)\n        handle.close()\n        handles.append(handle)\nreturn len(handles)\n";
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(524288) };
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }


    [Fact]
    public async Task ManyTinyMembersEnforceMemoryBudget()
    {
        // R36: thousands of individually tiny members still accumulate
        // staged storage against the memory budget.
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    for i in range(2000):\n        archive.writestr(\"f\" + str(i), b\"\")\nreturn 1\n";
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, new MockLythonHost(), options);
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
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(script, new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("execution canceled", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(script, new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }


    private static object InvokeSerializeArchive(byte[] data, LythonRuntime.ExecutionContext context)
    {
        var writerType = typeof(LythonRuntime).Assembly.GetType("Lokad.Lython.Runtime.Zip.ZipRecordWriter")
            ?? throw new InvalidOperationException("ZipRecordWriter not found.");
        var entryType = writerType.GetNestedType("StagedEntry", BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("StagedEntry not found.");
        var ctor = Assert.Single(entryType.GetConstructors());
        var entry = ctor.Invoke(["a", new byte[] { 97 }, (ushort)0, (ushort)0, (ushort)0, (ushort)8, 6, Array.Empty<byte>(), Array.Empty<byte>(), 0, 0u, data, null, false]);
        var staged = Array.CreateInstance(entryType, 1);
        staged.SetValue(entry, 0);
        var method = writerType.GetMethod("SerializeArchive", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("SerializeArchive not found.");
        return method.Invoke(null, [staged, Array.Empty<byte>(), true, context, null])
            ?? throw new InvalidOperationException("SerializeArchive returned null.");
    }

    [Fact]
    public void DeflatedSerializeTransientsStayCharged()
    {
        // R36: one incompressible DEFLATED entry holds staged, compressed and
        // builder copies at once; the serializer transients stay charged, so a
        // budget between one and two payloads fails instead of succeeding.
        // Measured old peak is about 600KB accounted for a 200KB payload.
        var data = new byte[200000];
        new Random(42).NextBytes(data);
        var host = new MockLythonHost();
        var budget = new LythonMemoryBudget(700000);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var failure = Assert.Throws<TargetInvocationException>(() => InvokeSerializeArchive(data, context));
        Assert.Equal("LythonRuntimeException", failure.InnerException?.GetType().Name);
        Assert.Contains("memory budget exceeded", failure.InnerException?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


}
