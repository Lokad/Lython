using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class AnnotationOnlyLocalBoundaryTests
{

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "declaration", "def helper(value):\n local:note('annotation')\n return value+6\nreturn helper(note('argument'))", false },
        { "target_reads", "def helper():\n table()[pick()]:note('annotation')\n return 7\nreturn helper()", false },
        { "target_error", "def helper():\n fail()[pick()]:note('annotation')\n return 7\nreturn helper()", true },
        { "protected", "def helper():\n local:note('annotation')\n try:return 7\n finally:note('finally')\nreturn helper()", false },
        { "closure", "def outer():\n value:note('annotation')\n value=7\n def inner():return value\n return inner\nreturn outer()()", false },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DeclarationTargetsPreserveEffectsAndUnwind(string label, string source, bool fails)
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
                    Assert.Equal("ValueError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.Equal(new BigInteger(7), result);
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "declaration" => ["argument"],
                    "target_reads" => ["base", "index"],
                    "target_error" => ["fail"],
                    "protected" => ["finally"],
                    _ => Array.Empty<string>()
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
        context.Variables["note"] = new Note(effects);
        context.Variables["fail"] = new Fail(effects);
        context.Variables["table"] = new Argument(effects, "base", new PyList([], context.MemoryGovernor));
        context.Variables["pick"] = new Argument(effects, "index", new BigInteger(9999));
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
            return BigInteger.One;
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
