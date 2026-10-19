using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class BoundedStartsWithBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "one-prefix", "return source.startswith('a')", false },
        { "two-arguments", "return source.startswith('😀',1)", false },
        { "three-arguments", "return source.startswith('😀',1,2)", false },
        { "hooks", "return source.startswith(prefix(),start(),end())", false },
        { "bad_prefix", "return source.startswith(bad(),start(),end())", true },
        { "bad_start", "return source.startswith(prefix(),wrong(),end())", true },
        { "bad_end", "return source.startswith(prefix(),start(),wrong())", true },
        { "bound_error", "return source.startswith(prefix(),start(),fail())", true },
        { "guest-bounds", "class Bound:\n def __init__(self,name):self.name=name\n def __index__(self):return note(self.name)\nreturn source.startswith('😀',Bound('start'),Bound('end'))", false },
        { "guest-bad-start", "class Start:\n def __index__(self):note('start');return 'bad'\nclass End:\n @property\n def __index__(self):note('end');return lambda:2\nreturn source.startswith('😀',Start(),End())", true },
    };

    public static IEnumerable<object[]> AffixPrograms()
    {
        foreach (var row in Programs)
        foreach (var method in new[] { "startswith", "endswith" })
        {
            var source = ((string)row[1]).Replace("startswith", method, StringComparison.Ordinal);
            // Both native two-argument controls match the shared a😀a source.
            if (method == "endswith" && (string)row[0] == "two-arguments")
                source = source.Replace("'😀',1)", "'a',1)", StringComparison.Ordinal);
            yield return [row[0], source, row[2]];
        }
    }

    [Theory]
    [MemberData(nameof(AffixPrograms))]
    public async Task BoundsPreserveEffectsAndUnwind(string label, string source, bool fails)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
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
                    Assert.Equal(label == "bound_error" ? "ValueError" : "TypeError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.True(Assert.IsType<bool>(result));
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "one-prefix" or "two-arguments" or "three-arguments" => [],
                    "hooks" => ["prefix", "start", "end"],
                    "bad_prefix" => ["bad", "start", "end"],
                    "bad_start" => ["prefix", "wrong", "end"],
                    "bad_end" => ["prefix", "start", "wrong"],
                    "guest-bounds" => ["start", "end"],
                    "guest-bad-start" => ["start"],
                    _ => new[] { "prefix", "start", "fail" },
                }, fullEffects);
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var effects = new List<string>();
        context.Variables["source"] = PyString.FromString("a😀a", context.MemoryGovernor, null);
        context.Variables["prefix"] = new Argument(effects, "prefix", PyString.FromString("😀", context.MemoryGovernor, null));
        context.Variables["start"] = new Argument(effects, "start", new BigInteger(1));
        context.Variables["end"] = new Argument(effects, "end", new BigInteger(2));
        context.Variables["bad"] = new Argument(effects, "bad", new BigInteger(7));
        context.Variables["wrong"] = new Argument(effects, "wrong", PyString.FromString("wrong", context.MemoryGovernor, null));
        context.Variables["fail"] = new Fail(effects);
        context.Variables["note"] = new Note(effects);
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
            var name = Assert.IsType<PyString>(arguments[0].Value).AsString();
            effects.Add(name);
            return new BigInteger(name == "start" ? 1 : 2);
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
