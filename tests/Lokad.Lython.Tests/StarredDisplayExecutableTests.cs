using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StarredDisplayExecutableTests
{
    [Theory]
    [InlineData("{before,*left,marker,*right}")]
    [InlineData("{'before':before,**left,'marker':marker,**right}")]
    public void SetAndDictionaryExpansionDoNotForceFallbackOrMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile($"def combine(left,right,marker):\n    before=marker+1\n    return {expression}").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var body = Assert.Single(code.EntryPoint.Functions).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.ExpressionFallbacks);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TupleFinalConversionRefusesBeforeAssignment(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=(*source,)").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        Context Create()
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1024 * 1024 });
            context.Variables["source"] = new PyTuple(new object[] { new BigInteger(7), new BigInteger(8) }, context.MemoryGovernor, null);
            context.Variables["target"] = PyNone.Instance;
            return context;
        }
        void Run(Context context)
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        }
        var complete = Create();
        Run(complete);
        var denied = Create();
        var padding = 1024 * 1024 - complete.MemoryGovernor.PeakAccountedBytes + 1;
        Assert.True(padding > 0);
        denied.MemoryGovernor.Reserve(padding, null);
        denied.MemoryGovernor.Commit(padding);
        var failure = Assert.Throws<LythonRuntimeException>(() => Run(denied));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(new LythonSourceSpan(21, 10, 2, 8), failure.Span);
        Assert.Same(PyNone.Instance, denied.Variables["target"]);
        Assert.Null(denied.CurrentExecutableFrame);
        Assert.Equal(0, denied.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, denied.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DeniedGrowthLeavesTheOriginalTargetUnchanged(bool asynchronous, bool tuple)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=" + (tuple ? "(1,*range(10000),2)" : "[1,*range(10000),2]")).Script!;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryFuelBoundaryPreservesPublicationAndUnwindsBothDrivers(bool tuple)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=" + (tuple ? "(0,*range(3),4)" : "[0,*range(3),4]")).Script!;
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
                if (published) Assert.Equal(new object[] { new BigInteger(0), new BigInteger(0), new BigInteger(1), new BigInteger(2), new BigInteger(4) }, Assert.IsAssignableFrom<IEnumerable<object>>(context.Variables["target"]));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarredDisplayDoesNotForceExpressionFallbackOrLocalMirroring(bool tuple)
    {
        var frontend = LythonFrontend.Compile($$"""
def combine(left, right, marker):
    before = marker + 1
    result = {{(tuple ? "(before, *left, marker, *right)" : "[before, *left, marker, *right]")}}
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
