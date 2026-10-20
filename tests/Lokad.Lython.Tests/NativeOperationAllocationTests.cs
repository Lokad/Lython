using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NativeOperationAllocationTests
{
    [Theory]
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
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            // Debug async iterators include state-machine traffic. Optimized
            // builds qualify allocation traffic; refusal/unwind hold in both.
#if !DEBUG
            if (pass == 2) Assert.True(allocated < 131072, $"Denied {operation} allocated {allocated} bytes.");
#endif
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
