using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvLongFieldOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);
    private static LythonRuntime.CsvOptions Options => new(PyStringOps.CommaLiteral,
        PyString.FromString("\""), LythonRuntime.CsvQuotingMode.Minimal, true,
        null, false, PyString.FromString("\n"), false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongFieldsKeepOffsetsAndFundingAfterLaterRowsAndScratchCleanup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var a = "source/path/first.txt";
            var b = "Résumé😀_first_field";
            var c = "other first long text";
            var input = new PyList([PyString.FromString($"{a},{b},{c}\n"),
                PyString.FromString("second/path/second.txt,another😀_field,second long text\n")]);
            var source = new LythonRuntime.CsvRecordSource(input, Options, Span, context);
            var first = await Next();
            var saved = Assert.IsType<PyString>(first[1]);
            _ = await Next();
            var end = asynchronous ? await source.TryMoveNextAsync()
                : source.TryMoveNext(out var endedRow) ? endedRow : null;
            Assert.Null(end);
            Assert.Equal([a, b, c], first.Cast<PyString>().Select(value => value.AsString()));
            Assert.Equal(Encoding.UTF8.GetBytes(b), saved.Utf8Span.ToArray());
            Assert.Equal(b.EnumerateRunes().Count(), saved.Length);
            Assert.Same(context.MemoryGovernor, saved.OwnerMemoryGovernor);
            Assert.True(budget.ReservedBytes >= Encoding.UTF8.GetByteCount(a + b + c));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);

            async ValueTask<object[]> Next()
            {
                if (asynchronous) return Assert.IsType<object[]>(await source.TryMoveNextAsync());
                Assert.True(source.TryMoveNext(out var nextRow));
                return nextRow;
            }
            context.State.CloseResources();
            Assert.Equal(b, saved.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedPayloadAdmissionStopsBeforeAllocationAndPublication(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = gate, CancellationToken = cancellation.Token });
            var parser = new LythonRuntime.CsvRecordParser(Options, context, Span);
            // Complete the working input before arming the final-row admission.
            // The closing quote/newline needs no buffer or descriptor growth.
            var field = new string('x', 16384);
            parser.Feed(Encoding.UTF8.GetBytes(field + "," + field + ",\"" + field));
            Assert.False(parser.TryTakeReady(out _));
            var baseline = budget.ReservedBytes;
            long? allocatedAtAdmission = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            if (cancel) gate.AfterGrowth = _ =>
            {
                allocatedAtAdmission = GC.GetAllocatedBytesForCurrentThread() - before;
                cancellation.Cancel();
            };
            else gate.RefuseGrowth = _ =>
            {
                allocatedAtAdmission = GC.GetAllocatedBytesForCurrentThread() - before;
                return true;
            };
            try
            {
                var error = Assert.Throws<LythonRuntimeException>(() => parser.Feed("\"\n"u8));
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.NotNull(allocatedAtAdmission);
                Assert.True(allocatedAtAdmission < 32768, $"Allocated {allocatedAtAdmission} bytes before final row admission.");
                Assert.False(parser.TryTakeReady(out _));
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { parser.ReleaseScratch(); context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
