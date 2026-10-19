using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableLoopTargetTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("def run(values):\n for left,right in values: pass")]
    [InlineData("def run(values):\n return [left+right for left,right in values]")]
    public void LoopGroupsUseExecutableOperations(string source)
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.Single(code.Constants.OfType<ExecutableUnpackValues>());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task LoopRestDenialPreservesOnlyCompletedOuterStores(bool asynchronous, bool comprehension, bool nested)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var sink = new ExecutableUnpackingTests.Sink();
        var outerSink = new ExecutableUnpackingTests.Sink();
        using var values = new ExecutableUnpackingTests.LimitAfterEnumeration(context.MemoryGovernor, limit);
        context.Variables["sink"] = sink;
        context.Variables["outerSink"] = outerSink;
        context.Variables["source"] = values;
        context.Variables["outer"] = new PyList([PyNone.Instance, values], context.MemoryGovernor, Span);
        var target = nested ? "outerSink.value,(sink.value,*rest)" : "sink.value,*rest";
        var input = nested ? "outer" : "source";
        var source = comprehension ? "[None for " + target + " in [" + input + "]]"
            : "for " + target + " in [" + input + "]: pass";
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.True(values.LimitedAllowance);
            Assert.Equal(1, values.DisposedEnumerations);
            Assert.Equal(0, sink.Stores);
            Assert.Equal(nested ? 1 : 0, outerSink.Stores);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { values.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
