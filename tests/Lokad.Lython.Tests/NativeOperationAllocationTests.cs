using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NativeOperationAllocationTests
{
    [Fact]
    public void CompletedTupleRefusesBeforeAllocatingFinalBackingAndKeepsInputsFunded()
    {
        const int count = 131072;
        var inputBytes = 64L + 16L * count;
        var budget = new LythonMemoryBudget(inputBytes + 4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        governor.Reserve(inputBytes, null);
        var values = new object[count];
        Array.Fill(values, PyNone.Instance);
        governor.Commit(inputBytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<LythonRuntimeException>(() => new PyTuple(values.AsSpan(), governor));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(inputBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(inputBytes, budget.ReservedBytes);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => new PyTuple([PyNone.Instance], governor)));
#if !DEBUG
        Assert.True(allocated < 65536, $"Refused completed tuple allocated {allocated} bytes.");
#endif
        GC.KeepAlive(values);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("d", 262144, false)] [InlineData("d", 262144, true)]
    [InlineData("x", 32768, false)] [InlineData("x", 32768, true)]
    [InlineData("hex", 32768, false)] [InlineData("hex", 32768, true)]
    [InlineData("%d", 262144, false)] [InlineData("%d", 262144, true)]
    [InlineData("%x", 32768, false)] [InlineData("%x", 32768, true)]
    public async Task IntegerFormattingRefusesBeforeConstructingProportionalDigits(string format, int bits, bool asynchronous)
    {
        var source = format == "hex" ? "return hex(value)"
            : format.StartsWith('%') ? "return '" + format + "' % value"
            : "return f'{value:" + format + "}'";
        var frontend = LythonFrontend.Compile(source);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        for (var pass = 0; pass < 3; pass++)
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var input = LythonRuntime.OwnFreshInteger(BigInteger.One << bits, context.MemoryGovernor, null);
            context.Variables["value"] = input;
            Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 4096, out var pressure));
            using (pressure)
            {
                LythonRuntimeException? failure = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    if (asynchronous)
                    {
                        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        Assert.True(pending.IsCompleted);
                        _ = await pending;
                    }
                    else _ = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.NotNull(failure);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(input, context.Variables["value"]);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
#if !DEBUG
                if (pass == 2) Assert.True(allocated < 32768, $"Refused integer {format} formatting allocated {allocated} bytes.");
#endif
            }
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelledIntegerScratchAdmissionStopsBeforeDigitConstruction(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("return f'{value:d}'");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        for (var pass = 0; pass < 3; pass++)
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            using var cancellation = new CancellationTokenSource();
            var observed = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = observed, CancellationToken = cancellation.Token });
            var input = LythonRuntime.OwnFreshInteger(BigInteger.One << 262144, context.MemoryGovernor, null);
            context.Variables["value"] = input;
            var entered = false;
            observed.AfterGrowth = bytes =>
            {
                if (bytes > 4096) { entered = true; cancellation.Cancel(); }
            };
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous)
                {
                    var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                    Assert.True(pending.IsCompleted);
                    _ = await pending;
                }
                else _ = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(entered);
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Same(input, context.Variables["value"]);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
#if !DEBUG
            if (pass == 2) Assert.True(allocated < 32768, $"Cancelled integer formatting allocated {allocated} bytes.");
#endif
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("shlex-quote", false)] [InlineData("shlex-quote", true)]
    [InlineData("shlex-join", false)] [InlineData("shlex-join", true)]
    [InlineData("shlex-split", false)] [InlineData("shlex-split", true)]
    [InlineData("shlex-lexer", false)] [InlineData("shlex-lexer", true)]
    [InlineData("random-bytes", false)] [InlineData("random-bytes", true)]
    [InlineData("random-bits", false)] [InlineData("random-bits", true)]
    [InlineData("integer-to-bytes", false)] [InlineData("integer-to-bytes", true)]
    [InlineData("float-fixed-format", false)] [InlineData("float-fixed-format", true)]
    [InlineData("float-scientific-format", false)] [InlineData("float-scientific-format", true)]
    [InlineData("float-general-width", false)] [InlineData("float-general-width", true)]
    [InlineData("decimal-format-width", false)] [InlineData("decimal-format-width", true)]
    [InlineData("complex-format-width", false)] [InlineData("complex-format-width", true)]
    [InlineData("decimal-digits", false)] [InlineData("decimal-digits", true)]
    [InlineData("decimal-text", false)] [InlineData("decimal-text", true)]
    [InlineData("decimal-context-text", false)] [InlineData("decimal-context-text", true)]
    [InlineData("byte-sized", false)] [InlineData("byte-sized", true)]
    [InlineData("byte-iterable", false)] [InlineData("byte-iterable", true)]
    [InlineData("integer-byte-iterable", false)] [InlineData("integer-byte-iterable", true)]
    [InlineData("byte-fromhex", false)] [InlineData("byte-fromhex", true)]
    [InlineData("byte-rsplit", false)] [InlineData("byte-rsplit", true)]
    [InlineData("date-strftime", false)] [InlineData("date-strftime", true)]
    [InlineData("date-format", false)] [InlineData("date-format", true)]
    [InlineData("time-strftime", false)] [InlineData("time-strftime", true)]
    [InlineData("date-strptime", false)] [InlineData("date-strptime", true)]
    [InlineData("bytes", false)] [InlineData("bytes", true)]
    [InlineData("byte-slice", false)] [InlineData("byte-slice", true)]
    [InlineData("byte-stepped-slice", false)] [InlineData("byte-stepped-slice", true)]
    [InlineData("byte-repr", false)] [InlineData("byte-repr", true)]
    [InlineData("byte-join", false)] [InlineData("byte-join", true)]
    [InlineData("byte-join-input", false)] [InlineData("byte-join-input", true)]
    [InlineData("byte-hex", false)] [InlineData("byte-hex", true)]
    [InlineData("byte-translate", false)] [InlineData("byte-translate", true)]
    [InlineData("byte-expandtabs", false)] [InlineData("byte-expandtabs", true)]
    [InlineData("byte-decode", false)] [InlineData("byte-decode", true)]
    [InlineData("byte-replace", false)] [InlineData("byte-replace", true)]
    [InlineData("byte-pad", false)] [InlineData("byte-pad", true)]
    [InlineData("byte-zfill", false)] [InlineData("byte-zfill", true)]
    [InlineData("byte-lower", false)] [InlineData("byte-lower", true)]
    [InlineData("json-integer-indent", false)] [InlineData("json-integer-indent", true)]
    [InlineData("json-string-indent", false)] [InlineData("json-string-indent", true)]
    [InlineData("lower", false)] [InlineData("lower", true)]
    [InlineData("upper", false)] [InlineData("upper", true)]
    [InlineData("capitalize", false)] [InlineData("capitalize", true)]
    [InlineData("title", false)] [InlineData("title", true)]
    [InlineData("swapcase", false)] [InlineData("swapcase", true)]
    [InlineData("casefold", false)] [InlineData("casefold", true)]
    [InlineData("replace", false)] [InlineData("replace", true)]
    [InlineData("expandtabs", false)] [InlineData("expandtabs", true)]
    [InlineData("translate", false)] [InlineData("translate", true)]
    public async Task RefusalPrecedesProportionalConstruction(string operation, bool asynchronous)
    {
        // Compilation and context setup are outside the measured operation.
        var source = NativeOperationSources.RefusedConstruction(operation).Replace("return len(", "return (");
        var frontend = LythonFrontend.Compile(source);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        for (var pass = 0; pass < 3; pass++)
        {
            var budget = new LythonMemoryBudget(32768);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var randomState = context.State.RandomState.Snapshot();
            LythonRuntimeException? failure = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                if (asynchronous)
                {
                    var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                    // CPU-only operations must finish on this thread for this
                    // allocation measurement; public tests cover API workers.
                    Assert.True(pending.IsCompleted);
                    _ = await pending;
                }
                else _ = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            }
            catch (LythonRuntimeException error) { failure = error; }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.NotNull(failure);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            if (operation is "float-general-width" or "decimal-format-width" or "complex-format-width")
                Assert.Equal(operation == "decimal-format-width" ? 3 : 2, failure.Span?.Line);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (operation.StartsWith("random-", StringComparison.Ordinal)) Assert.Equal(randomState, context.State.RandomState.Snapshot());
            // Debug async iterators include state-machine traffic. Optimized
            // builds qualify allocation traffic; refusal/unwind hold in both.
#if !DEBUG
            if (pass == 2) Assert.True(allocated < 131072, $"Denied {operation} allocated {allocated} bytes.");
#endif
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IntegerBytesAvoidPerByteBigIntegerIntermediates(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("return (0x" + new string('f', 16384) + ").to_bytes(8192)");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        for (var pass = 0; pass < 3; pass++)
        {
            var budget = new LythonMemoryBudget(32768);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var before = GC.GetAllocatedBytesForCurrentThread();
            object? value;
            if (asynchronous)
            {
                var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                Assert.True(pending.IsCompleted);
                value = await pending;
            }
            else value = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var bytes = Assert.IsType<PyBytes>(value);
            Assert.Equal(8192, bytes.Length);
            Assert.True(bytes.Bytes.IndexOfAnyExcept((byte)0xff) < 0);
#if !DEBUG
            if (pass == 2) Assert.True(allocated < 131072, $"Integer conversion allocated {allocated} bytes.");
#endif
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
