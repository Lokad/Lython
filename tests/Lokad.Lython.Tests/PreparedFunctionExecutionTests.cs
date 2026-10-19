using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class PreparedFunctionExecutionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AliasedDefaultsAndNonlocalCellsSurviveGeneratorClose(bool asynchronous, bool generic)
    {
        var source = """
            def make():
                state = 1
                def innerTYPE(items=(yield 'default')):
                    'prepared function'
                    nonlocal state
                    state += items[0]
                    return state
                state = 10
                yield inner
            g = make()
            next(g)
            values = [3]
            function = g.send(values)
            values[0] = 4
            g.close()
            return function
            """.Replace("innerTYPE", generic ? "inner[T]" : "inner");
        var syntax = LythonFrontend.Compile(source);
        Assert.NotNull(syntax.Script);
        Assert.DoesNotContain(syntax.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax.Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var returned = asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var function = Assert.IsType<PyExecutableFunction>(returned);
        var span = new LythonSourceSpan(0, 1, 1, 1);
        foreach (var expected in new[] { 14, 18 })
        {
            var value = asynchronous ? await function.InvokeAsync([], span, context) : function.Invoke([], span, context);
            Assert.Equal(new BigInteger(expected), value);
        }
        Assert.True(function.TryGetMember("__doc__", out var docstring));
        Assert.Equal("prepared function", Assert.IsType<Runtime.Text.PyString>(docstring).AsString());
        if (generic)
        {
            Assert.True(function.TryGetMember("__type_params__", out var parameters));
            Assert.Equal("T", Assert.IsType<PyTypeParameter>(Assert.Single(Assert.IsType<PyTuple>(parameters))).Name);
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
