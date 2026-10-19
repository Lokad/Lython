using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FormattedStringExecutableTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalJoinDenialPreservesTargetAndReleasesReservations(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=f'{left}{right}'",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        Context Create()
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
            context.Variables["left"] = PyString.FromString(new string('a', 4096), context.MemoryGovernor, null);
            context.Variables["right"] = PyString.FromString(new string('b', 4096), context.MemoryGovernor, null);
            context.Variables["target"] = PyNone.Instance;
            return context;
        }
        void Run(Context context)
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
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
        Assert.Same(PyNone.Instance, denied.Variables["target"]);
        Assert.Null(denied.CurrentExecutableFrame);
        Assert.Equal(0, denied.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, denied.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData("f'{value}'")]
    [InlineData("f'prefix:{value!r:>{width}}:suffix'")]
    [InlineData("f'{value=}'")]
    [InlineData("f'{f\"{value}\"}'")]
    public void OrdinaryFieldsDoNotForceFallbackOrLocalMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile($"def render(value,width):\n    return {expression}",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var body = Assert.Single(code.EntryPoint.Functions).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.ExpressionFallbacks);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }
}
