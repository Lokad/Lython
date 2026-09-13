using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG01: abandoned record sources stop contributing charges once reclaimed:
/// dropping 200 mid-scan sources then continuing on a live one releases the
/// dead pools and scratches on cadence, while the live source keeps yielding
/// correct rows.
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
        AbandonSources(context, lines, span);
        var live = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        for (var k = 0; k < 300; k++)
        {
            Assert.True(live.TryMoveNext(out _));
        }

        Assert.True(context.MemoryGovernor.CurrentCommittedBytes < 1000000);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes < 100000);
        for (var k = 300; k < 310; k++)
        {
            Assert.True(live.TryMoveNext(out var row));
            Assert.Equal("r" + k, ((PyString)((PyList)row)[0]).AsString());
        }
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
    private static void AbandonSources(LythonRuntime.ExecutionContext context, PyList lines, LythonSourceSpan span)
    {
        for (var s = 0; s < 200; s++)
        {
            var source = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
            for (var k = 0; k < 50; k++)
            {
                Assert.True(source.TryMoveNext(out _));
            }
        }
    }
}