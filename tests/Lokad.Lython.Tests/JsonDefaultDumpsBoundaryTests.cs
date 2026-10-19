using System.Numerics;
using System.Text.Json;
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
    public async Task DefaultCallPreservesCheckpointsErrorsAndUnwind(bool asynchronous, string shape)
    {
        const string source = "import json\nreturn json.dumps(make())";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
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
        // Frozen on22648c5a before removing default-call packaging.
        var expected = shape switch
        {
            "cycle" => "{\"steps\":19,\"outcome\":\"ValueError:Circular reference detected.\",\"effects\":[\"make:2,0\"],\"checkpoints\":[\"-:\",\"0,11,1,1:\",\"-:\",\"19,4,2,8:\",\"19,10,2,8:\",\"30,4,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\"]}",
            "unsupported" => "{\"steps\":21,\"outcome\":\"TypeError:Object of type set is not JSON serializable\",\"effects\":[\"make:2,0\"],\"checkpoints\":[\"-:\",\"0,11,1,1:\",\"-:\",\"19,4,2,8:\",\"19,10,2,8:\",\"30,4,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\"]}",
            "scalar" => "{\"steps\":21,\"outcome\":\"7\",\"effects\":[\"make:2,0\"],\"checkpoints\":[\"-:\",\"0,11,1,1:\",\"-:\",\"19,4,2,8:\",\"19,10,2,8:\",\"30,4,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"12,25,2,1:make:2,0\"]}",
            "sequence" => "{\"steps\":22,\"outcome\":\"[\\\"\\\\u00e9\\\\ud83d\\\\ude00\\\", null]\",\"effects\":[\"make:2,0\"],\"checkpoints\":[\"-:\",\"0,11,1,1:\",\"-:\",\"19,4,2,8:\",\"19,10,2,8:\",\"30,4,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"30,6,2,19:\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"19,18,2,8:make:2,0\",\"12,25,2,1:make:2,0\"]}",
            _ => throw new InvalidOperationException(shape),
        };
        // Compare decoded fields so JSON escaping choice is not an assertion.
        using var frozen = JsonDocument.Parse(expected);
        Assert.Equal(frozen.RootElement.GetProperty("outcome").GetString(), outcome);
        Assert.Equal(frozen.RootElement.GetProperty("effects").EnumerateArray().Select(x => x.GetString()), effects);
    }

    private static (Context Context, Make Make) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
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
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Make(object value) : LythonRuntime.ICallable
    {
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            Events.Add($"make:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth}");
            return value;
        }
    }
}
