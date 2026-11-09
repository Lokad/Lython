using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeTextConstructionRollbackTests
{
    [Theory]
    [Trait("Category", "Stress")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CancellationReturnsUnpublishedFragmentsWithoutRetiringInput(int operation)
    {
        const int payloadBytes = 64 * 1024 * 1024;
        var span = new LythonSourceSpan(0, 1, 1, 1);
        var budget = new LythonMemoryBudget(512L * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var admitted = new ManualResetEventSlim();
        using var cancellerReady = new ManualResetEventSlim();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            // One short fragment precedes a substantial second fragment. A
            // joined canceller runs after output admission, without guest hooks
            // or a test-only checkpoint in the construction loop.
            var bytes = new byte[payloadBytes + (operation == 2 ? 3 : 2)];
            bytes.AsSpan().Fill((byte)'B');
            bytes[0] = (byte)'a';
            bytes[1] = operation == 2 ? (byte)',' : (byte)' ';
            if (operation == 2) bytes[2] = (byte)'"';
            var input = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, span);
            LythonRuntime.CsvRecordParser? parser = null;
            if (operation == 2)
            {
                var options = new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral, PyString.FromString("\""),
                    LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
                parser = new(options, context, span);
                parser.Feed(input.Utf8Span);
            }
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var held = budget.ReservedBytes;
            hook.AfterGrowth = growth =>
            {
                if (growth < payloadBytes) return;
                hook.AfterGrowth = null;
                admitted.Set();
            };
            var canceller = new Thread(() =>
            {
                cancellerReady.Set();
                admitted.Wait();
                Thread.Sleep(1);
                cancellation.Cancel();
            }) { IsBackground = true };
            canceller.Start();
            try
            {
                Assert.True(cancellerReady.Wait(TimeSpan.FromSeconds(10)));
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                LythonRuntimeException? failure = null;
                IReadOnlyList<object>? result = null;
                try
                {
                    if (parser is not null)
                    {
                        parser.Finish();
                        Assert.True(parser.TryTakeReady(out var row));
                        result = row;
                    }
                    else if (operation == 1) result = PyStringOps.SplitCasedWhitespace(input, false, context.MemoryGovernor, span, context);
                    else result = PyStringOps.SplitWhitespace(input, -1, context.MemoryGovernor, span);
                }
                catch (LythonRuntimeException error) { failure = error; }
                Assert.True(admitted.IsSet);
                if (failure is not null)
                {
                    Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                    if (parser is not null) Assert.False(parser.TryTakeReady(out _));
                    Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                    Assert.Equal(held, budget.ReservedBytes);
                }
                else
                {
                    // Completion can precede cancellation on a fast/scheduled
                    // run. It must return correct, still-funded values.
                    Assert.NotNull(result);
                    Assert.Equal(2, result.Count);
                    Assert.Equal("a", Assert.IsType<PyString>(result[0]).AsString());
                    var last = Assert.IsType<PyString>(result[1]);
                    Assert.Equal(payloadBytes, last.ByteLength);
                    Assert.Equal(operation == 1 ? (byte)'b' : (byte)'B', last.Utf8Span[^1]);
                    Assert.True(budget.ReservedBytes >= held + last.CommittedOwnedBytes);
                }
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal((byte)'a', input.Utf8Span[0]);
                Assert.Equal((byte)'B', input.Utf8Span[^1]);
            }
            finally
            {
                admitted.Set();
                Assert.True(canceller.Join(TimeSpan.FromSeconds(10)));
                parser?.ReleaseScratch();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
