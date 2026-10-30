using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonStringDispatchBoundaryTests
{

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StringScalarsAndPairsHooksPreserveEffectsAndUnwind(bool asynchronous, bool pairs)
    {
        var source = "import json\nreturn json.loads(source" + (pairs ? ",object_pairs_hook=hook)" : ")");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] fullEffects;
        using (var owner = budget.CreateScope())
        {
            var (context, hook) = Prepare(owner, pairs);
            Assert.Equal("a\n😀", Assert.IsType<PyString>(await Run(code, context, asynchronous)).AsString());
            fullEffects = hook.Events.ToArray();
            Assert.Equal(pairs ? 1 : 0, hook.Events.Count);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(pairs ? ["hook"] : Array.Empty<string>(), fullEffects);
    }

    private static (Context Context, Hook Hook) Prepare(ILythonMemoryBudget owner, bool pairs)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var document = pairs ? "{\"café\":\"a\\n😀\",\"z\":\"end\"}" : "\"a\\n😀\"";
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
            Events.Add("hook");
            return pair[1];
        }
    }
}
