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
        // Compilation and context setup are outside the measured operation. No
        // builtin namespace is needed to construct (and refuse) these results.
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
            if (pass == 2) Assert.True(allocated < 131072, $"Denied {operation} allocated {allocated} bytes.");
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
