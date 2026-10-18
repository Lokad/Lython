using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StringJoinCleanupTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultAdmissionIncludesLiveDrainAndBuilder(bool asynchronous, bool shared)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, MaxExecutionMemoryBytes = shared ? null : limit });
            var governor = context.MemoryGovernor;
            var first = PyString.FromString("abcd", governor, Span);
            var last = PyString.FromString("efgh", governor, Span);
            var parts = new PyList([first, last], governor, Span);
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return '|'.join").Script!)).EntryPoint;
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            // Four drained slots (64), builder capacity (16), nine output bytes
            // plus metadata (137). Leave one byte too little for the final value.
            var available = 64 + 16 + PyString.EstimateApproximateBytes(9) - 1;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(limit - budget.ReservedBytes - available, out blocker));
            else
            {
                var padding = limit - governor.CurrentCommittedBytes - available;
                governor.Reserve(padding, Span);
                governor.Commit(padding);
            }
            using (blocker)
            {
                var before = governor.CurrentCommittedBytes;
                object? published = null;
                CallArgumentValue[] arguments = [CallArgumentValue.Positional(parts)];
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    published = asynchronous ? await callable.InvokeAsync(arguments, Span, context)
                        : callable.Invoke(arguments, Span, context);
                });
                Assert.Null(published);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit, error.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, error);
                Assert.Same(Span, error.Span);
                Assert.Equal(PyString.EstimateApproximateBytes(9), governor.LastDeniedReservationBytes);
                Assert.Equal(before, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Same(first, parts[0]);
                Assert.Same(last, parts[1]);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidItemReleasesDrainedAndOutputScratch(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var first = PyString.FromString("abcdefghij", context.MemoryGovernor, Span);
            var parts = new PyList([first, BigInteger.One], context.MemoryGovernor, Span);
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return '|'.join").Script!)).EntryPoint;
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(parts)];
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await callable.InvokeAsync(arguments, Span, context);
                else callable.Invoke(arguments, Span, context);
            });
            Assert.Equal("TypeError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Same(first, parts[0]);
            Assert.Equal(BigInteger.One, parts[1]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
