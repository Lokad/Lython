using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG01: abandoned record sources stop contributing charges once reclaimed:
/// dropping 200 mid-scan sources releases their actual storage through the
/// shared checkpoint cadence. Live rows and the pool's retained backing stay
/// fully funded, and the surviving source keeps yielding correct rows.
/// </summary>
public sealed class CsvAbandonedReaderAccountingTests
{
    [Fact]
    public void AbandonedSourcesReclaimOnCadence()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var lines = BuildLines();
        var abandoned = AbandonSources(context, lines, span);
        var live = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(abandoned, source => Assert.False(source.TryGetTarget(out _)));
        var retainedRows = new List<PyList>();
        for (var k = 0; k < 300; k++)
        {
            Assert.True(live.TryMoveNext(out var row));
            Assert.Equal("r" + k, ((PyString)row[0]).AsString());
            retainedRows.Add(row);
        }

        // The abandoned population spans several bounded retained quanta.
        // Exercise normal checkpoints without allocating more guest storage;
        // one sweep need not reclaim the entire population. Ambient GC must
        // not decide whether the test meets an arbitrary total-byte threshold.
        for (var k = 0; k < 4096; k++)
        {
            context.CheckExecution(span);
        }

        var pool = context.State.CallTemporaries;
        var expected = pool.CommittedBackingBytes + live.OwnedBytes + ChargeReclamationPool.EntryChargeBytes;
        foreach (var row in retainedRows)
        {
            Assert.True(((IPyOwnershipSnapshot)row).TrySnapshotOwnership(out var rowBytes));
            Assert.True(((IPyOwnershipSnapshot)row[0]).TrySnapshotOwnership(out var fieldBytes));
            expected += rowBytes + fieldBytes + 2 * ChargeReclamationPool.EntryChargeBytes;
        }
        Assert.Equal(1 + 2 * retainedRows.Count, pool.Count);
        Assert.Equal(expected, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        for (var k = 300; k < 310; k++)
        {
            Assert.True(live.TryMoveNext(out var row));
            Assert.Equal("r" + k, ((PyString)((PyList)row)[0]).AsString());
        }
        GC.KeepAlive(retainedRows);
    }

    private static PyList BuildLines()
    {
        var items = new object[2000];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = PyString.FromString("r" + i);
        }

        return new PyList(items);
    }

    private static LythonRuntime.CsvOptions CreateOptions()
    {
        return new LythonRuntime.CsvOptions(
            PyStringOps.CommaLiteral,
            null,
            LythonRuntime.CsvQuotingMode.Minimal,
            true,
            null,
            false,
            PyString.FromString("\n"),
            false);
    }

    // All abandoned sources die with this frame, so no test slots root them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference<LythonRuntime.CsvRecordSource>> AbandonSources(
        LythonRuntime.ExecutionContext context, PyList lines, LythonSourceSpan span)
    {
        var abandoned = new List<WeakReference<LythonRuntime.CsvRecordSource>>();
        for (var s = 0; s < 200; s++)
        {
            var source = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
            abandoned.Add(new(source));
            for (var k = 0; k < 50; k++)
            {
                Assert.True(source.TryMoveNext(out _));
            }
        }
        return abandoned;
    }
}
