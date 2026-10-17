using System.Reflection;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ComparisonResultRouteTests
{
    [Theory]
    [InlineData("syntax", false)]
    [InlineData("lowered", false)]
    [InlineData("lowered-async", false)]
    [InlineData("executable", false)]
    [InlineData("executable-async", false)]
    [InlineData("syntax", true)]
    [InlineData("lowered", true)]
    [InlineData("lowered-async", true)]
    [InlineData("executable", true)]
    [InlineData("executable-async", true)]
    public async Task DecidingResultRetainsItsIdentityAcrossEvaluationRoutes(string route, bool final)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var expected = PyString.FromString(final ? "retained" : "", context.MemoryGovernor, null);
        var comparison = new Comparison(expected);
        var member = final ? "__gt__" : "__lt__";
        var type = new PyType("Operand", [], new()
        {
            [member] = comparison, ["__getattribute__"] = new Lookup(member, comparison)
        }, context.MemoryGovernor, null);
        context.Variables["operand"] = new PyInstance(type, context.MemoryGovernor, null);
        var tail = new Tail();
        context.Variables["tail"] = tail;
        var frontend = LythonFrontend.Compile(final ? "return 0 < 1 < operand" : "return operand < 1 < tail()");
        var script = frontend.Script.RequireNotNull();
        var syntax = Assert.IsType<ReturnStatementSyntax>(Assert.Single(script.Statements)).Expression.RequireNotNull();
        var lowered = LoweredScript.LowerStandaloneExpression(syntax);
        var code = ExecutableScript.Compile(LoweredScript.Lower(script)).EntryPoint;
        var result = route switch
        {
            "syntax" => Invoke("EvaluateExpression", syntax, context),
            "lowered" => Invoke("EvaluateLoweredExpression", lowered, context),
            "lowered-async" => await (ValueTask<object>)Invoke("EvaluateLoweredExpressionAsync", lowered, context),
            "executable" => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null),
            _ => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
        };
        Assert.Same(expected, result);
        Assert.Equal(1, comparison.Calls);
        Assert.Equal(0, tail.Calls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Invoke(string name, object expression, Context context)
        => typeof(LythonRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [expression, context])!;

    private sealed class Comparison(object result) : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return result; }
    }

    private sealed class Lookup(string member, Comparison comparison) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => Assert.IsType<PyString>(arguments[0].Value).AsString() == member ? comparison
                : throw new LythonRuntimeException("AttributeError", "missing test member", span);
    }

    private sealed class Tail : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return new BigInteger(2); }
    }
}
