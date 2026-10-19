using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Explicit batch cleanup returns temporary funding while a concurrent reader
// and its retained rows stay charged and keep their cursor/content semantics.
[Collection("AllocationSensitive")]
public sealed class CsvAbandonedReaderAccountingTests
{
    [Fact]
    public void ReaderBatchesReleaseOnlyTheirOwnAllowance()
    {
        var budget = new LythonMemoryBudget(262144);
        using var retainedOwner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = retainedOwner },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var lines = BuildLines();
        var live = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
        var rows = new List<PyList>();
        for (var row = 0; row < 5; row++)
        {
            Assert.True(live.TryMoveNext(out var value));
            Assert.Equal("r" + row, ((PyString)value[0]).AsString());
            rows.Add(value);
        }
        var baseline = budget.ReservedBytes;
        var abandoned = new List<WeakReference<LythonRuntime.CsvRecordSource>>();
        for (var batch = 0; batch < 8; batch++)
        {
            using (var owner = budget.CreateScope())
            {
                var temporary = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
                abandoned.Add(ConsumeSource(temporary, lines, span));
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(abandoned, source => Assert.False(source.TryGetTarget(out _)));
        Assert.Equal("r0", ((PyString)rows[0][0]).AsString());
        Assert.True(live.TryMoveNext(out var next));
        Assert.Equal("r5", ((PyString)next[0]).AsString());
        GC.KeepAlive(live); GC.KeepAlive(rows); GC.KeepAlive(next);
        live = null!; rows.Clear(); next = null!;
        retainedOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyList BuildLines()
    {
        var items = new object[16];
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<LythonRuntime.CsvRecordSource> ConsumeSource(
        LythonRuntime.ExecutionContext context, PyList lines, LythonSourceSpan span)
    {
        var source = new LythonRuntime.CsvRecordSource(lines, CreateOptions(), span, context);
        Assert.True(source.TryMoveNext(out var first));
        Assert.Equal("r0", ((PyString)first[0]).AsString());
        Assert.True(source.TryMoveNext(out var second));
        Assert.Equal("r1", ((PyString)second[0]).AsString());
        return new(source);
    }
}
