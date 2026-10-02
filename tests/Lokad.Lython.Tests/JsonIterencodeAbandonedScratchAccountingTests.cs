using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N38: abandoned encoder iterators stop contributing sort scratch once
// reclaimed: dropping 50 mid-stream sorted iterators then sweeping releases
// their tracked scratch (~3MB), while a live iterator keeps streaming
// correct chunks.
public sealed class JsonIterencodeAbandonedScratchAccountingTests
{
    [Fact]
    public void AbandonedSortedStreamsReclaimScratchOnSweep()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 8000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var big = BuildDict(2000);
        AbandonIterators(context, big, span);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        context.Services.State.CallTemporaries.Sweep(full: true);

        var live = new PyDict();
        live.SetItem(PyString.FromString("b"), (object)1);
        live.SetItem(PyString.FromString("a"), (object)2);
        var iterator = new LythonRuntime.JsonEncodeIterator(live, SortedOptions(), context, span);
        var text = new StringBuilder();
        while (iterator.TryMoveNext(out var chunk))
        {
            text.Append(((PyString)chunk).AsString());
        }

        Assert.Equal("{\"a\": 2, \"b\": 1}", text.ToString());
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes < 1000000);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes < 100000);
    }

    private static PyDict BuildDict(int count)
    {
        var dict = new PyDict();
        for (var i = 0; i < count; i++)
        {
            dict.SetItem(PyString.FromString("k" + i), (object)i);
        }

        return dict;
    }

    private static LythonRuntime.JsonDumpOptions SortedOptions()
        => new(false, true, true, true, null, ", ", ": ", null, true);

    // All abandoned iterators die with this frame, so no test slots root them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonIterators(LythonRuntime.ExecutionContext context, PyDict big, LythonSourceSpan span)
    {
        var options = SortedOptions();
        for (var s = 0; s < 50; s++)
        {
            var iterator = new LythonRuntime.JsonEncodeIterator(big, options, context, span);
            for (var k = 0; k < 5; k++)
            {
                Assert.True(iterator.TryMoveNext(out _));
            }
        }
    }
}
