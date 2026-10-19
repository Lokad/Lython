using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DictionaryItemBindingBoundaryTests
{

    [Theory]
    [InlineData("__getitem__", false)]
    [InlineData("__getitem__", true)]
    [InlineData("__setitem__", false)]
    [InlineData("__setitem__", true)]
    [InlineData("__delitem__", false)]
    [InlineData("__delitem__", true)]
    public async Task PositionalAndArityFailuresPreserveResultsAndCleanup(string method, bool extra)
    {
        var args = method == "__setitem__" ? "pick(),value()" : "pick()";
        if (extra) args += ",value()";
        var source = "global published\npublished=table." + method + "(" + args + ")\nreturn published";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects, table, key) = Prepare(owner);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (extra)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal("TypeError", error.ExceptionType);
                    completedError = Describe(error);
                    Assert.Equal(new BigInteger(11), context.Variables["published"]);
                    Assert.Equal(new BigInteger(7), table.GetItem(key));
                }
                else
                {
                    Assert.Null(failure);
                    if (method == "__getitem__") Assert.Equal(new BigInteger(7), result);
                    else Assert.Same(PyNone.Instance, result);
                    Assert.Equal(method != "__delitem__", table.ContainsKey(key));
                    if (method == "__setitem__") Assert.Equal(new BigInteger(9), table.GetItem(key));
                }
                Assert.Equal(method == "__setitem__" ? (extra ? ["key", "value", "value"] : ["key", "value"])
                    : extra ? new[] { "key", "value" } : new[] { "key" }, effects);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects, PyDict Table, PyString Key) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var key = PyString.FromString("x", context.MemoryGovernor, null);
        var table = new PyDict(context.MemoryGovernor);
        table.SetItem(key, new BigInteger(7));
        var effects = new List<string>();
        context.Variables["table"] = table;
        context.Variables["published"] = new BigInteger(11);
        context.Variables["pick"] = new Argument(effects, "key", key);
        context.Variables["value"] = new Argument(effects, "value", new BigInteger(9));
        return (context, effects, table, key);
    }

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static string Describe(LythonRuntimeException error)
        => $"{error.ExceptionType}:{error.Message}:{error.Span?.Start},{error.Span?.Length},{error.Span?.Line},{error.Span?.Column}";

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Argument(List<string> effects, string name, object value) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { effects.Add(name); return value; }
    }
}
