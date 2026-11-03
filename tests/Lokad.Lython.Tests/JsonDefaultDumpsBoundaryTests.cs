using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonDefaultDumpsBoundaryTests
{

    [Theory]
    [InlineData(false, "scalar")]
    [InlineData(true, "scalar")]
    [InlineData(false, "sequence")]
    [InlineData(true, "sequence")]
    [InlineData(false, "unsupported")]
    [InlineData(true, "unsupported")]
    [InlineData(false, "cycle")]
    [InlineData(true, "cycle")]
    public async Task DefaultCallPreservesEffectsErrorsAndUnwind(bool asynchronous, string shape)
    {
        const string source = "import json\nreturn json.dumps(make())";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] effects;
        string outcome;
        using (var owner = budget.CreateScope())
        {
            var (context, make) = Prepare(owner, shape);
            if (shape is "unsupported" or "cycle")
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Equal(shape == "unsupported" ? "TypeError" : "ValueError", error.ExceptionType);
                outcome = error.ExceptionType + ":" + error.Message;
            }
            else
            {
                outcome = Assert.IsType<PyString>(await Run(code, context, asynchronous)).AsString();
                Assert.Equal(shape == "scalar" ? "7" : "[\"\\u00e9\\ud83d\\ude00\", null]", outcome);
            }
            effects = make.Events.ToArray();
            Assert.Single(effects);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var expectedOutcome = shape switch
        {
            "cycle" => "ValueError:Circular reference detected.",
            "unsupported" => "TypeError:Object of type set is not JSON serializable",
            "scalar" => "7",
            "sequence" => "[\"\\u00e9\\ud83d\\ude00\", null]",
            _ => throw new InvalidOperationException(shape),
        };
        Assert.Equal(expectedOutcome, outcome);
        Assert.Equal(["make:2,0"], effects);
    }

    private static (Context Context, Make Make) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        object value = new BigInteger(7);
        if (shape == "sequence") value = new PyList([PyString.FromString("é😀", context.MemoryGovernor, null), PyNone.Instance], context.MemoryGovernor, null);
        if (shape == "unsupported") value = new PySet(context.MemoryGovernor, null);
        if (shape == "cycle")
        {
            var cycle = new PyList(Array.Empty<object>(), context.MemoryGovernor, null);
            cycle.Add(cycle);
            value = cycle;
        }
        var make = new Make(value);
        context.Variables["make"] = make;
        return (context, make);
    }

    private static async ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Make(object value) : LythonRuntime.ICallable
    {
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            Events.Add($"make:{context.State.CurrentInterpreterDepth},{context.State.CurrentRecursionDepth}");
            return value;
        }
    }
}
