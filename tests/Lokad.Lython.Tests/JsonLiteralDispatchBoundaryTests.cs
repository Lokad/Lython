using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonLiteralDispatchBoundaryTests
{

    // Captured on 527ff833 before changing literal dispatch.
    private static readonly string[] ScalarCheckpoints =
    [
        "-:",
        "0,11,1,1:",
        "-:",
        "19,4,2,8:",
        "19,10,2,8:",
        "30,6,2,19:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "12,25,2,1:",
    ];
    private static readonly string[] PairsCheckpoints =
    [
        "-:",
        "0,11,1,1:",
        "-:",
        "19,4,2,8:",
        "19,10,2,8:",
        "30,6,2,19:",
        "55,4,2,44:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "19,41,2,8:",
        "12,48,2,1:hook:3,0",
    ];

    // Captured on df1ab51f before removing the prefix wrapper.
    private static readonly string[] WhitespaceCheckpoints =
    [
        "-:",
        "0,11,1,1:",
        "-:",
        "19,4,2,8:",
        "19,10,2,8:",
        "30,6,2,19:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "19,18,2,8:",
        "12,25,2,1:",
    ];

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task LiteralCheckpointsPreserveEffectsAndUnwind(bool asynchronous, bool pairs, bool padded)
    {
        var source = "import json\nreturn json.loads(source" + (pairs ? ",object_pairs_hook=hook)" : ")");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] fullEffects;
        using (var owner = budget.CreateScope())
        {
            var (context, hook) = Prepare(owner, pairs, padded);
            var result = await Run(code, context, asynchronous);
            if (pairs)
            {
                var values = Assert.IsType<PyList>(result);
                Assert.Equal(3, values.Count);
                Assert.True(Assert.IsType<bool>(values[0]));
                Assert.False(Assert.IsType<bool>(values[1]));
                Assert.Same(PyNone.Instance, values[2]);
            }
            else Assert.True(Assert.IsType<bool>(result));
            fullEffects = hook.Events.ToArray();
            Assert.Equal(pairs ? 1 : 0, hook.Events.Count);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(pairs ? ["hook:3,0"] : Array.Empty<string>(), fullEffects);
    }

    private static (Context Context, Hook Hook) Prepare(ILythonMemoryBudget owner, bool pairs, bool padded)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var document = pairs ? "{\"café\":[true,false,null],\"z\":null}" : "true";
        if (padded) document = new string(' ', 1025) + document;
        context.Variables["source"] = PyString.FromString(document, context.MemoryGovernor, null);
        var hook = new Hook();
        context.Variables["hook"] = hook;
        return (context, hook);
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

    private sealed class Hook : LythonRuntime.ICallable
    {
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var pairs = Assert.IsType<PyList>(Assert.Single(arguments).Value);
            Assert.Equal(2, pairs.Count);
            var pair = Assert.IsType<PyTuple>(pairs[0]);
            Assert.Equal("café", Assert.IsType<PyString>(pair[0]).AsString());
            Events.Add($"hook:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth}");
            return pair[1];
        }
    }
}
