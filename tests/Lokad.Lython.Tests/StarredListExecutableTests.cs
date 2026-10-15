using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StarredListExecutableTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeniedGrowthLeavesTheOriginalTargetUnchanged(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=[1,*range(10000),2]").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        var original = new PyList(new object[] { new BigInteger(17) }, context.MemoryGovernor, null);
        context.Variables["target"] = original;
        var failure = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        });
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Same(original, context.Variables["target"]);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void EveryFuelBoundaryPreservesPublicationAndUnwindsBothDrivers()
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=[0,*range(3),4]").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        Context Context() => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionSteps = 1000 });
        void Run(Context context, bool asynchronous)
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        }
        var completed = Context();
        Run(completed, false);
        var steps = completed.Limits.ExecutionStepCount;
        Assert.InRange(steps, 1, 100);
        for (var fuel = 0; fuel <= steps; fuel++)
        {
            (bool Published, LythonSourceSpan? Span, long Steps)? synchronous = null;
            foreach (var asynchronous in new[] { false, true })
            {
                var context = Context();
                var original = new PyList(new object[] { new BigInteger(17) }, context.MemoryGovernor, null);
                context.Variables["target"] = original;
                context.Limits.ExecutionStepCount = 1000 - fuel;
                var failure = Record.Exception(() => Run(context, asynchronous));
                if (fuel < steps)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
                }
                else Assert.Null(failure);
                var published = !ReferenceEquals(original, context.Variables["target"]);
                if (published) Assert.Equal(new object[] { new BigInteger(0), new BigInteger(0), new BigInteger(1), new BigInteger(2), new BigInteger(4) }, (PyList)context.Variables["target"]);
                var observation = (published, (failure as LythonRuntimeException)?.Span, context.Limits.ExecutionStepCount);
                if (asynchronous) Assert.Equal(synchronous!.Value, observation);
                else synchronous = observation;
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
    }

    [Fact]
    public void StarredListDoesNotForceExpressionFallbackOrLocalMirroring()
    {
        var frontend = LythonFrontend.Compile("""
def combine(left, right, marker):
    before = marker + 1
    result = [before, *left, marker, *right]
    return result
print(combine([1], [2], 3))
""");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
        var body = Assert.Single(code.EntryPoint.Functions).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.ExpressionFallbacks);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }
}
