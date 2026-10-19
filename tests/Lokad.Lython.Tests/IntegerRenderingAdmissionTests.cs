using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class IntegerRenderingAdmissionTests
{
    // Frozen on 2c381510 before changing any production rendering code.
    private static string[] Checkpoints(string route) => route switch
    {
        "str" => ["-:0", "7,3,1,8:0", "11,8,1,12:0", "11,10,1,12:0", "11,10,1,12:0", "11,10,1,12:0",
            "7,15,1,8:1", "7,15,1,8:1", "7,15,1,8:1", "7,15,1,8:1", "-:1", "0,22,1,1:1"],
        "repr" => ["-:0", "7,4,1,8:0", "12,8,1,13:0", "12,10,1,13:0", "12,10,1,13:0", "12,10,1,13:0",
            "7,16,1,8:1", "7,16,1,8:1", "7,16,1,8:1", "7,16,1,8:1", "-:1", "0,23,1,1:1"],
        _ => ["-:0", "10,8,1,11:0", "10,10,1,11:0", "10,10,1,11:0", "10,10,1,11:0",
            "7,15,1,8:1", "-:1", "7,15,1,8:1", "0,22,1,1:1"],
    };

    public static IEnumerable<object[]> Refusals()
    {
        foreach (var mode in new[] { 0, 1, 2 })
        foreach (var shared in new[] { false, true }) yield return [mode, shared];
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public void RefusalPrecedesProportionalFormattingAllocation(int mode, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            const long limit = 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                    { MemoryBudget = owner, MaxExecutionMemoryBytes = shared ? null : limit });
                var governor = context.MemoryGovernor;
                var inputBytes = RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(65537) + 64;
                governor.Reserve(inputBytes, null);
                governor.Commit(inputBytes);
                object input = BigInteger.One << 65536;
                var padding = limit - budget.ReservedBytes - 1024;
                ILythonMemoryReservation? blocker = null;
                if (shared) Assert.True(budget.TryReserve(padding, out blocker));
                else { governor.Reserve(padding, null); governor.Commit(padding); }
                using var hostPadding = blocker;
                var committed = governor.CurrentCommittedBytes;
                LythonRuntimeException? failure = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                try { _ = Render(input, context, mode); }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.NotNull(failure);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit,
                    failure.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.True(governor.LastDeniedReservationBytes > 16384);
                Assert.Equal(committed, governor.CurrentCommittedBytes);
                AssertUnwound(context);
                Assert.Equal(65537, ((BigInteger)input).GetBitLength());
                Assert.True(((BigInteger)input).IsPowerOfTwo);
                if (measured) Assert.True(allocated < 16384, $"Refused integer rendering allocated {allocated} bytes.");
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RefusedResultCopyRefundsWorkingStorage(int mode)
    {
        object value = BigInteger.One << 256;
        long working;
        var controlBudget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = controlBudget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            _ = Render(value, context, mode);
            working = context.MemoryGovernor.PeakAccountedBytes - context.MemoryGovernor.CurrentCommittedBytes;
            Assert.True(working > 0);
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes > before);
        }
        Assert.Equal(0, controlBudget.ReservedBytes);

        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - working - 1, out var padding));
            using (padding)
            {
                var before = context.MemoryGovernor.CurrentCommittedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => Render(value, context, mode));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(PyString.EstimateApproximateBytes(78), context.MemoryGovernor.LastDeniedReservationBytes);
                Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
                AssertUnwound(context);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ResultKeepsFundingAfterWorkingStorageEnds(int mode)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var before = budget.ReservedBytes;
            var result = Render(BigInteger.One << 256, context, mode);
            Assert.Equal(BigInteger.One << 256, BigInteger.Parse(result.AsString()));
            Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
            Assert.True(budget.ReservedBytes >= before + PyString.EstimateApproximateBytes(result.Utf8Bytes.Length));
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("str", false)]
    [InlineData("str", true)]
    [InlineData("repr", false)]
    [InlineData("repr", true)]
    [InlineData("format", false)]
    [InlineData("format", true)]
    public async Task RenderingPreservesResultsAndArgumentEffects(string route, bool asynchronous)
    {
        var source = "return " + (route == "format" ? "f'{argument()}'" : route + "(argument())");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, argument) = Prepare(owner);
            Assert.Equal("-123456", Assert.IsType<PyString>(await Run(code, context, asynchronous)).AsString());
            Assert.Equal(1, argument.Calls);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (Context, Argument) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var argument = new Argument();
        context.Variables["argument"] = argument;
        return (context, argument);
    }

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static PyString Render(object value, Context context, int mode) => mode switch
    {
        0 => PyRendering.ToInterpolatedPyString(value, new(context)),
        1 => PyRendering.ToPythonPyString(value, new(context)),
        _ => PyRendering.ToReprPyString(value, new(context)),
    };

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Argument : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return new BigInteger(-123456); }
    }
}
