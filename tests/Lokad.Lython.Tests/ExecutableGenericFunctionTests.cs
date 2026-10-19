using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableGenericFunctionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "ordinary", "def f[T](x:T)->T:return x\nreturn f" },
        { "escaped", "def make():\n seed=1\n def f[T](x):return seed+x\n seed=2\n return f\nreturn make()" },
        { "shadow", "def make():\n T=17\n def f[T]():return T\n return f\nreturn make()" },
        { "nested", "def f[T](x):\n def helper():return T,x\n x=7\n return helper\nreturn f(2)" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task GenericBodiesAndNestedHelpersRemainExecutable(string name, string source)
    {
        var syntax = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(syntax.Script);
        Assert.DoesNotContain(syntax.Diagnostics, diagnostic => diagnostic.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax.Script!)).EntryPoint;
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var returned = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            var function = Assert.IsType<PyExecutableFunction>(returned);
            CallArgumentValue[] arguments = name is "ordinary" or "escaped" ? [CallArgumentValue.Positional(new BigInteger(3))] : [];
            var result = asynchronous ? await function.InvokeAsync(arguments, new(0, 1, 1, 1), context)
                : function.Invoke(arguments, new(0, 1, 1, 1), context);
            if (name == "ordinary") Assert.Equal(new BigInteger(3), result);
            else if (name == "escaped") Assert.Equal(new BigInteger(5), result);
            else if (name == "shadow")
            {
                Assert.True(function.TryGetMember("__type_params__", out var parameters));
                Assert.Same(Assert.Single(Assert.IsType<PyTuple>(parameters)), result);
            }
            else
            {
                var values = Assert.IsType<PyTuple>(result);
                Assert.Equal("T", Assert.IsType<PyTypeParameter>(values[0]).Name);
                Assert.Equal(new BigInteger(7), values[1]);
            }
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
