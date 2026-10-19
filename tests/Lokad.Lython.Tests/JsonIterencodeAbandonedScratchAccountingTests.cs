using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Sorted JSON iterators keep live scratch and emit correct chunks; finite
// host-driven owners return temporary accounting without collector hooks.
[Collection("AllocationSensitive")]
public sealed class JsonIterencodeAbandonedScratchAccountingTests
{
    [Fact]
    public void SortedJsonBatchesReturnAllowanceAndDoNotDisruptAnotherIterator()
    {
        var budget = new LythonMemoryBudget(262144);
        using var retainedOwner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = retainedOwner },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var live = new PyDict();
        live.SetItem(PyString.FromString("b"), (object)1);
        live.SetItem(PyString.FromString("a"), (object)2);
        var iterator = new LythonRuntime.JsonEncodeIterator(live, SortedOptions(), context, span);
        var baseline = budget.ReservedBytes;
        var big = BuildDict(16);
        for (var batch = 0; batch < 8; batch++)
        {
            using (var owner = budget.CreateScope())
            {
                var temporary = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
                AbandonIterators(temporary, big, span);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        var text = new StringBuilder();
        while (iterator.TryMoveNext(out var chunk)) text.Append(((PyString)chunk).AsString());
        Assert.Equal("{\"a\": 2, \"b\": 1}", text.ToString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(iterator);
        iterator = null!;
        retainedOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
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
        for (var s = 0; s < 2; s++)
        {
            var iterator = new LythonRuntime.JsonEncodeIterator(big, options, context, span);
            for (var k = 0; k < 5; k++)
            {
                Assert.True(iterator.TryMoveNext(out _));
            }
        }
    }
}
