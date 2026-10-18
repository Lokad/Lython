using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExactIntegerUnaryAdmissionTests
{
    [Theory]
    [InlineData("-", false)]
    [InlineData("-", true)]
    [InlineData("~", false)]
    [InlineData("~", true)]
    [InlineData("abs", false)]
    [InlineData("abs", true)]
    public void DeniedUnaryResultPreservesThePreviousTarget(string token, bool asynchronous)
    {
        var expression = token == "abs" ? "abs(seed)" : token + "seed";
        var syntax = LythonFrontend.Compile("global entered,target\nentered=True\ntarget=" + expression).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = limit });
        var seed = (token == "abs" ? -BigInteger.One : BigInteger.One) << 262144;
        context.ObserveValue(seed, default);
        var original = new PyList([true], context.MemoryGovernor, null);
        context.Variables["seed"] = seed;
        context.Variables["target"] = original;
        context.Variables["entered"] = false;
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes - 16384;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
        Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, error.Attribution?.Reason);
        Assert.True(Assert.IsType<bool>(context.Variables["entered"]));
        Assert.Same(original, context.Variables["target"]);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
    }
}
