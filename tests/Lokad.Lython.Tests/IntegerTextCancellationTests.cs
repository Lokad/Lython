using System.Reflection;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class IntegerTextCancellationTests
{
    private static readonly LythonSourceSpan Span = new(10, 3, 2, 4);
    private static readonly Func<object, int, Context, LythonSourceSpan, object> Parse = typeof(LythonRuntime)
        .GetMethod("ParseOwnedIntegerText", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object, int, Context, LythonSourceSpan, object>>();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringAdmissionStopsParsingAndKeepsTheInputFunded(bool bytes)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            const string text = "999999999999999999999";
            object input = bytes ? new PyBytes(Encoding.ASCII.GetBytes(text), context.MemoryGovernor, Span)
                : PyString.FromString(text, context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var reached = false;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                reached = true;
                cancellation.Cancel();
            };
            try
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Parse(input, 10, context, Span));
                Assert.True(reached);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Same(Span, error.Span);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(text, input is PyString value ? value.AsString()
                    : Encoding.ASCII.GetString(((PyBytes)input).Bytes));
                using (var other = budget.CreateScope())
                    Assert.True(other.TryReserve(256, out _));
                Assert.Equal(held, budget.ReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact, Trait("Category", "Stress")]
    public void CancellationDuringMagnitudeWorkDrainsBeforeRefund()
    {
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        using (var admitted = new ManualResetEventSlim())
        using (var completed = new ManualResetEventSlim())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString(new string('f', 1024 * 1024), context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            Exception? failure = null;
            hook.AfterGrowth = _ => { hook.AfterGrowth = null; admitted.Set(); };
            var worker = new Thread(() =>
            {
                try { _ = Parse(input, 16, context, Span); }
                catch (Exception error) { failure = error; }
                finally { completed.Set(); }
            }) { IsBackground = true };
            worker.Start();
            try
            {
                Assert.True(admitted.Wait(TimeSpan.FromSeconds(5)));
                // The parser owns its magnitude reservation and is running actual
                // finite work, rather than waiting in an admission callback.
                Thread.Sleep(20);
                Assert.False(completed.IsSet);
                Assert.True(budget.ReservedBytes > held);
                cancellation.Cancel();
                Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                cancellation.Cancel();
                worker.Join(); // End all users before resource/owner disposal.
                context.State.CloseResources();
            }
            Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(1024 * 1024, input.Utf8Bytes.Length);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalDuringPromotionKeepsItsFirstOriginAndRefundsScratch(bool bytes)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = hook });
            const string text = "999999999999999999999";
            object input = bytes ? new PyBytes(Encoding.ASCII.GetBytes(text), context.MemoryGovernor, Span)
                : PyString.FromString(text, context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            var origin = new LythonSourceSpan(99, 2, 8, 1);
            LythonRuntimeException? first = null;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                first = context.MemoryGovernor.RefuseAdmission("controlled integer promotion", 777, origin);
            };
            try
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Parse(input, 10, context, Span));
                Assert.NotNull(first);
                Assert.Same(first, error);
                Assert.Same(first, context.MemoryGovernor.AdmissionFailure);
                Assert.Same(origin, error.Span);
                Assert.Equal(777L, error.Attribution?.Requested);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
