using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class EagerComprehensionExecutableTests
{
    [Fact]
    public void ManyFlatClausesCompileWithoutRecursiveClauseFrames()
    {
        var source = "global result\nresult=[value for value in (0,) " +
            string.Concat(Enumerable.Repeat("for value in (value+1,) ", 511)) + "]";
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.True(frontend.Diagnostics.All(d => d.Severity != LythonDiagnosticSeverity.Error));
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        Assert.Empty(code.ExpressionFallbacks);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4 * 1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.Equal(new BigInteger(511), Assert.Single(Assert.IsType<PyList>(context.Variables["result"])));
        Assert.False(context.TryGetInlinedBinding("value", out _));
    }

    [Theory]
    [InlineData("[convert(value) for value in values if value]")]
    [InlineData("{convert(value) for value in values if value}")]
    [InlineData("{value:convert(value) for value in values if value}")]
    public void HelperItemsUseInstructionsInTheCallerFrame(string expression)
    {
        var syntax = LythonFrontend.Compile("def collect(values,convert):\n    return " + expression,regexProvider:Utf8RegexProvider.Instance).Script!;
        var module = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var body = Assert.Single(module.Functions).CodeObject!;
        Assert.Empty(body.ExpressionFallbacks);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.DoesNotContain(body.Constants, value => value is ExecutableCreateGeneratorExpression);
        Assert.Single(body.Constants.OfType<ExecutableStartComprehension>());
        Assert.DoesNotContain("value", body.LocalNames);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DenialRestoresScopeAndPreservesThePreviousTarget(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=[value for value in range(10000)]",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) },regexProvider:Utf8RegexProvider.Instance);
        var original = new PyList([true], context.MemoryGovernor, null);
        context.Variables["target"] = original;
        var closure = context.FunctionClosureContext;
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(original, context.Variables["target"]);
        Assert.Same(closure, context.FunctionClosureContext);
        Assert.False(context.TryGetInlinedBinding("value", out _));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
