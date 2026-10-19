using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CountedReplaceBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "literal", "return source.replace('a','bb',1)", false },
        { "hooks", "return source.replace(old(),replacement(),limit())", false },
        { "bad_old", "return source.replace(bad(),replacement(),limit())", true },
        { "bad_new", "return source.replace(old(),bad(),limit())", true },
        { "bad_count", "return source.replace(old(),replacement(),wrong())", true },
        { "count_error", "return source.replace(old(),replacement(),fail())", true },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task CountedReplacementPreservesEffectsAndUnwind(string label, string source, bool fails)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            string[] fullEffects;
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (fails)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal(label == "count_error" ? "ValueError" : "TypeError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal("bb😀a", Assert.IsType<PyString>(result).AsString());
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "literal" => [],
                    "hooks" => ["old", "new", "count"],
                    "bad_old" => ["bad", "new", "count"],
                    "bad_new" => ["old", "bad", "count"],
                    "bad_count" => ["old", "new", "wrong"],
                    _ => new[] { "old", "new", "fail" },
                }, fullEffects);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var effects = new List<string>();
        context.Variables["source"] = PyString.FromString("a😀a", context.MemoryGovernor, null);
        context.Variables["old"] = new Argument(effects, "old", PyString.FromString("a", context.MemoryGovernor, null));
        context.Variables["replacement"] = new Argument(effects, "new", PyString.FromString("bb", context.MemoryGovernor, null));
        context.Variables["limit"] = new Argument(effects, "count", new BigInteger(1));
        context.Variables["bad"] = new Argument(effects, "bad", new BigInteger(7));
        context.Variables["wrong"] = new Argument(effects, "wrong", PyString.FromString("wrong", context.MemoryGovernor, null));
        context.Variables["fail"] = new Fail(effects);
        return (context, effects);
    }

    private static string Describe(LythonRuntimeException error)
        => $"{error.ExceptionType}:{error.Span?.Start},{error.Span?.Length},{error.Span?.Line},{error.Span?.Column}";

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

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
        {
            effects.Add(name);
            return value;
        }
    }

    private sealed class Note(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add(Assert.IsType<PyString>(arguments[0].Value).AsString());
            return new BigInteger(7);
        }
    }

    private sealed class Fail(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            effects.Add("fail");
            throw new LythonRuntimeException("ValueError", "annotation fixture", span);
        }
    }
}
