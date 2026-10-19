using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StringReplaceBoundaryTests
{

    // Frozen before direct receiver lowering at 9321533b.
    private static readonly string[] LiteralCheckpoints =
    [
        "-:",
        "7,6,1,8:",
        "7,14,1,8:",
        "22,3,1,23:",
        "26,4,1,27:",
        "7,24,1,8:",
        "7,24,1,8:",
        "7,24,1,8:",
        "7,24,1,8:",
        "0,31,1,1:",
    ];

    // Frozen before direct receiver lowering at 9321533b.
    private static readonly string[] HooksCheckpoints =
    [
        "-:",
        "7,6,1,8:",
        "7,14,1,8:",
        "22,3,1,23:",
        "22,5,1,23:",
        "22,5,1,23:",
        "22,5,1,23:",
        "28,11,1,29:old:2,0",
        "28,13,1,29:old:2,0",
        "28,13,1,29:old:2,0",
        "28,13,1,29:old:2,0",
        "7,35,1,8:old:2,0;new:2,0",
        "7,35,1,8:old:2,0;new:2,0",
        "7,35,1,8:old:2,0;new:2,0",
        "7,35,1,8:old:2,0;new:2,0",
        "0,42,1,1:old:2,0;new:2,0",
    ];

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task EveryCallCheckpointPreservesEffectsAndUnwinds(bool asynchronous, bool hooks)
    {
        var source = hooks ? "return source.replace(old(),replacement())" : "return source.replace('a','bb')";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] fullEffects;
        using (var owner = budget.CreateScope())
        {
            var (context, events) = Prepare(owner);
            Assert.Equal("bb😀bb", Assert.IsType<PyString>(await Run(code, context, asynchronous)).AsString());
            fullEffects = events.ToArray();
            Assert.Equal(hooks ? 2 : 0, events.Count);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(hooks ? new string[] { "old:2,0", "new:2,0" } : Array.Empty<string>(), fullEffects);
    }

    private static (Context Context, List<string> Events) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        context.Variables["source"] = PyString.FromString("a😀a", context.MemoryGovernor, null);
        var events = new List<string>();
        context.Variables["old"] = new Hook("old", PyString.FromString("a", context.MemoryGovernor, null), events);
        context.Variables["replacement"] = new Hook("new", PyString.FromString("bb", context.MemoryGovernor, null), events);
        return (context, events);
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

    private sealed class Hook(string name, PyString value, List<string> events) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            events.Add($"{name}:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth}");
            return value;
        }
    }
}
